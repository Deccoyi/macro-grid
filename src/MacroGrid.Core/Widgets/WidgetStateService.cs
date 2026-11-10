using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins.Images;
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
public sealed class WidgetStateService : IHostedService, IDisposable, IVariableDemandSource
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

    /// <summary>Joins the unavailable names of a widget into the string kept per session to spot a change.</summary>
    private const char UnavailableSeparator = '|';

    private readonly VariableStore _variables;
    private readonly SessionRegistry _sessions;
    private readonly ProfileStore _profiles;
    private readonly ToggleStateStore _toggles;
    private readonly LayoutSender _layouts;
    private readonly ILogger<WidgetStateService> _logger;
    private readonly HashSet<string> _dirtyVariables = [];
    private readonly HashSet<(string? SessionId, string WidgetId)> _dirtyWidgets = [];
    private readonly LastResultStore? _results;
    private readonly Lock _dirtyLock = new();
    private CoalescingTimer? _timer;
    private int _flushing;

    /// <summary>A client that does not take a frame for this long is dropped, so it cannot hold up the others; a phone reconnects by itself.</summary>
    internal TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long a button's actions must run before <c>self.busy</c> turns on, so a quick action never flashes.</summary>
    internal TimeSpan BusyDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>How long <c>self.pressed</c> stays on at least, so a quick tap still draws its look.</summary>
    internal TimeSpan PressedMinimum { get; set; } = TimeSpan.FromMilliseconds(150);

    private readonly UserVariableService? _userVariables;

    public WidgetStateService(VariableStore variables, SessionRegistry sessions, ProfileStore profiles, ToggleStateStore toggles, LayoutSender layouts, ILogger<WidgetStateService> logger,
        PluginWidgetCatalog? pluginWidgets = null, LastResultStore? results = null, ActionDispatcher? actions = null, UserVariableService? userVariables = null,
        PluginImageService? images = null)
    {
        _images = images;
        if (images is not null) images.ImagesChanged += OnImagesChanged;
        _actions = actions;
        _userVariables = userVariables;
        if (actions is not null) actions.HandlersChanged += OnActionTypesChanged;
        _results = results;
        _variables = variables;
        _sessions = sessions;
        _profiles = profiles;
        _toggles = toggles;
        _layouts = layouts;
        _logger = logger;
        _variables.Changed += OnVariableChanged;
        _variables.Demand.AddSource(this);
        _pluginWidgets = pluginWidgets;
        if (pluginWidgets is not null) pluginWidgets.PluginChanged += OnPluginWidgetsChanged;
    }

    private readonly PluginWidgetCatalog? _pluginWidgets;
    private readonly PluginImageService? _images;
    private readonly ActionDispatcher? _actions;

    /// <summary>An action type appeared or went away (a plugin switched on or off): every widget with actions on a shown page is looked at again for its unavailable mark.</summary>
    private void OnActionTypesChanged()
    {
        Interlocked.Increment(ref _handlerGeneration); // action dependencies depend on the handler list
        _variables.Demand.NotifyChanged();
        foreach (var session in _sessions.All)
        {
            if (session.ProfileId is null || session.PageId is null) continue;
            var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
            if (page is null) continue;
            foreach (var widget in page.Widgets.Where(w => w.Actions.Count > 0)) Refresh(widget.Id, session);
        }
    }

    /// <summary>What the widget's action steps need and cannot get now. A variable with no value used only in the text or look is not here: the
    /// text shows "n/a" and the press runs.</summary>
    private IReadOnlyList<string> UnavailableFor(Widget widget, IVariableStore scope, Profile? profile) =>
        _actions?.UnavailableSources(widget, scope, profile, id => _profiles.Get(id) is not null, _userVariables is null ? null : name => _userVariables.Find(name) is not null) ?? [];

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
        // Armed by the first change after a flush (see OnVariableChanged), not ticking while nothing changes.
        _timer = new CoalescingTimer(TimeSpan.FromMilliseconds(100), () => _ = FlushOnceAsync());
        if (HasPending()) _timer.Request();
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
        _timer?.Dispose();
        if (_pluginWidgets is not null) _pluginWidgets.PluginChanged -= OnPluginWidgetsChanged;
        if (_images is not null) _images.ImagesChanged -= OnImagesChanged;
    }

    /// <summary>Pictures of a plugin arrived (or its folder became known): every widget on a page a device is showing whose icon is, or can become, one of them is evaluated again
    /// for that device. Nothing is remembered between the layout and now: the profile is read as it is at this moment, so an edit or a page change cannot leave a stale entry.
    /// A page that is not shown gets its pictures when it is shown (<see cref="SendInitialAsync"/>).</summary>
    private void OnImagesChanged(string pluginId, IReadOnlyCollection<string>? imageIds)
    {
        foreach (var session in _sessions.All)
        {
            if (session.ProfileId is null || session.PageId is null) continue;
            var page = _profiles.Get(session.ProfileId)?.FindPage(session.PageId);
            if (page is null) continue;
            foreach (var widget in page.Widgets.Where(w => PluginImageRef.Of(w).Any(r => r.PluginId == pluginId && (imageIds is null || imageIds.Contains(r.ImageId)))))
                Refresh(widget.Id, session);
        }
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
        _variables.Demand.NotifyChanged(); // a save can add widgets that read more variables
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
                session.SentUnavailable.Clear();
            }
            else
            {
                foreach (var id in result.ChangedWidgetIds)
                {
                    session.SentTexts.TryRemove(id, out _);
                    session.SentStyles.TryRemove(id, out _);
                    session.SentValues.TryRemove(id, out _);
                    session.SentUnavailable.TryRemove(id, out _);
                }
            }

            await SendInitialAsync(session, page, ct);
        }
    }

    /// <summary>Sends the current rendered text and toggle state of every relevant widget on a page, e.g. right after a layout is shown.</summary>
    public async Task SendInitialAsync(ClientSession session, Page page, CancellationToken ct)
    {
        _variables.Demand.NotifyChanged(); // a page is shown: plugins refresh what they skipped
        var profile = session.ProfileId is null ? null : _profiles.Get(session.ProfileId);
        foreach (var widget in page.Widgets)
        {
            // A removed web widget reaches the device as a plain label with fixed text: no live state may overwrite it.
            if (widget.Type == WidgetTypes.Web) continue;
            var scope = ScopeFor(session, widget);
            // One frame per widget: the client merges state per field, so null fields leave the old value alone.
            var text = DynamicText.Resolve(widget, scope);
            if (text is not null) session.SentTexts[widget.Id] = text;

            bool? active = widget.Type == WidgetTypes.Toggle ? _toggles.Get(widget.Id) : null;

            var value = ResolveBoundValue(widget);
            if (value is not null) session.SentValues[widget.Id] = value.Value;

            var unavailable = UnavailableFor(widget, scope, profile);
            List<string>? unavailableToSend = null;
            if (unavailable.Count > 0 || session.SentUnavailable.GetValueOrDefault(widget.Id, "").Length > 0)
            {
                session.SentUnavailable[widget.Id] = string.Join(UnavailableSeparator, unavailable);
                unavailableToSend = [.. unavailable];
            }

            Dictionary<string, string>? styleToSend = null;
            var style = ResolveDynamicStyle(widget, scope);
            if (style is not null)
            {
                session.SentStyles[widget.Id] = style;
                styleToSend = _layouts.ForClient(session, style);
            }
            else if (session.SentStyles.TryGetValue(widget.Id, out var previous) && previous.Count > 0)
            {
                // Nothing matches now but the device still holds an old style (the page was off screen while the variable changed): the client
                // merges state, so an empty style (not null) is what takes it back to the widget's own.
                session.SentStyles[widget.Id] = [];
                styleToSend = [];
            }

            if (text is null && active is null && value is null && unavailableToSend is null && styleToSend is null) continue;
            await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text, Value: value, Active: active, Style: styleToSend, Unavailable: unavailableToSend), ct);
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

    /// <returns>Only the properties whose binding currently produced a value (matched a case, or hit the binding's default); null if the widget has no dynamized properties at all.
    /// An icon that is a plugin picture reference is replaced by the cached picture, or left out while it is missing (the widget then shows its own icon, or its text). A widget
    /// whose own icon is such a reference gets that picture here too, since the layout carried it only if it was cached when the layout was sent.</returns>
    private Dictionary<string, string>? ResolveDynamicStyle(Widget widget, IVariableStore variables)
    {
        var ownIcon = widget.Style?.Icon;
        var ownReference = PluginImageRef.LooksLikeRef(ownIcon);
        if (widget.Dynamic.Count == 0 && !ownReference) return null;

        Dictionary<string, string>? resolved = null;
        foreach (var (propertyPath, binding) in widget.Dynamic)
        {
            if (!DynamizableProperties.TryGetValue(propertyPath, out var styleKey)) continue;
            var value = DynamicRuleEvaluator.Evaluate(binding, variables);
            if (value is null) continue; // no case matched and no default: leave the widget's own static value alone
            resolved ??= [];
            if (styleKey == "icon" && PluginImageRef.LooksLikeRef(value))
            {
                if (_images?.Resolve(value) is not { } picture) continue;
                value = picture;
            }
            resolved[styleKey] = value;
        }
        if (ownReference && (resolved is null || !resolved.ContainsKey("icon")) && _images?.Resolve(ownIcon!) is { } own)
        {
            resolved ??= [];
            resolved["icon"] = own;
        }
        return resolved;
    }

    private static HashSet<string> StyleDependenciesOf(IEnumerable<DynamicBinding> bindings) =>
        [.. bindings.SelectMany(b => b.Cases).SelectMany(c => DynamicRuleEvaluator.CollectVariables(c.Condition))];

    /// <summary>What one widget of a page reads, worked out once. It must stay the same set the flush used to rebuild on every pass.</summary>
    private sealed class WidgetEntry(Widget widget, HashSet<string> text, HashSet<string> style, string? valueVariable, HashSet<string> actions)
    {
        public Widget Widget { get; } = widget;
        public HashSet<string> TextDependencies { get; } = text;
        public HashSet<string> StyleDependencies { get; } = style;
        public string? ValueVariable { get; } = valueVariable;
        public HashSet<string> ActionDependencies { get; } = actions;
    }

    /// <summary>Reverse index of one page: variable name to the widgets that read it. Built for one <see cref="Page"/> object (a save puts new objects in the store, so
    /// an edit drops the index with the old page) and for one generation of action handlers (<see cref="_handlerGeneration"/>).</summary>
    private sealed class PageIndex(List<WidgetEntry> entries, int generation)
    {
        public int Generation { get; } = generation;
        private readonly Dictionary<string, List<int>> _byVariable = Build(entries);
        private readonly Dictionary<string, List<int>> _byWidgetId = entries.Select((e, i) => (e.Widget.Id, i)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.Select(x => x.i).ToList());

        private static Dictionary<string, List<int>> Build(List<WidgetEntry> entries)
        {
            var map = new Dictionary<string, List<int>>();
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                IEnumerable<string> names = e.TextDependencies.Concat(e.StyleDependencies).Concat(e.ActionDependencies);
                if (!string.IsNullOrEmpty(e.ValueVariable)) names = names.Append(e.ValueVariable);
                foreach (var name in names.Distinct())
                {
                    if (!map.TryGetValue(name, out var list)) map[name] = list = [];
                    list.Add(i);
                }
            }
            return map;
        }

        /// <summary>The widgets touched by the dirty variables or marked for a forced look, in page order.</summary>
        public bool Reads(string name) => _byVariable.ContainsKey(name);

        public bool ReadsPrefix(string prefix) => _byVariable.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));

        public IEnumerable<WidgetEntry> Affected(HashSet<string> dirty, HashSet<string> marked)
        {
            var hit = new SortedSet<int>();
            foreach (var name in dirty)
                if (_byVariable.TryGetValue(name, out var list)) hit.UnionWith(list);
            foreach (var id in marked)
                if (_byWidgetId.TryGetValue(id, out var list)) hit.UnionWith(list);
            return hit.Select(i => entries[i]);
        }
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, PageIndex> _indexes = new();
    private int _handlerGeneration;

    /// <summary>The variable is read by a widget on a page a device shows now (text, look, value variable or action templates).</summary>
    public bool Observes(string name) => ShownIndexes().Any(i => i.Reads(name));

    public bool ObservesPrefix(string prefix) => ShownIndexes().Any(i => i.ReadsPrefix(prefix));

    private IEnumerable<PageIndex> ShownIndexes()
    {
        foreach (var session in _sessions.All)
        {
            if (session.ProfileId is null || session.PageId is null) continue;
            if (_profiles.Get(session.ProfileId)?.FindPage(session.PageId) is { } page) yield return IndexFor(page);
        }
    }

    private PageIndex IndexFor(Page page)
    {
        var generation = Volatile.Read(ref _handlerGeneration);
        if (_indexes.TryGetValue(page, out var index) && index.Generation == generation) return index;
        var entries = new List<WidgetEntry>();
        foreach (var widget in page.Widgets)
        {
            if (widget.Type == WidgetTypes.Web) continue; // see SendInitialAsync
            entries.Add(new WidgetEntry(widget, DynamicText.Dependencies(widget), StyleDependenciesOf(widget.Dynamic.Values),
                widget.Props?["valueVariable"]?.GetValue<string>(), _actions is null ? [] : _actions.ActionDependencies(widget)));
        }
        index = new PageIndex(entries, generation);
        _indexes.AddOrUpdate(page, index);
        return index;
    }

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
        _timer?.Request();
    }

    private bool HasPending()
    {
        lock (_dirtyLock) return _dirtyVariables.Count > 0 || _dirtyWidgets.Count > 0;
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
        // A change that arrived while this flush ran (its timer run was skipped by the guard above) must not wait for the next unrelated change.
        if (HasPending()) _timer?.Request();
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
        var profile = _profiles.Get(session.ProfileId);
        var page = profile?.FindPage(session.PageId);
        if (page is null) return;

        var index = IndexFor(page);
        foreach (var entry in index.Affected(dirty, marked))
        {
            var widget = entry.Widget;
            var forced = marked.Contains(widget.Id);
            var scope = ScopeFor(session, widget);
            string? text = null;
            if ((forced || entry.TextDependencies.Any(dirty.Contains)) && DynamicText.Resolve(widget, scope) is { } rendered
                && (!session.SentTexts.TryGetValue(widget.Id, out var prevText) || prevText != rendered))
                text = rendered;

            Dictionary<string, string>? style = null;
            if ((widget.Dynamic.Count > 0 || PluginImageRef.LooksLikeRef(widget.Style?.Icon)) && (forced || entry.StyleDependencies.Any(dirty.Contains)))
            {
                var resolved = ResolveDynamicStyle(widget, scope);
                var previous = session.SentStyles.GetValueOrDefault(widget.Id);
                if (resolved is not null && StyleChanged(previous, resolved))
                    style = resolved;
                else if (resolved is null && previous is { Count: > 0 })
                    style = []; // no rule matches any more and there is no default: the client merges state, so tell it to drop the old style
            }

            double? value = null;
            var variableName = entry.ValueVariable;
            if (!string.IsNullOrEmpty(variableName) && dirty.Contains(variableName))
            {
                var resolved = ResolveBoundValue(widget);
                if (resolved is not null && (!session.SentValues.TryGetValue(widget.Id, out var prevValue) || prevValue != resolved))
                    value = resolved;
            }

            List<string>? unavailable = null;
            if (forced || entry.ActionDependencies.Any(dirty.Contains))
            {
                var now = UnavailableFor(widget, scope, profile);
                if (string.Join(UnavailableSeparator, now) !=session.SentUnavailable.GetValueOrDefault(widget.Id, ""))
                    unavailable = [.. now];
            }

            if (text is null && style is null && value is null && unavailable is null) continue;

            if (text is not null) session.SentTexts[widget.Id] = text;
            if (style is not null) session.SentStyles[widget.Id] = style;
            if (value is not null) session.SentValues[widget.Id] = value.Value;
            if (unavailable is not null) session.SentUnavailable[widget.Id] = string.Join(UnavailableSeparator, unavailable);

            try
            {
                using var timeout = new CancellationTokenSource(SendTimeout);
                await session.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widget.Id, Text: text, Value: value, Style: style is null ? null : _layouts.ForClient(session, style), Unavailable: unavailable), timeout.Token);
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
