using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jint;
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
}
