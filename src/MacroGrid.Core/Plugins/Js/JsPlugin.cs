using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jint;
using MacroGrid.Core.Input;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>Thrown by the host API to the script (it arrives there as an ordinary, catchable <c>Error</c>).</summary>
public sealed class JsHostException(string message) : Exception(message);

/// <summary>
/// A <c>kind: "js"</c> plugin running in a Jint sandbox. Security boundary, in the order it matters:
/// <list type="number">
/// <item>The engine gets no .NET access at all (no CLR interop, no host objects): the script only sees the plain
/// <c>host</c> object, built from a few delegates, so it can reach nothing but what that object offers.</item>
/// <item>Every host call checks the permissions the user approved (<see cref="JsPermissions"/>); a missing one
/// throws a catchable error. Variables can only be published under the plugin's own <c>&lt;id&gt;.</c> prefix and
/// HTTP only goes to approved host:port pairs.</item>
/// <item>Each entry into the script (start-up, an action, a timer) has a time, statement, recursion and memory
/// budget (<see cref="JsPluginLimits"/>), so a runaway script is cut off.</item>
/// <item>The script runs alone on its own thread, one job at a time, so a slow or blocked plugin never holds up
/// the server or another plugin. A plugin that keeps failing is switched off.</item>
/// </list>
/// The script registers everything at its top level (actions, settings page, timers); registrations made later
/// from a callback are not picked up.
/// </summary>
public sealed partial class JsPlugin : IPlugin, IDisposable
{
    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(10);
    private const int MaxPendingTimerJobs = 50;

    private readonly PluginManifest _manifest;
    private readonly string _scriptPath;
    private readonly JsPermissions _permissions;
    private readonly IVariableStore _variables;
    private readonly IInputService? _input;
    private readonly ILogger _logger;
    private readonly Action<string> _onFaulted;
    private readonly JsPluginLimits _limits;

    private readonly BlockingCollection<Action> _jobs = [];
    private readonly Dictionary<int, Timer> _timers = [];
    private readonly Dictionary<string, IPluginStatusItem> _statusItems = [];
    private readonly List<VariableInfo> _described = [];
    private readonly HttpClient _http;
    private readonly Lock _timerLock = new();
    private Engine? _engine;
    private IPluginHost? _host;
    private JsSettingsPage? _settingsPage;
    private Thread? _thread;
    private int _pendingTimerJobs;
    private int _consecutiveErrors;
    private volatile bool _disposed;
    private bool _initialized;

    public JsPlugin(
        PluginManifest manifest,
        string scriptPath,
        JsPermissions permissions,
        IVariableStore variables,
        IInputService? input,
        ILogger logger,
        Action<string> onFaulted,
        JsPluginLimits? limits = null)
    {
        _manifest = manifest;
        _scriptPath = scriptPath;
        _permissions = permissions;
        _variables = variables;
        _input = input;
        _logger = logger;
        _onFaulted = onFaulted;
        _limits = limits ?? JsPluginLimits.Default;
        // Redirects are off: a redirect could send an approved request to a host that was never approved.
        _http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = _limits.HttpTimeout };
    }

    public void Initialize(IPluginHost host)
    {
        _host = host;
        _thread = new Thread(RunLoop) { IsBackground = true, Name = $"js-plugin:{_manifest.Id}" };
        _thread.Start();

        var script = File.ReadAllText(_scriptPath);
        var started = Post(() =>
        {
            _engine = CreateEngine();
            _engine.Execute(Bootstrap, "host-bootstrap");
            _engine.Execute(script, _manifest.Entry);
            return 0;
        });

        try
        {
            if (!started.Wait(InitTimeout))
                throw new TimeoutException("The plugin did not finish starting in time.");
        }
        catch (AggregateException ex)
        {
            throw new InvalidOperationException(Describe(ex.InnerException ?? ex), ex.InnerException);
        }

        if (_settingsPage is not null) host.RegisterSettingsPage(_settingsPage);
        if (_described.Count > 0) host.RegisterVariableProvider(new DescriptionProvider(_described));
        _initialized = true;
    }

    /// <summary>Runs a registered action's function on the plugin thread and completes when it returns.</summary>
    internal Task RunActionAsync(string type, ActionContext context, JsonObject settings, CancellationToken ct)
    {
        var contextJson = JsonSerializer.Serialize(new { context.DeviceId, context.PageId, context.WidgetId, context.Value }, ProtocolJson.Options);
        var run = Post(() =>
        {
            Invoke("__runAction", type, contextJson, settings.ToJsonString());
            return 0;
        });
        return run.WaitAsync(ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_timerLock)
        {
            foreach (var timer in _timers.Values) timer.Dispose();
            _timers.Clear();
        }
        _jobs.CompleteAdding();
        _http.Dispose();
        // The loop ends when it has drained; an engine call cannot be interrupted, but every call has a time limit.
    }

    // ---- the plugin thread ----

    private void RunLoop()
    {
        foreach (var job in _jobs.GetConsumingEnumerable())
            job();
    }

    private Task<T> Post<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_disposed)
        {
            tcs.SetException(new ObjectDisposedException(nameof(JsPlugin)));
            return tcs.Task;
        }

        try
        {
            _jobs.Add(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
        }
        catch (InvalidOperationException) // the queue was completed by Dispose
        {
            tcs.SetException(new ObjectDisposedException(nameof(JsPlugin)));
        }
        return tcs.Task;
    }

    private Engine CreateEngine()
    {
        var engine = new Engine(options =>
        {
            options.Strict();
            options.TimeoutInterval(_limits.CallTimeout);
            options.LimitMemory(_limits.MemoryBytes);
            options.MaxStatements(_limits.MaxStatements);
            options.LimitRecursion(_limits.MaxRecursion);
            // Only our own host errors become script errors; anything else is a bug and must not be swallowed.
            options.CatchClrExceptions(ex => ex is JsHostException);
        });

        engine.SetValue("__permissions", new Func<string>(() => JsonSerializer.Serialize(_permissions.Granted)));
        engine.SetValue("__log", new Action<string>(message => _host!.Log(Truncate(message, 500))));
        engine.SetValue("__varSet", new Action<string, object?>(VarSet));
        engine.SetValue("__varGet", new Func<string, object?>(VarGet));
        engine.SetValue("__varRemove", new Action<string>(VarRemove));
        engine.SetValue("__varDescribe", new Action<string>(VarDescribe));
        engine.SetValue("__registerAction", new Action<string>(RegisterAction));
        engine.SetValue("__settingsPage", new Action<string>(SettingsPage));
        engine.SetValue("__settingsGet", new Func<string>(() => (_settingsPage?.Load() ?? []).ToJsonString()));
        engine.SetValue("__status", new Action<string, string, string>(Status));
        engine.SetValue("__hotkey", new Action<string>(Hotkey));
        engine.SetValue("__type", new Action<string>(TypeText));
        engine.SetValue("__http", new Func<string, string, string, string, string>(Http));
        engine.SetValue("__timer", new Action<int, int, bool>(StartTimer));
        engine.SetValue("__cancel", new Action<int>(CancelTimer));
        return engine;
    }

    /// <summary>Calls a global script function under the per-call budget and does the error accounting.</summary>
    private void Invoke(string function, params object[] args)
    {
        try
        {
            _engine!.Invoke(function, args);
            if (_initialized) Interlocked.Exchange(ref _consecutiveErrors, 0);
        }
        catch (Exception ex)
        {
            var message = Describe(ex);
            _logger.LogWarning("[{PluginId}] {Function} failed: {Error}", _manifest.Id, function, message);
            if (_initialized && Interlocked.Increment(ref _consecutiveErrors) >= _limits.MaxConsecutiveErrors)
                _onFaulted($"Switched off after {_limits.MaxConsecutiveErrors} failures in a row. Last error: {message}");
            throw new InvalidOperationException(message, ex);
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        Jint.Runtime.JavaScriptException js => js.Message,
        TimeoutException => $"Took too long (limit reached). {ex.Message}",
        _ => ex.Message,
    };

    // ---- host API (each call checks its permission) ----

    private void Require(string permission)
    {
        if (!_permissions.Has(permission))
            throw new JsHostException($"This plugin has not been granted the '{permission}' permission.");
    }

    private void VarSet(string name, object? value)
    {
        Require(JsPermissions.Variables);
        _variables.Set(OwnName(name), value switch
        {
            null => null,
            double or bool or string => value,
            _ => value.ToString(),
        });
    }

    private object? VarGet(string name)
    {
        Require(JsPermissions.Variables);
        return _variables.Get(name);
    }

    private void VarRemove(string name)
    {
        Require(JsPermissions.Variables);
        _variables.Remove(OwnName(name));
    }

    private void VarDescribe(string json)
    {
        Require(JsPermissions.Variables);
        foreach (var info in JsonSerializer.Deserialize<VariableInfo[]>(json, ProtocolJson.Options) ?? [])
        {
            OwnName(info.Name);
            _described.Add(info);
        }
    }

    /// <summary>A plugin may only publish variables under its own id, so it can never overwrite <c>system.cpu</c> or another plugin's values.</summary>
    private string OwnName(string name)
    {
        if (!name.StartsWith(_manifest.Id + ".", StringComparison.Ordinal) || name.Length > 120 || !VariableName().IsMatch(name))
            throw new JsHostException($"Variable names must start with '{_manifest.Id}.' and use only letters, digits, '.', '_' and '-'.");
        return name;
    }

    private void RegisterAction(string json)
    {
        Require(JsPermissions.Actions);
        var meta = JsonSerializer.Deserialize<JsActionMeta>(json, ProtocolJson.Options)
            ?? throw new JsHostException("registerAction needs a definition.");
        if (string.IsNullOrWhiteSpace(meta.Type) || !meta.Type.StartsWith(_manifest.Id + ".", StringComparison.Ordinal))
            throw new JsHostException($"Action types must start with '{_manifest.Id}.'.");
        _host!.RegisterAction(new JsAction(this, meta));
    }

    private void SettingsPage(string json)
    {
        var fields = JsonSerializer.Deserialize<SettingField[]>(json, ProtocolJson.Options)
            ?? throw new JsHostException("settings.page needs a list of fields.");
        _settingsPage = new JsSettingsPage(_host!.DataDirectory, fields);
    }

    private void Status(string id, string text, string level)
    {
        if (!_statusItems.TryGetValue(id, out var item))
        {
            if (_statusItems.Count >= 10) throw new JsHostException("A plugin can have at most 10 status items.");
            _statusItems[id] = item = _host!.CreateStatusItem(id);
        }
        item.Update(Truncate(text, 80), Enum.TryParse<StatusLevel>(level, ignoreCase: true, out var parsed) ? parsed : StatusLevel.Idle);
    }

    private void Hotkey(string combo)
    {
        Require(JsPermissions.Input);
        if (!HotkeyParser.TryParse(combo, out var parsed, out var error))
            throw new JsHostException($"Not a valid key combination: {error}");
        (_input ?? throw new JsHostException("Keyboard input is not available.")).SendKeyCombo(parsed);
    }

    private void TypeText(string text)
    {
        Require(JsPermissions.Input);
        (_input ?? throw new JsHostException("Keyboard input is not available.")).TypeText(Truncate(text, 2000));
    }

    private string Http(string method, string url, string body, string headersJson)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new JsHostException("Not a valid URL.");
        if (!_permissions.AllowsHttp(uri))
            throw new JsHostException($"This plugin has not been granted 'http:{uri.Host}:{uri.Port}'.");

        using var request = new HttpRequestMessage(new HttpMethod(method), uri);
        if (method != "GET") request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [])
            request.Headers.TryAddWithoutValidation(key, value);

        try
        {
            using var response = _http.Send(request);
            using var stream = response.Content.ReadAsStream();
            var buffer = new byte[_limits.MaxHttpResponseBytes + 1];
            var read = 0;
            int n;
            while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0) read += n;
            if (read > _limits.MaxHttpResponseBytes) throw new JsHostException("The response is too large.");
            return JsonSerializer.Serialize(new { status = (int)response.StatusCode, body = System.Text.Encoding.UTF8.GetString(buffer, 0, read) });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new JsHostException($"The request failed: {ex.Message}");
        }
    }

    private void StartTimer(int id, int intervalMs, bool repeat)
    {
        if (intervalMs < 100) throw new JsHostException("The shortest timer is 100 ms.");
        lock (_timerLock)
        {
            if (_disposed) return;
            if (_timers.Count >= _limits.MaxTimers) throw new JsHostException($"A plugin can have at most {_limits.MaxTimers} timers.");
            var timer = new Timer(_ => FireTimer(id, repeat), null, intervalMs, repeat ? intervalMs : Timeout.Infinite);
            _timers[id] = timer;
        }
    }

    private void CancelTimer(int id)
    {
        lock (_timerLock)
        {
            if (_timers.Remove(id, out var timer)) timer.Dispose();
        }
    }

    private void FireTimer(int id, bool repeat)
    {
        // A busy plugin must not pile up ticks: extra ones are dropped, not queued.
        if (Interlocked.Increment(ref _pendingTimerJobs) > MaxPendingTimerJobs)
        {
            Interlocked.Decrement(ref _pendingTimerJobs);
            return;
        }
        if (!repeat) CancelTimer(id);

        _ = Post(() =>
        {
            try { Invoke("__fire", id); }
            catch (InvalidOperationException) { /* already logged and counted */ }
            finally { Interlocked.Decrement(ref _pendingTimerJobs); }
            return 0;
        });
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    [GeneratedRegex(@"^[A-Za-z0-9._\-]+$")]
    private static partial Regex VariableName();

    /// <summary>Publishes the catalog entries the script described, so the editor's variable picker lists them.</summary>
    private sealed class DescriptionProvider(List<VariableInfo> infos) : IVariableProvider, IVariableCatalogSource
    {
        public IEnumerable<VariableInfo> Describe() => infos;
        public Task RunAsync(IVariableStore store, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Builds the one object the script sees. The native delegates are captured in a closure and removed from the
    /// global scope, so the script cannot reach them (or replace what the wrappers call).
    /// </summary>
    private const string Bootstrap = """
        (function () {
          'use strict';
          const g = globalThis;
          const names = ['permissions', 'log', 'varSet', 'varGet', 'varRemove', 'varDescribe', 'registerAction',
            'settingsPage', 'settingsGet', 'status', 'hotkey', 'type', 'http', 'timer', 'cancel'];
          const n = {};
          for (const name of names) { n[name] = g['__' + name]; delete g['__' + name]; }

          const actions = Object.create(null);
          const timers = Object.create(null);
          let nextTimer = 1;
          const start = (fn, ms, repeat) => {
            if (typeof fn !== 'function') throw new Error('The timer needs a function.');
            const id = nextTimer++;
            timers[id] = { fn, repeat };
            n.timer(id, Math.floor(Number(ms)), repeat);
            return id;
          };
          const request = (method, url, body, options) =>
            JSON.parse(n.http(method, String(url), body === undefined ? '' : JSON.stringify(body), JSON.stringify((options && options.headers) || {})));

          const host = {
            permissions: Object.freeze(JSON.parse(n.permissions())),
            log: (message) => n.log(String(message)),
            variables: Object.freeze({
              set: (name, value) => n.varSet(String(name), value === undefined ? null : value),
              get: (name) => n.varGet(String(name)),
              remove: (name) => n.varRemove(String(name)),
              describe: (list) => n.varDescribe(JSON.stringify(list)),
            }),
            registerAction: (definition) => {
              if (!definition || typeof definition.run !== 'function') throw new Error('registerAction needs a run function.');
              const meta = Object.assign({}, definition);
              delete meta.run;
              n.registerAction(JSON.stringify(meta));
              actions[definition.type] = definition.run;
            },
            settings: Object.freeze({
              page: (fields) => n.settingsPage(JSON.stringify(fields)),
              get: () => JSON.parse(n.settingsGet()),
            }),
            status: (id, text, level) => n.status(String(id), String(text), String(level || 'Idle')),
            input: Object.freeze({ hotkey: (combo) => n.hotkey(String(combo)), type: (text) => n.type(String(text)) }),
            http: Object.freeze({
              get: (url, options) => request('GET', url, undefined, options),
              post: (url, body, options) => request('POST', url, body, options),
            }),
            every: (ms, fn) => start(fn, ms, true),
            after: (ms, fn) => start(fn, ms, false),
            cancel: (id) => { delete timers[id]; n.cancel(id); },
          };
          Object.defineProperty(g, 'host', { value: Object.freeze(host), writable: false, configurable: false });

          g.__runAction = function (type, contextJson, settingsJson) {
            const run = actions[type];
            if (!run) throw new Error('Unknown action ' + type);
            run(JSON.parse(contextJson), JSON.parse(settingsJson));
          };
          g.__fire = function (id) {
            const timer = timers[id];
            if (!timer) return;
            if (!timer.repeat) delete timers[id];
            timer.fn();
          };
        })();
        """;
}

/// <summary>What a script passes to <c>host.registerAction</c> (everything except the run function).</summary>
internal sealed record JsActionMeta(string Type, string? Name, string? Category, string? Description, string? Icon, SettingField[]? Fields);

internal sealed class JsAction(JsPlugin plugin, JsActionMeta meta) : IActionHandler, IActionDescriptor
{
    public string Type => meta.Type;
    public string DisplayName => string.IsNullOrWhiteSpace(meta.Name) ? meta.Type : meta.Name;
    public string Category => string.IsNullOrWhiteSpace(meta.Category) ? "Plugins" : meta.Category;
    public string? Description => meta.Description;
    public string? Icon => meta.Icon;
    public IReadOnlyList<SettingField> Fields => meta.Fields ?? [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        plugin.RunActionAsync(Type, context, settings, cancellationToken);
}

/// <summary>The plugin's settings page: the fields the script declared, stored in <c>settings.json</c> in its folder.</summary>
internal sealed class JsSettingsPage(string dataDirectory, IReadOnlyList<SettingField> fields) : IPluginSettingsPage
{
    private readonly string _path = Path.Combine(dataDirectory, "settings.json");
    private readonly Lock _lock = new();

    public IReadOnlyList<SettingField> Fields => fields;

    public JsonObject Load()
    {
        var values = new JsonObject();
        foreach (var field in fields)
            if (field.Default is not null) values[field.Key] = field.Default.DeepClone();

        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && JsonNode.Parse(File.ReadAllText(_path)) is JsonObject saved)
                    foreach (var (key, value) in saved) values[key] = value?.DeepClone();
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* fall back to the defaults */ }
        }
        return values;
    }

    public void Save(JsonObject values)
    {
        // Only keys the plugin declared are kept, so a caller cannot store arbitrary data in the plugin's folder.
        var kept = new JsonObject();
        foreach (var field in fields)
            if (values[field.Key] is { } value) kept[field.Key] = value.DeepClone();

        lock (_lock)
        {
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(_path, kept.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
