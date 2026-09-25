using System.Collections.Concurrent;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

public sealed record PluginStatusEntry(string PluginId, string Id, string Text, StatusLevel Level, string? Icon, string? Tooltip, DateTimeOffset UpdatedAt);

/// <summary>
/// Thread-safe collection of every status item shown in the editor's window-wide status bar — one entry
/// per (pluginId, id) pair, plus the core items (server version, connected-device count) this registry
/// itself owns and refreshes.
/// </summary>
public sealed class PluginStatusRegistry
{
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<(string PluginId, string Id), PluginStatusEntry> _items = new();
    // Items that disappear by themselves (a transient message such as a failed action); a plugin's own items never expire.
    private readonly ConcurrentDictionary<(string PluginId, string Id), DateTimeOffset> _expiry = new();

    public PluginStatusRegistry() : this(TimeProvider.System) { }

    internal PluginStatusRegistry(TimeProvider time) => _time = time;

    public IReadOnlyList<PluginStatusEntry> All
    {
        get
        {
            DropExpired();
            return _items.Values.OrderBy(i => i.PluginId).ThenBy(i => i.Id).ToList();
        }
    }

    internal void Set(string pluginId, string id, string text, StatusLevel level, string? icon, string? tooltip)
    {
        _expiry.TryRemove((pluginId, id), out _);
        _items[(pluginId, id)] = new PluginStatusEntry(pluginId, id, text, level, icon, tooltip, _time.GetUtcNow());
    }

    /// <summary>Drops every status item a plugin owns (it was unloaded).</summary>
    internal void RemovePlugin(string pluginId)
    {
        foreach (var key in _items.Keys.Where(k => k.PluginId == pluginId).ToList())
        {
            _items.TryRemove(key, out _);
            _expiry.TryRemove(key, out _);
        }
    }

    /// <summary>Sets a core item. With a <paramref name="lifetime"/> it removes itself after that time unless it is set again.</summary>
    public void SetCore(string id, string text, StatusLevel level, string? icon = null, string? tooltip = null, TimeSpan? lifetime = null)
    {
        Set("core", id, text, level, icon, tooltip);
        if (lifetime is { } span) _expiry[("core", id)] = _time.GetUtcNow() + span;
    }

    /// <summary>Removes a core item, for example a failure message once the next action succeeded.</summary>
    public void RemoveCore(string id)
    {
        _items.TryRemove(("core", id), out _);
        _expiry.TryRemove(("core", id), out _);
    }

    private void DropExpired()
    {
        var now = _time.GetUtcNow();
        foreach (var (key, at) in _expiry)
            if (at <= now && _expiry.TryRemove(key, out _))
                _items.TryRemove(key, out _);
    }
}

/// <summary>Handed to a plugin by <see cref="PluginHostCollector.CreateStatusItem"/>; writes into the
/// shared <see cref="PluginStatusRegistry"/> under this plugin's id.</summary>
internal sealed class PluginStatusItem(PluginStatusRegistry registry, string pluginId, string id) : IPluginStatusItem
{
    public void Update(string text, StatusLevel level, string? icon = null, string? tooltip = null) =>
        registry.Set(pluginId, id, text, level, icon, tooltip);
}
