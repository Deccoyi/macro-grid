using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Variables;

/// <summary>Aggregates every registered <see cref="IVariableCatalogSource"/> for the editor's variable picker.</summary>
public sealed class VariableCatalog(IEnumerable<IVariableCatalogSource> sources)
{
    public IReadOnlyList<VariableInfo> All => [.. sources.SelectMany(s => s.Describe()).OrderBy(v => v.Name)];
}
