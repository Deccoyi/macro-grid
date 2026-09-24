namespace MacroGrid.Plugin.Abstractions;

/// <summary>
/// One variable a provider makes available, described for the editor's variable picker.
/// <paramref name="Category"/> groups it in that picker (e.g. "Sistem"); a plugin can introduce its own
/// category name freely — the editor just lists whatever distinct category strings show up.
/// </summary>
public sealed record VariableInfo(string Name, string Description, string Example, string Category);

/// <summary>
/// Optional companion to <see cref="IVariableProvider"/>: lets the editor list "what variables can I
/// use in a widget's text" without having to guess from currently-live values (which may be empty
/// or zero at edit time). A provider implements both interfaces from the same instance.
/// </summary>
public interface IVariableCatalogSource
{
    IEnumerable<VariableInfo> Describe();
}
