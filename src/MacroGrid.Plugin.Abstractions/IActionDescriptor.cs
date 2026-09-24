namespace MacroGrid.Plugin.Abstractions;

/// <summary>Optional side interface an <see cref="IActionHandler"/> can implement to get a category badge,
/// a description, an icon, and a schema-driven settings form in the editor's action picker. A handler
/// without this keeps its own hand-written form (looked up by <c>Type</c> in the editor).</summary>
public interface IActionDescriptor
{
    /// <summary>Groups actions in the picker, e.g. "OBS", "Klavye", "Sayfa &amp; Profil".</summary>
    string Category { get; }

    string? Description { get; }

    /// <summary>A lucide-react icon name.</summary>
    string? Icon { get; }

    /// <summary>Empty/absent means the editor falls back to a hand-written or generic JSON form.</summary>
    IReadOnlyList<SettingField> Fields { get; }
}
