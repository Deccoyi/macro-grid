using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>Aggregates every registered <see cref="IVariableCatalogSource"/> for the editor's variable picker.
/// Sources can come and go at runtime (a hot-loaded or unloaded plugin's variables).</summary>
public sealed class VariableCatalog(IEnumerable<IVariableCatalogSource> sources)
{
    private readonly Lock _lock = new();
    private List<(IVariableCatalogSource Source, string? PluginId)> _sources = [.. sources.Select(s => (s, (string?)null))];

    public IReadOnlyList<VariableInfo> All => Collect(null);

    /// <summary>Like <see cref="All"/>, with each plugin's variable texts translated to the current language.</summary>
    public IReadOnlyList<VariableInfo> Localized(PluginLocalizer localizer) => Collect(localizer);

    private List<VariableInfo> Collect(PluginLocalizer? localizer)
    {
        List<(IVariableCatalogSource Source, string? PluginId)> snapshot;
        lock (_lock) snapshot = _sources;
        return [.. snapshot
            .SelectMany(s => s.Source.Describe().Select(v => localizer is null ? v : localizer.Localize(s.PluginId, v)))
            .OrderBy(v => v.Name)];
    }

    /// <param name="pluginId">The plugin the source belongs to (null for built-in sources); picks the translation table.</param>
    public void Add(IVariableCatalogSource source, string? pluginId = null)
    {
        lock (_lock) _sources = [.. _sources, (source, pluginId)];
    }

    public void Remove(IVariableCatalogSource source)
    {
        lock (_lock) _sources = [.. _sources.Where(s => !ReferenceEquals(s.Source, source))];
    }
}
