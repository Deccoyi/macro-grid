using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Widgets;

/// <summary>An event a plugin pushed to its own widgets. <see cref="WidgetId"/> and <see cref="DeviceId"/> narrow the target; null means all.</summary>
public sealed record PluginWidgetEvent(string PluginId, string Widget, string Name, JsonNode? Data, string? WidgetId, string? DeviceId, bool Retain);

/// <summary>
/// Where a plugin's <see cref="IPluginWidgets.Post"/> lands. The widget router subscribes and sends each event to the devices that show a matching widget.
/// An event nobody is showing is not queued, except the last one of each name that the plugin marked <c>retain</c>: a widget that comes on screen
/// later gets it right away. Size and rate are limited per plugin (the limits of docs/design/plugin-widgets.md).
/// </summary>
public sealed class PluginWidgetEventHub(ProblemList? problems = null, Func<DateTimeOffset>? clock = null)
{
    public const int MaxEventBytes = 64 * 1024;
    public const int MaxEventsPerSecond = 20;
    public const int MaxRetainedNames = 8;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (double Tokens, DateTimeOffset At)> _buckets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PluginWidgetEvent>> _retained = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warnedRate = new(StringComparer.Ordinal);

    /// <summary>Raised for every accepted event, outside the lock.</summary>
    public event Action<PluginWidgetEvent>? Posted;

    public void Post(string pluginName, PluginWidgetEvent widgetEvent)
    {
        var size = widgetEvent.Data?.ToJsonString().Length ?? 0;
        if (size > MaxEventBytes)
        {
            problems?.Report(widgetEvent.PluginId, pluginName, ProblemSeverity.Warning, ProblemCodes.WidgetEventDropped,
                $"An event '{widgetEvent.Name}' for widget '{widgetEvent.Widget}' was dropped: it is larger than {MaxEventBytes / 1024} KB");
            return;
        }

        lock (_lock)
        {
            if (!TakeToken(widgetEvent.PluginId))
            {
                if (_warnedRate.Add(widgetEvent.PluginId))
                    problems?.Report(widgetEvent.PluginId, pluginName, ProblemSeverity.Warning, ProblemCodes.WidgetEventDropped,
                        $"Events to widgets were dropped: more than {MaxEventsPerSecond} per second");
                return;
            }
            if (widgetEvent.Retain) Retain(widgetEvent);
        }
        Posted?.Invoke(widgetEvent);
    }

    /// <summary>The retained events (last one per name) that apply to one placed widget on one device.</summary>
    public IReadOnlyList<PluginWidgetEvent> RetainedFor(string pluginId, string widget, string widgetId, string deviceId)
    {
        lock (_lock)
            return _retained.TryGetValue(Key(pluginId, widget), out var list)
                ? [.. list.Where(e => (e.WidgetId is null || e.WidgetId == widgetId) && (e.DeviceId is null || e.DeviceId == deviceId))]
                : [];
    }

    /// <summary>Drops what a plugin retained (when it is unloaded), so a reload starts clean.</summary>
    public void Forget(string pluginId)
    {
        lock (_lock)
        {
            foreach (var key in _retained.Keys.Where(k => k.StartsWith(pluginId + "\n", StringComparison.Ordinal)).ToList()) _retained.Remove(key);
            _buckets.Remove(pluginId);
            _warnedRate.Remove(pluginId);
        }
    }

    private void Retain(PluginWidgetEvent e)
    {
        var key = Key(e.PluginId, e.Widget);
        if (!_retained.TryGetValue(key, out var list)) _retained[key] = list = [];
        // One value per name and target: a newer event replaces the older one for the same name and the same widget and device.
        list.RemoveAll(x => x.Name == e.Name && x.WidgetId == e.WidgetId && x.DeviceId == e.DeviceId);
        list.Add(e);
        while (list.Select(x => x.Name).Distinct().Count() > MaxRetainedNames) list.RemoveAt(0);
    }

    private bool TakeToken(string pluginId)
    {
        var now = _clock();
        var (tokens, at) = _buckets.TryGetValue(pluginId, out var b) ? b : (MaxEventsPerSecond, now);
        tokens = Math.Min(MaxEventsPerSecond, tokens + (now - at).TotalSeconds * MaxEventsPerSecond);
        if (tokens < 1) { _buckets[pluginId] = (tokens, now); return false; }
        _buckets[pluginId] = (tokens - 1, now);
        return true;
    }

    private static string Key(string pluginId, string widget) => pluginId + "\n" + widget;
}
