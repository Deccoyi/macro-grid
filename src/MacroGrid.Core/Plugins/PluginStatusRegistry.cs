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
    private readonly ConcurrentDictionary<(string PluginId, string Id), PluginStatusEntry> _items = new();

    public IReadOnlyList<PluginStatusEntry> All => _items.Values.OrderBy(i => i.PluginId).ThenBy(i => i.Id).ToList();

    internal void Set(string pluginId, string id, string text, StatusLevel level, string? icon, string? tooltip) =>
        _items[(pluginId, id)] = new PluginStatusEntry(pluginId, id, text, level, icon, tooltip, DateTimeOffset.UtcNow);

    /// <summary>Drops every status item a plugin owns (it was unloaded).</summary>
    internal void RemovePlugin(string pluginId)
    {
        foreach (var key in _items.Keys.Where(k => k.PluginId == pluginId).ToList())
            _items.TryRemove(key, out _);
    }

    public void SetCore(string id, string text, StatusLevel level, string? icon = null, string? tooltip = null) =>
        Set("core", id, text, level, icon, tooltip);
}

/// <summary>Handed to a plugin by <see cref="PluginHostCollector.CreateStatusItem"/>; writes into the
/// shared <see cref="PluginStatusRegistry"/> under this plugin's id.</summary>
internal sealed class PluginStatusItem(PluginStatusRegistry registry, string pluginId, string id) : IPluginStatusItem
{
    public void Update(string text, StatusLevel level, string? icon = null, string? tooltip = null) =>
        registry.Set(pluginId, id, text, level, icon, tooltip);
}
