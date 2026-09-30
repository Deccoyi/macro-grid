using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Sessions;

/// <summary>What the router needs from the plugin loader: the running plugin's handler for its widgets' requests.</summary>
public interface IPluginWidgetHost
{
    IPluginWidgetHandler? GetWidgetHandler(string pluginId);
}

/// <summary>A token bucket: <paramref name="Capacity"/> bursts, refilled at <paramref name="PerSecond"/>.</summary>
internal sealed class TokenBucket
{
    private readonly double _capacity;
    private readonly double _perSecond;
    private double _tokens;
    private DateTimeOffset _at;
    private bool _started;

    public TokenBucket(double capacity, double perSecond)
    {
        _capacity = capacity;
        _perSecond = perSecond;
        _tokens = capacity;
    }

    public bool Take(DateTimeOffset now)
    {
        if (!_started) { _started = true; _at = now; }
        _tokens = Math.Min(_capacity, _tokens + (now - _at).TotalSeconds * _perSecond);
        _at = now;
        if (_tokens < 1) return false;
        _tokens -= 1;
        return true;
    }
}

/// <summary>What the router keeps for one connected device.</summary>
internal sealed class PluginWidgetSessionState
{
    public readonly Lock Lock = new();
    /// <summary>Placed widget id to the variable names it subscribed to.</summary>
    public readonly Dictionary<string, HashSet<string>> Subscriptions = new(StringComparer.Ordinal);
    /// <summary>Placed widget id to the last value sent per variable (as JSON text), so only changes go out.</summary>
    public readonly Dictionary<string, Dictionary<string, string>> Sent = new(StringComparer.Ordinal);
    public readonly Dictionary<string, TokenBucket> WidgetBuckets = new(StringComparer.Ordinal);
    /// <summary>For <c>ready</c> and <c>subscribe</c>: a small burst, then two a second, per widget.</summary>
    public readonly Dictionary<string, TokenBucket> LightBuckets = new(StringComparer.Ordinal);
    public readonly TokenBucket DeviceBucket = new(PluginWidgetRouter.MaxRequestsPerSecondPerDevice, PluginWidgetRouter.MaxRequestsPerSecondPerDevice);
    public readonly Dictionary<string, int> InFlight = new(StringComparer.Ordinal);
    public readonly Dictionary<string, Queue<DateTimeOffset>> Errors = new(StringComparer.Ordinal);
    public bool DeniedLogged;
}

/// <summary>
/// Everything a plugin widget on a device asks of the server (docs/design/plugin-widgets.md). The plugin and widget are taken from the widget in the profile
/// this device has open, never from the message; the widget must be on a page of that profile. Then the rules of the widget's plugin apply: it can talk to
/// its own handler, read its own variables (JavaScript plugins need <c>variables</c>) and the variables the person bound in the widget's settings, and run
/// its own actions (JavaScript plugins need <c>actions</c>). Rates, sizes and time are limited here and checked again on every message.
/// </summary>
public sealed class PluginWidgetRouter : IHostedService, IDisposable
{
    public const int MaxRequestBytes = 16 * 1024;
    public const int MaxReplyBytes = 64 * 1024;
    public const int MaxRequestsPerSecondPerWidget = 10;
    public const int MaxRequestsPerSecondPerDevice = 30;
    public const int MaxInFlightPerWidget = 4;
    public const int MaxSubscribedVariables = 32;
    public const int MaxErrorsPer10Seconds = 5;
    public static readonly TimeSpan HandlerTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(10);

    private readonly PluginWidgetCatalog _catalog;
    private readonly IPluginWidgetHost _plugins;
    private readonly PluginPermissionStore _permissions;
    private readonly ProfileStore _profiles;
    private readonly SessionRegistry _sessions;
    private readonly ActionDispatcher _dispatcher;
    private readonly VariableStore _variables;
    private readonly PluginWidgetEventHub _events;
    private readonly ProblemList _problems;
    private readonly ILogger<PluginWidgetRouter> _logger;
    private readonly Func<DateTimeOffset> _clock;
    private readonly HashSet<string> _dirty = [];
    private readonly Lock _dirtyLock = new();
    private Timer? _timer;
    private int _flushing;

    public PluginWidgetRouter(PluginWidgetCatalog catalog, IPluginWidgetHost plugins, PluginPermissionStore permissions, ProfileStore profiles,
        SessionRegistry sessions, ActionDispatcher dispatcher, VariableStore variables, PluginWidgetEventHub events, ProblemList problems,
        ILogger<PluginWidgetRouter> logger, Func<DateTimeOffset>? clock = null)
    {
        _catalog = catalog;
        _plugins = plugins;
        _permissions = permissions;
        _profiles = profiles;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _variables = variables;
        _events = events;
        _problems = problems;
        _logger = logger;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _variables.Changed += OnVariableChanged;
        _events.Posted += OnEvent;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => _ = FlushVariablesAsync(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _variables.Changed -= OnVariableChanged;
        _events.Posted -= OnEvent;
        _timer?.Dispose();
    }

    // ---- what a widget sends ----

    /// <summary>Handles one <c>plugin.widget.request</c>. <paramref name="device"/> is the device controller a <c>run</c> action gets, as for a press.</summary>
    public async Task HandleRequestAsync(ClientSession session, PluginWidgetRequestMessage message, IDeviceController device, CancellationToken ct)
    {
        if (session.DeviceId is null || !session.Supports(ClientCapabilities.PluginWidgets)) return;
        var isCall = message.Kind is PluginWidgetKinds.Request or PluginWidgetKinds.Run;
        if (isCall && message.Id is not > 0) return; // a call without an id cannot be answered
        if (message.Kind is not (PluginWidgetKinds.Ready or PluginWidgetKinds.Request or PluginWidgetKinds.Run or PluginWidgetKinds.Subscribe))
        {
            if (isCall) await ReplyAsync(session, message, ok: false, error: "bad_request");
            return;
        }

        var state = session.WidgetBridge;
        var widget = Locate(session, message.PageId, message.WidgetId);
        if (widget is null || !PluginWidgetProps.TryRead(widget, out var pluginId, out var widgetId))
        {
            LogDenied(session, state);
            if (isCall) await ReplyAsync(session, message, ok: false, error: "not_allowed");
            return;
        }

        var info = _catalog.Resolve(pluginId, widgetId, out _);
        if (info is null)
        {
            if (isCall) await ReplyAsync(session, message, ok: false, error: "plugin_unavailable");
            return;
        }

        if (message.Data is { } data && data.ToJsonString().Length > MaxRequestBytes)
        {
            if (isCall) await ReplyAsync(session, message, ok: false, error: "too_large");
            return;
        }

        switch (message.Kind)
        {
            case PluginWidgetKinds.Ready:
                if (TryStartLight(state, widget.Id)) await SendRetainedAsync(session, widget, info);
                return;
            case PluginWidgetKinds.Subscribe:
                if (TryStartLight(state, widget.Id)) await SubscribeAsync(session, widget, info, message.Data);
                return;
        }
        if (!TryStartCall(state, widget.Id))
        {
            await ReplyAsync(session, message, ok: false, error: "rate_limited");
            return;
        }

        try
        {
            if (message.Kind == PluginWidgetKinds.Request) await RequestAsync(session, message, widget, info, ct);
            else await RunAsync(session, message, widget, info, device, ct);
        }
        finally
        {
            lock (state.Lock) state.InFlight[widget.Id] = Math.Max(0, state.InFlight.GetValueOrDefault(widget.Id) - 1);
        }
    }

    /// <summary>Handles a <c>plugin.widget.error</c>: an error from a widget's own code goes to the Error List, at most five per ten seconds per widget.</summary>
    public void HandleError(ClientSession session, PluginWidgetErrorMessage message)
    {
        if (session.DeviceId is null || !session.Supports(ClientCapabilities.PluginWidgets)) return;
        var state = session.WidgetBridge;
        var widget = Locate(session, message.PageId, message.WidgetId);
        if (widget is null || !PluginWidgetProps.TryRead(widget, out var pluginId, out var widgetId))
        {
            LogDenied(session, state);
            return;
        }
        var info = _catalog.Resolve(pluginId, widgetId, out _);
        if (info is null) return;

        var now = _clock();
        lock (state.Lock)
        {
            if (!state.Errors.TryGetValue(widget.Id, out var times)) state.Errors[widget.Id] = times = new Queue<DateTimeOffset>();
            while (times.Count > 0 && now - times.Peek() > TimeSpan.FromSeconds(10)) times.Dequeue();
            if (times.Count >= MaxErrorsPer10Seconds) return;
            times.Enqueue(now);
        }
        var text = message.Message ?? "";
        _problems.Report(pluginId, info.PluginName, ProblemSeverity.Error, ProblemCodes.WidgetScriptError,
            $"Widget '{widgetId}' on {session.DeviceName ?? "a device"}: {(text.Length > 200 ? text[..200] : text)}");
    }

    /// <summary>Forgets what a device subscribed to when it disconnects or changes profile (its widgets start again and ask again).</summary>
    public void Forget(ClientSession session)
    {
        var state = session.WidgetBridge;
        lock (state.Lock)
        {
            state.Subscriptions.Clear();
            state.Sent.Clear();
        }
    }

    private Widget? Locate(ClientSession session, string pageId, string widgetId) =>
        session.ProfileId is null ? null : _profiles.Get(session.ProfileId)?.FindPage(pageId)?.FindWidget(widgetId);

    private void LogDenied(ClientSession session, PluginWidgetSessionState state)
    {
        lock (state.Lock)
        {
            if (state.DeniedLogged) return;
            state.DeniedLogged = true;
        }
        _logger.LogWarning(SecurityEvents.PluginWidgetDenied,
            "Security: device {Device} sent a plugin widget message for a widget that is not on a page of its profile", SecurityEvents.ForLog(session.DeviceName ?? "?"));
    }

    /// <summary>A <c>ready</c> or <c>subscribe</c> costs the server a read and a push, so it is limited too (no reply to send; the widget simply asks again later).</summary>
    private bool TryStartLight(PluginWidgetSessionState state, string widgetId)
    {
        var now = _clock();
        lock (state.Lock)
        {
            if (!state.LightBuckets.TryGetValue(widgetId, out var bucket))
                state.LightBuckets[widgetId] = bucket = new TokenBucket(4, 2);
            return bucket.Take(now) && state.DeviceBucket.Take(now);
        }
    }

    private bool TryStartCall(PluginWidgetSessionState state, string widgetId)
    {
        var now = _clock();
        lock (state.Lock)
        {
            if (!state.WidgetBuckets.TryGetValue(widgetId, out var bucket))
                state.WidgetBuckets[widgetId] = bucket = new TokenBucket(MaxRequestsPerSecondPerWidget, MaxRequestsPerSecondPerWidget);
            if (state.InFlight.GetValueOrDefault(widgetId) >= MaxInFlightPerWidget) return false;
            if (!bucket.Take(now) || !state.DeviceBucket.Take(now)) return false;
            state.InFlight[widgetId] = state.InFlight.GetValueOrDefault(widgetId) + 1;
            return true;
        }
    }

    private async Task RequestAsync(ClientSession session, PluginWidgetRequestMessage message, Widget widget, PluginWidgetInfo info, CancellationToken ct)
    {
        var handler = _plugins.GetWidgetHandler(info.PluginId);
        if (handler is null)
        {
            await ReplyAsync(session, message, ok: false, error: "not_allowed", text: "This plugin does not answer widget requests.");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HandlerTimeout);
        try
        {
            var body = new PluginWidgetMessage(info.Widget.Manifest.Id, session.DeviceId!, message.PageId, widget.Id, PluginWidgetProps.Settings(widget), message.Data?.DeepClone());
            var reply = await handler.OnWidgetMessageAsync(body, timeout.Token);
            if (reply is not null && reply.ToJsonString().Length > MaxReplyBytes)
            {
                await ReplyAsync(session, message, ok: false, error: "too_large");
                return;
            }
            await ReplyAsync(session, message, ok: true, data: reply);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _problems.Report(info.PluginId, info.PluginName, ProblemSeverity.Warning, ProblemCodes.WidgetCallFailed,
                $"A request of widget '{info.Widget.Manifest.Id}' took longer than {HandlerTimeout.TotalSeconds:0} seconds");
            await ReplyAsync(session, message, ok: false, error: "timeout");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _problems.Report(info.PluginId, info.PluginName, ProblemSeverity.Warning, ProblemCodes.WidgetCallFailed,
                $"A request of widget '{info.Widget.Manifest.Id}' failed: {ex.Message}");
            await ReplyAsync(session, message, ok: false, error: "failed", text: Short(ex.Message));
        }
    }

    private async Task RunAsync(ClientSession session, PluginWidgetRequestMessage message, Widget widget, PluginWidgetInfo info, IDeviceController device, CancellationToken ct)
    {
        var data = message.Data as JsonObject;
        var action = data?["action"] is JsonValue a && a.TryGetValue<string>(out var text) ? text : null;
        var settings = data?["settings"] as JsonObject ?? [];
        // Only the plugin's own actions, and a JavaScript plugin only when it was approved to register actions.
        var mayRun = action is not null && action.StartsWith(info.PluginId + ".", StringComparison.Ordinal) && _dispatcher.Has(action)
            && (info.Kind != PluginKind.Js || _permissions.IsGranted(info.PluginId, ["actions"]));
        if (!mayRun)
        {
            await ReplyAsync(session, message, ok: false, error: "not_allowed");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RunTimeout);
        var context = new ActionContext(session.DeviceId!, message.PageId, widget.Id, device) { UserGesture = message.UserGesture };
        var error = await _dispatcher.RunAsync(action!, (JsonObject)settings.DeepClone(), context, timeout.Token);
        if (error is null)
        {
            await ReplyAsync(session, message, ok: true);
            return;
        }
        _problems.Report(info.PluginId, info.PluginName, ProblemSeverity.Warning, ProblemCodes.WidgetCallFailed,
            $"Widget '{info.Widget.Manifest.Id}' ran '{action}' and it failed: {error}");
        await ReplyAsync(session, message, ok: false, error: "failed", text: Short(error));
    }

    private async Task SubscribeAsync(ClientSession session, Widget widget, PluginWidgetInfo info, JsonNode? data)
    {
        var requested = (data?["variables"] as JsonArray)?.Select(n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>() ?? [];
        var bound = PluginWidgetProps.BoundVariables(info, PluginWidgetProps.Settings(widget));
        var ownAllowed = info.Kind != PluginKind.Js || _permissions.IsGranted(info.PluginId, ["variables"]);
        var allowed = requested
            .Where(PluginWidgetProps.IsVariableName)
            .Where(name => bound.Contains(name) || (ownAllowed && name.StartsWith(info.PluginId + ".", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxSubscribedVariables)
            .ToList();

        var state = session.WidgetBridge;
        var initial = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var sent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in allowed)
        {
            if (_variables.Get(name) is not { } value) continue;
            var node = ToNode(value);
            initial[name] = node;
            sent[name] = node?.ToJsonString() ?? "null";
        }
        lock (state.Lock)
        {
            state.Subscriptions[widget.Id] = [.. allowed];
            state.Sent[widget.Id] = sent;
        }
        if (initial.Count > 0)
            await session.SendAsync(MessageTypes.PluginWidgetVars, new PluginWidgetVarsMessage(widget.Id, initial));
    }

    private async Task SendRetainedAsync(ClientSession session, Widget widget, PluginWidgetInfo info)
    {
        foreach (var e in _events.RetainedFor(info.PluginId, info.Widget.Manifest.Id, widget.Id, session.DeviceId!))
            await session.SendAsync(MessageTypes.PluginWidgetEvent, new PluginWidgetEventMessage(widget.Id, e.Name, e.Data));
    }

    private static Task ReplyAsync(ClientSession session, PluginWidgetRequestMessage message, bool ok, JsonNode? data = null, string? error = null, string? text = null) =>
        session.SendAsync(MessageTypes.PluginWidgetReply, new PluginWidgetReplyMessage(message.WidgetId, message.Id ?? 0, ok, data, error, text));

    private static string Short(string text) => text.Length > 200 ? text[..200] : text;

    // ---- what the server pushes ----

    private void OnVariableChanged(string name)
    {
        lock (_dirtyLock) _dirty.Add(name);
    }

    private async Task FlushVariablesAsync()
    {
        // One flush at a time: a slow send must not let a second tick overtake it and deliver an older value after a newer one.
        if (Interlocked.Exchange(ref _flushing, 1) == 1) return;
        try { await FlushCoreAsync(); }
        finally { Interlocked.Exchange(ref _flushing, 0); }
    }

    private async Task FlushCoreAsync()
    {
        HashSet<string> dirty;
        lock (_dirtyLock)
        {
            if (_dirty.Count == 0) return;
            dirty = [.. _dirty];
            _dirty.Clear();
        }

        foreach (var session in _sessions.All)
        {
            if (!session.IsIdentified || session.ProfileId is null || session.PageId is null) continue;
            var state = session.WidgetBridge;
            List<(string WidgetId, HashSet<string> Names)> subscribed;
            lock (state.Lock)
            {
                if (state.Subscriptions.Count == 0) continue;
                subscribed = [.. state.Subscriptions.Select(kv => (kv.Key, kv.Value))];
            }
            var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
            if (page is null) continue;

            foreach (var (widgetId, names) in subscribed)
            {
                if (!names.Overlaps(dirty) || page.FindWidget(widgetId) is null) continue; // only widgets on the shown page
                var changed = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                lock (state.Lock)
                {
                    if (!state.Sent.TryGetValue(widgetId, out var sent)) continue;
                    foreach (var name in names.Where(dirty.Contains))
                    {
                        var node = ToNode(_variables.Get(name));
                        var text = node?.ToJsonString() ?? "null";
                        if (sent.TryGetValue(name, out var before) && before == text) continue;
                        sent[name] = text;
                        changed[name] = node;
                    }
                }
                if (changed.Count == 0) continue;
                try { await session.SendAsync(MessageTypes.PluginWidgetVars, new PluginWidgetVarsMessage(widgetId, changed)); }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.WebSockets.WebSocketException)
                {
                    _logger.LogDebug(ex, "Could not push plugin.widget.vars to {Session}", session.Id);
                }
            }
        }
    }

    private void OnEvent(PluginWidgetEvent e) => _ = PushEventAsync(e);

    private async Task PushEventAsync(PluginWidgetEvent e)
    {
        foreach (var session in _sessions.All)
        {
            // One broken connection must not stop the push to the others.
            try
            {
                if (!session.IsIdentified || session.ProfileId is null || session.PageId is null || !session.Supports(ClientCapabilities.PluginWidgets)) continue;
                if (e.DeviceId is not null && e.DeviceId != session.DeviceId) continue;
                var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
                if (page is null) continue;
                foreach (var widget in page.Widgets)
                {
                    if (!PluginWidgetProps.TryRead(widget, out var pluginId, out var widgetId) || pluginId != e.PluginId || widgetId != e.Widget) continue;
                    if (e.WidgetId is not null && e.WidgetId != widget.Id) continue;
                    await session.SendAsync(MessageTypes.PluginWidgetEvent, new PluginWidgetEventMessage(widget.Id, e.Name, e.Data));
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.WebSockets.WebSocketException)
            {
                _logger.LogDebug(ex, "Could not push plugin.widget.event to {Session}", session.Id);
            }
        }
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        string s => JsonValue.Create(s),
        double d => double.IsFinite(d) ? JsonValue.Create(d) : null,
        float f => float.IsFinite(f) ? JsonValue.Create((double)f) : null,
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        _ => JsonValue.Create(value.ToString()),
    };
}
