namespace MacroStation.Core.Preferences;

/// <summary>A user-defined "cihaz önizleme" size the editor's Önizleme dropdown offers alongside the
/// built-in phone/tablet presets.</summary>
public sealed class PreviewProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    public int Width { get; set; } = 390;
    public int Height { get; set; } = 844;
}

/// <summary>Editor-wide preferences: theme, language, user-defined preview sizes. Not tied to any one
/// profile or browser — see docs/agent-notes.md on why this replaced localStorage (a fresh browser
/// profile, a cleared cache, or opening the editor from a different machine on the LAN must not lose
/// these; they live with the rest of the user's data on the server instead).</summary>
public sealed class AppPreferences
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "tr";
    public List<PreviewProfile> PreviewProfiles { get; set; } = [];

    /// <summary>Whether each Inspector section ("appearance", "typeFields", "actions", "css") is
    /// collapsed — only entries the user actually toggled are stored; a missing key falls back to that
    /// section's own hardcoded default (see Inspector.tsx's SECTION_DEFAULTS).</summary>
    public Dictionary<string, bool> CollapsedInspectorSections { get; set; } = [];
}
