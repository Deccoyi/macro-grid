using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MacroGrid.Core.Sessions;

namespace MacroGrid.Core.Widgets;

/// <summary>
/// Pushes template-driven widget text and toggle state to clients.
/// Live updates are batched at 100ms (max ~10Hz) and only sent when the rendered text actually changed for that client.
/// </summary>
public sealed class WidgetStateService : IHostedService, IDisposable
{
    /// <summary>Widget property paths the editor can dynamize, mapped to the key sent to clients in WidgetStateMessage.Style.</summary>
    private static readonly Dictionary<string, string> DynamizableProperties = new()
    {
        ["style.background"] = "background",
        ["style.foreground"] = "foreground",
        ["style.borderColor"] = "borderColor",
        ["style.animation"] = "animation",
        ["style.icon"] = "icon",
    };

    private readonly VariableStore _variables;
    private readonly SessionRegistry _sessions;
    private readonly ProfileStore _profiles;
    private readonly ToggleStateStore _toggles;
    private readonly LayoutSender _layouts;
    private readonly WebViewState _webViews;
    private readonly ILogger<WidgetStateService> _logger;
    private readonly HashSet<string> _dirtyVariables = [];
    private readonly HashSet<(string? SessionId, string WidgetId)> _dirtyWidgets = [];
    private readonly LastResultStore? _results;
    private readonly Lock _dirtyLock = new();
    private Timer? _timer;
    private int _flushing;

    /// <summary>A client that does not take a frame for this long is dropped, so it cannot hold up the others; a phone reconnects by itself.</summary>
    internal TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a button's actions must run before <c>self.busy</c> turns on, so a quick action never flashes.</summary>
    internal TimeSpan BusyDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>How long <c>self.pressed</c> stays on at least, so a quick tap still draws its look.</summary>
    internal TimeSpan PressedMinimum { get; set; } = TimeSpan.FromMilliseconds(150);

    public WidgetStateService(VariableStore variables, SessionRegistry sessions, ProfileStore profiles, ToggleStateStore toggles, LayoutSender layouts, WebViewState webViews, ILogger<WidgetStateService> logger,
        PluginWidgetCatalog? pluginWidgets = null, LastResultStore? results = null)
    {
        _results = results;
        _variables = variables;
        _sessions = sessions;
        _profiles = profiles;
        _toggles = toggles;
        _layouts = layouts;
        _webViews = webViews;
        _logger = logger;
        _variables.Changed += OnVariableChanged;
        _pluginWidgets = pluginWidgets;
        if (pluginWidgets is not null) pluginWidgets.PluginChanged += OnPluginWidgetsChanged;
    }

    private readonly PluginWidgetCatalog? _pluginWidgets;

    /// <summary>A plugin's widgets changed (approved, reloaded, switched off, removed): devices showing a profile with plugin widgets get their layout again, so a
    /// widget that could not run starts, or one that stopped draws its placeholder.</summary>
    private void OnPluginWidgetsChanged(string pluginId) => _ = RefreshPluginWidgetLayoutsAsync(pluginId);

    private async Task RefreshPluginWidgetLayoutsAsync(string pluginId)
    {
        try
        {
            foreach (var profileId in _sessions.All.Select(s => s.ProfileId).Where(id => id is not null).Distinct().ToList())
            {
                var profile = _profiles.Get(profileId!);
                if (profile is not null && profile.Pages.SelectMany(p => p.Widgets).Any(w => PluginWidgetProps.TryRead(w, out var owner, out _) && owner == pluginId))
                    await BroadcastProfileAsync(profile);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.WebSockets.WebSocketException)
        {
            _logger.LogDebug(ex, "Could not refresh layouts after plugin widgets changed");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => _ = FlushOnceAsync(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
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
        if (_pluginWidgets is not null) _pluginWidgets.PluginChanged -= OnPluginWidgetsChanged;
    }

    /// <summary>Sends a profile's full layout to one client (see <see cref="LayoutSender"/>).</summary>
    public Task SendLayoutAsync(ClientSession session, Profile profile, string pageId, CancellationToken ct = default) =>
        _layouts.SendFullAsync(session, profile, pageId, ct);

    /// <summary>Answers a client's <c>asset.get</c>.</summary>
    public Task SendAssetsAsync(ClientSession session, IEnumerable<string> hashes, CancellationToken ct = default) =>
        _layouts.SendAssetsAsync(session, hashes, ct);

    /// <summary>
    /// Brings every connected client currently showing this profile up to date after an editor "Save", so
    /// devices see the edit live instead of having to reconnect. A client that supports it gets a small
    /// <c>layout.patch</c> with just the changed widgets, any other client the full layout; either way the usual
    /// initial text/toggle/dynamic-style state follows. A client on a page that no longer exists (deleted while
    /// editing) falls back to the profile's first page, same as a fresh <c>hello</c> would.
    /// </summary>
    public async Task BroadcastProfileAsync(Profile profile, CancellationToken ct = default)
    {
        foreach (var session in _sessions.All)
        {
            if (session.ProfileId != profile.Id) continue;

            var page = profile.FindPage(session.PageId ?? "") ?? profile.Pages.FirstOrDefault();
            if (page is null) continue;

            session.PageId = page.Id;

            var result = await _layouts.SendUpdateAsync(session, profile, page.Id, ct);
            if (result.Kind == LayoutSendKind.Unchanged) continue;

            if (result.Kind == LayoutSendKind.Full)
            {
                session.SentTexts.Clear();
                session.SentStyles.Clear();
                session.SentValues.Clear();
            }
            else
            {
                foreach (var id in result.ChangedWidgetIds)
                {
                    session.SentTexts.TryRemove(id, out _);
                    session.SentStyles.TryRemove(id, out _);
                    session.SentValues.TryRemove(id, out _);
                }
            }

            // A saved profile starts the web widgets from its own addresses again; tell the device to drop what a button had set.
            if (session.DeviceId is not null)
                foreach (var widgetId in _webViews.Clear(session.DeviceId))
                    await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widgetId, Url: ""), ct);

            await SendInitialAsync(session, page, ct);
        }
    }

    /// <summary>Forgets the addresses a device's buttons set in its web widgets (the device switched profile; its new layout replaces the widgets anyway).</summary>
    public void ForgetWebViews(ClientSession session)
    {
        if (session.DeviceId is not null)
            _webViews.Clear(session.DeviceId);
    }

    /// <summary>Sends the current rendered text and toggle state of every relevant widget on a page, e.g. right after a layout is shown.</summary>
    public async Task SendInitialAsync(ClientSession session, Page page, CancellationToken ct)
    {
        foreach (var widget in page.Widgets)
        {
            var scope = ScopeFor(session, widget);
            var text = DynamicText.Resolve(widget, scope);
            if (text is not null)
            {
                session.SentTexts[widget.Id] = text;
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text), ct);
            }

            if (widget.Type == WidgetTypes.Web && session.DeviceId is not null)
            {
                var url = _webViews.GetUrl(session.DeviceId, widget.Id);
                var reload = _webViews.GetReload(session.DeviceId, widget.Id);
                if (url is not null || reload > 0)
                    await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Url: url, Reload: reload > 0 ? reload : null), ct);
            }

            if (widget.Type == WidgetTypes.Toggle)
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Active: _toggles.Get(widget.Id)), ct);

            var value = ResolveBoundValue(widget);
            if (value is not null)
            {
                session.SentValues[widget.Id] = value.Value;
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Value: value), ct);
            }

            var style = ResolveDynamicStyle(widget, scope);
            if (style is not null)
            {
                session.SentStyles[widget.Id] = style;
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Style: _layouts.ForClient(session, style)), ct);
            }
            else if (session.SentStyles.TryGetValue(widget.Id, out var previous) && previous.Count > 0)
            {
                // Nothing matches now but the device still holds an old style (the page was off screen while the variable changed): the client
                // merges state, so an empty style is what takes it back to the widget's own.
                session.SentStyles[widget.Id] = [];
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Style: []), ct);
            }
        }
    }

    /// <summary>A slider/knob's live position, if its <c>props.valueVariable</c> (set in the Inspector, e.g.
    /// "system.audio.master") names a variable that currently holds a number. Buttons/labels/etc. and a
    /// slider/knob with no binding (its position is purely local until the user drags it) both return null —
    /// there is nothing to push from the server's side in that case.</summary>
    private double? ResolveBoundValue(Widget widget)
    {
        if (widget.Type is not (WidgetTypes.Slider or WidgetTypes.Knob)) return null;
        var variableName = widget.Props?["valueVariable"]?.GetValue<string>();
        if (string.IsNullOrEmpty(variableName)) return null;

        return _variables.Get(variableName) switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => null,
        };
    }

    /// <returns>Only the properties whose binding currently produced a value (matched a case, or hit the binding's default); null if the widget has no dynamized properties at all.</returns>
    private Dictionary<string, string>? ResolveDynamicStyle(Widget widget, IVariableStore variables)
    {
        if (widget.Dynamic.Count == 0) return null;

        Dictionary<string, string>? resolved = null;
        foreach (var (propertyPath, binding) in widget.Dynamic)
        {
            if (!DynamizableProperties.TryGetValue(propertyPath, out var styleKey)) continue;
            var value = DynamicRuleEvaluator.Evaluate(binding, variables);
            if (value is null) continue; // no case matched and no default: leave the widget's own static value alone
            resolved ??= [];
            resolved[styleKey] = value;
        }
        return resolved;
    }

    private static bool BindingsDependOn(IEnumerable<DynamicBinding> bindings, HashSet<string> dirtyVariables) =>
        bindings.SelectMany(b => b.Cases).SelectMany(c => DynamicRuleEvaluator.CollectVariables(c.Condition)).Any(dirtyVariables.Contains);

    private static bool StyleChanged(Dictionary<string, string>? previous, Dictionary<string, string> next)
    {
        if (previous is null || previous.Count != next.Count) return true;
        foreach (var (key, value) in next)
        {
            if (!previous.TryGetValue(key, out var prevValue) || prevValue != value) return true;
        }
        return false;
    }

    /// <summary>The store a widget is evaluated against for one device: the shared one plus the widget's own <c>self.*</c> state.</summary>
    private SelfVariableScope ScopeFor(ClientSession session, Widget widget) => new(_variables, session, widget, _toggles, _results);

    /// <summary>Tells the flush that one widget's own state (<c>self.*</c>) changed, so it is evaluated again at once. With no session, every device
    /// showing it is updated; with one, only that device.</summary>
    public void Refresh(string widgetId, ClientSession? session = null)
    {
        lock (_dirtyLock) _dirtyWidgets.Add((session?.Id, widgetId));
        _ = FlushOnceAsync();
    }

    /// <summary>Runs a button's action list. If it takes longer than <see cref="BusyDelay"/>, <c>self.busy</c> is on for that device until it ends.</summary>
    public async Task<T> RunBusyAsync<T>(ClientSession session, string widgetId, Func<Task<T>> work)
    {
        var task = work();
        var marked = false;
        try
        {
            if (await Task.WhenAny(task, Task.Delay(BusyDelay)) != task)
            {
                marked = true;
                session.Busy[widgetId] = true;
                Refresh(widgetId, session);
            }
            return await task;
        }
        finally
        {
            if (marked)
            {
                session.Busy.TryRemove(widgetId, out _);
                Refresh(widgetId, session);
            }
        }
    }

    /// <summary>A finger went down on a button: <c>self.pressed</c> is on for that device.</summary>
    public void PressBegan(ClientSession session, string widgetId)
    {
        session.Pressed[widgetId] = DateTime.UtcNow;
        Refresh(widgetId, session);
    }

    /// <summary>The finger lifted. The look is cleared at once after a long hold, or when <see cref="PressedMinimum"/> has passed after a quick tap. A newer press
    /// in between is not cleared by this release.</summary>
    public void PressEnded(ClientSession session, string widgetId)
    {
        if (!session.Pressed.TryGetValue(widgetId, out var started)) return;
        var remaining = PressedMinimum - (DateTime.UtcNow - started);
        if (remaining <= TimeSpan.Zero)
        {
            if (session.Pressed.TryRemove(new KeyValuePair<string, DateTime>(widgetId, started))) Refresh(widgetId, session);
            return;
        }
        _ = Task.Delay(remaining).ContinueWith(_ =>
        {
            if (session.Pressed.TryRemove(new KeyValuePair<string, DateTime>(widgetId, started))) Refresh(widgetId, session);
        }, TaskScheduler.Default);
    }

    private void OnVariableChanged(string name)
    {
        lock (_dirtyLock) _dirtyVariables.Add(name);
    }

    /// <summary>One flush at a time: a flush that is still sending (a slow client) is not overlapped by the next tick, the variables
    /// that changed meanwhile simply wait in the dirty set and go out together in the next flush.</summary>
    private async Task FlushOnceAsync()
    {
        if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return;
        try
        {
            await FlushAsync();
        }
        finally
        {
            Volatile.Write(ref _flushing, 0);
        }
    }

    internal async Task FlushAsync()
    {
        HashSet<string> dirty;
        HashSet<(string? SessionId, string WidgetId)> marks;
        lock (_dirtyLock)
        {
            if (_dirtyVariables.Count == 0 && _dirtyWidgets.Count == 0) return;
            dirty = [.. _dirtyVariables];
            marks = [.. _dirtyWidgets];
            _dirtyVariables.Clear();
            _dirtyWidgets.Clear();
        }

        // Each client is served on its own, so one that is slow to take frames does not delay the rest.
        var sessions = _sessions.All;
        if (sessions.Count == 1)
            await FlushSessionAsync(sessions.First(), dirty, marks);
        else
            await Task.WhenAll(sessions.Select(s => FlushSessionAsync(s, dirty, marks)));
    }

    private async Task FlushSessionAsync(ClientSession session, HashSet<string> dirty, HashSet<(string? SessionId, string WidgetId)> marks)
    {
        if (!session.IsIdentified || session.ProfileId is null || session.PageId is null) return;
        var marked = marks.Where(m => m.SessionId is null || m.SessionId == session.Id).Select(m => m.WidgetId).ToHashSet();
        if (dirty.Count == 0 && marked.Count == 0) return;
        var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
        if (page is null) return;

        foreach (var widget in page.Widgets)
        {
            var forced = marked.Contains(widget.Id);
            var scope = ScopeFor(session, widget);
            string? text = null;
            if ((forced || DynamicText.Dependencies(widget).Any(dirty.Contains)) && DynamicText.Resolve(widget, scope) is { } rendered
                && (!session.SentTexts.TryGetValue(widget.Id, out var prevText) || prevText != rendered))
                text = rendered;

            Dictionary<string, string>? style = null;
            if (widget.Dynamic.Count > 0 && (forced || BindingsDependOn(widget.Dynamic.Values, dirty)))
            {
                var resolved = ResolveDynamicStyle(widget, scope);
                var previous = session.SentStyles.GetValueOrDefault(widget.Id);
                if (resolved is not null && StyleChanged(previous, resolved))
                    style = resolved;
                else if (resolved is null && previous is { Count: > 0 })
                    style = []; // no rule matches any more and there is no default: the client merges state, so tell it to drop the old style
            }

            double? value = null;
            var variableName = widget.Props?["valueVariable"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(variableName) && dirty.Contains(variableName))
            {
                var resolved = ResolveBoundValue(widget);
                if (resolved is not null && (!session.SentValues.TryGetValue(widget.Id, out var prevValue) || prevValue != resolved))
                    value = resolved;
            }

            if (text is null && style is null && value is null) continue;

            if (text is not null) session.SentTexts[widget.Id] = text;
            if (style is not null) session.SentStyles[widget.Id] = style;
            if (value is not null) session.SentValues[widget.Id] = value.Value;

            try
            {
                using var timeout = new CancellationTokenSource(SendTimeout);
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text, Value: value, Style: style is null ? null : _layouts.ForClient(session, style)), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // The client stopped taking frames; closing its socket ends its session, and it reconnects with fresh state.
                _logger.LogWarning("Client {Session} did not accept a widget.state in {Seconds}s; dropping the connection", session.Id, SendTimeout.TotalSeconds);
                session.Socket.Abort();
                return;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Client disconnected between the snapshot above and the send; ClientHub will clean up the session.
                _logger.LogDebug(ex, "Could not push widget.state to {Session}", session.Id);
            }
        }
    }
}
