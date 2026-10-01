using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Jint;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Sessions;
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
public sealed partial class JsPlugin : IPlugin, IPluginWidgetHandler, IDisposable
{
    private static readonly TimeSpan InitTimeout = TimeSpan.FromSeconds(10);
    private const int MaxPendingTimerJobs = 50;

    private readonly PluginManifest _manifest;
    private readonly string _scriptPath;
    private readonly JsPermissions _permissions;
    private readonly IVariableStore _variables;
    private readonly IInputService? _input;
    private readonly ILogger _logger;
    private readonly ProblemList? _problems;
    // The last message a refused key press put on the problem list; a script that does not catch it fails the whole call with the same text, which must not be listed twice.
    private volatile string? _lastReported;
    private readonly Action<string> _onFaulted;
    private readonly JsPluginLimits _limits;
    private readonly IActiveWindowSource? _windows;
    private readonly Func<bool> _isElevated;

    // The press window of the job that is running on the plugin thread right now (null for start-up, timers and anything
    // else that is not a button press). Only the plugin thread reads or writes it.
    private JsPressWindow? _press;
    private bool _inputBlocked;
    private readonly Lock _usageLock = new();
    private DateOnly _usageDay;
    private int _usesToday;

    private readonly BlockingCollection<Action> _jobs = [];
    private readonly Dictionary<int, Timer> _timers = [];
    private readonly Dictionary<string, IPluginStatusItem> _statusItems = [];
    private readonly List<VariableInfo> _described = [];
    private readonly HttpClient _http;
    private readonly Lock _timerLock = new();
    private Engine? _engine;
    private IPluginHost? _host;
    private JsSettingsPage? _settingsPage;
    private JsStorage? _storage;
    private Thread? _thread;
    private int _pendingTimerJobs;
    private int _pendingHttp;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _outcomeWaits = [];
    private int _nextRunId;
    private readonly CancellationTokenSource _disposeCts = new();
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
        JsPluginLimits? limits = null,
        IActiveWindowSource? windows = null,
        Func<bool>? isElevated = null,
        ProblemList? problems = null,
        JsNetworkPolicy? network = null)
    {
        _problems = problems;
        _windows = windows;
        _isElevated = isElevated ?? JsInputPolicy.ServerIsElevated;
        _manifest = manifest;
        _scriptPath = scriptPath;
        _permissions = permissions;
        _variables = variables;
        _input = input;
        _logger = logger;
        _onFaulted = onFaulted;
        _limits = limits ?? JsPluginLimits.Default;
        // Redirects and the system proxy are off and the connection is made by the network guard: an approved request may only reach the kind of address that was approved.
        _http = new HttpClient(JsNetworkGuard.CreateHandler(network ?? JsNetworkPolicy.None, ReportNetworkRefused)) { Timeout = _limits.HttpTimeout };
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

    /// <summary>How many runs wait for their outcome right now (for tests).</summary>
    internal int PendingOutcomeWaits => _outcomeWaits.Count;

    /// <summary>The most async actions one plugin may have waiting for their outcome at once.</summary>
    private const int MaxPendingOutcomes = 16;

    /// <summary>The longest outcome text read back from a script (anything larger is a provider error with the default text).</summary>
    private const int MaxOutcomeJson = 4096;

    /// <summary>Runs a registered action's function on the plugin thread. A reported failure (<c>ok: false</c>) is a result here, not an
    /// exception: it is no script fault, so it adds no Error List line and does not count towards switching the plugin off. A thrown error still does.</summary>
    internal async Task<ActionOutcome> RunActionAsync(string type, bool waitsForOutcome, ActionContext context, JsonObject settings, CancellationToken ct)
    {
        var contextJson = JsonSerializer.Serialize(new { context.DeviceId, context.PageId, context.WidgetId, context.Value }, ProtocolJson.Options);
        TaskCompletionSource<string>? wait = null;
        var runId = 0;
        var run = Post<ActionOutcome?>(() =>
        {
            if (waitsForOutcome)
            {
                if (_outcomeWaits.Count >= MaxPendingOutcomes) return ActionOutcome.Failed(ActionFailureCode.Unavailable, "Too many runs are waiting.");
                runId = Interlocked.Increment(ref _nextRunId);
                wait = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                _outcomeWaits[runId] = wait;
            }

            // A person touched a device: keyboard input is allowed for this job and the continuations it starts. An action a widget's script
            // started without a real touch (ActionContext.UserGesture false) gets no press window, so it cannot type.
            _press = context.UserGesture ? new JsPressWindow(Stopwatch.GetTimestamp() + (long)(_limits.PressWindow.TotalSeconds * Stopwatch.Frequency)) : null;
            string? shape;
            try { shape = Invoke("__runAction", type, contextJson, settings.ToJsonString(), runId); }
            catch { _outcomeWaits.TryRemove(runId, out _); throw; }
            finally { _press = null; }

            if (shape is null && wait is not null) return null; // an async action: its promise settles later
            _outcomeWaits.TryRemove(runId, out _);
            return ReadOutcome(shape);
        });

        var immediate = await run.WaitAsync(ct);
        if (immediate is not null || wait is null) return immediate ?? ActionOutcome.Success;

        try { return ReadOutcome(await wait.Task.WaitAsync(_limits.ActionOutcomeTimeout, ct)); }
        catch (TimeoutException) { return ActionOutcome.Failed(ActionFailureCode.Timeout); }
        finally { _outcomeWaits.TryRemove(runId, out _); }
    }

    /// <summary>Reads what a script reported: null or '' is success; otherwise <c>{ ok: false | "accepted", code?, message? }</c>.</summary>
    private static ActionOutcome ReadOutcome(string? json)
    {
        if (string.IsNullOrEmpty(json)) return ActionOutcome.Success;
        if (json.Length > MaxOutcomeJson) return ActionOutcome.Failed(ActionFailureCode.ProviderError);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.String) return ActionOutcome.Accepted(message);
            var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                && Enum.TryParse<ActionFailureCode>(c.GetString(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : ActionFailureCode.ProviderError;
            return ActionOutcome.Failed(code, message);
        }
        catch (JsonException)
        {
            return ActionOutcome.Failed(ActionFailureCode.ProviderError);
        }
    }

    /// <summary>Called by the script when an <c>outcome: true</c> action's promise settles (on the plugin thread).</summary>
    private void ActionDone(int runId, string json)
    {
        if (_outcomeWaits.TryRemove(runId, out var wait)) wait.TrySetResult(json);
    }

    /// <summary>How many button presses used the keyboard through this plugin today (shown in the Plugins window).</summary>
    internal int KeyboardUsesToday
    {
        get
        {
            lock (_usageLock) return _usageDay == DateOnly.FromDateTime(DateTime.Now) ? _usesToday : 0;
        }
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
        foreach (var wait in _outcomeWaits.Values) wait.TrySetResult("""{"ok":false,"code":"Unavailable"}""");
        _outcomeWaits.Clear();
        _disposeCts.Cancel();
        _http.Dispose();
        _disposeCts.Dispose();
        _storage?.Dispose();
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
        engine.SetValue("__pressEnd", new Action(() => { if (_press is { } press) press.Settled = true; }));
        engine.SetValue("__http", new Func<string, string, string, string, string>(Http));
        engine.SetValue("__httpAsync", new Action<int, string, string, string, string>(HttpAsync));
        engine.SetValue("__storageGet", new Func<string, string?>(StorageGet));
        engine.SetValue("__storageSet", new Action<string, string>(StorageSet));
        engine.SetValue("__storageRemove", new Action<string>(StorageRemove));
        engine.SetValue("__storageKeys", new Func<string>(StorageKeys));
        engine.SetValue("__widgetPost", new Action<string>(WidgetPost));
        engine.SetValue("__diagnosticsReport", new Action<string, string, string?>(DiagnosticsReport));
        engine.SetValue("__diagnosticsClear", new Action(() => _host!.Diagnostics.Clear()));
        engine.SetValue("__diagnosticsResolve", new Action<string>(key => _host!.Diagnostics.Resolve(key)));
        engine.SetValue("__widgetReply", new Action<int, bool, string>(WidgetReply));
        engine.SetValue("__timer", new Action<int, int, bool>(StartTimer));
        engine.SetValue("__cancel", new Action<int>(CancelTimer));
        engine.SetValue("__actionDone", new Action<int, string>(ActionDone));
        return engine;
    }

    /// <summary>Calls a global script function under the per-call budget and does the error accounting.</summary>
    private string? Invoke(string function, params object[] args)
    {
        try
        {
            var result = _engine!.Invoke(function, args);
            // Promise callbacks (an awaited async request, a .then) only run when the engine is asked to; without this a
            // settled promise would sit unnoticed until the next unrelated call.
            _engine.Advanced.ProcessTasks();
            if (_initialized) Interlocked.Exchange(ref _consecutiveErrors, 0);
            return result.IsString() ? result.AsString() : null;
        }
        catch (Exception ex)
        {
            var message = Describe(ex);
            _logger.LogWarning("[{PluginId}] {Function} failed: {Error}", _manifest.Id, function, message);
            var repeated = _lastReported is { } last && message.Contains(last, StringComparison.Ordinal);
            if (!repeated) _problems?.Report(_manifest.Id, _manifest.Name, ProblemSeverity.Error, ProblemCodes.CallFailed, $"{function} failed: {message}");
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
