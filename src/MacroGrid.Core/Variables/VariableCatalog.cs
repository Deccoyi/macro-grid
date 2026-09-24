using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>Aggregates every registered <see cref="IVariableCatalogSource"/> for the editor's variable picker.
/// Sources can come and go at runtime (a hot-loaded or unloaded plugin's variables).</summary>
public sealed class VariableCatalog(IEnumerable<IVariableCatalogSource> sources)
{
    private readonly Lock _lock = new();
    private List<IVariableCatalogSource> _sources = [.. sources];

    public IReadOnlyList<VariableInfo> All
    {
        get
        {
            List<IVariableCatalogSource> snapshot;
            lock (_lock) snapshot = _sources;
            return [.. snapshot.SelectMany(s => s.Describe()).OrderBy(v => v.Name)];
        }
    }

    public void Add(IVariableCatalogSource source)
    {
        lock (_lock) _sources = [.. _sources, source];
    }

    public void Remove(IVariableCatalogSource source)
    {
        lock (_lock) _sources = [.. _sources.Where(s => !ReferenceEquals(s, source))];
    }
}
