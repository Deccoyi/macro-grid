namespace MacroGrid.Core.Preferences;

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
/// profile or browser: a fresh WebView profile or a cleared cache must not lose these, so they live with
/// the rest of the user's data on the server instead of in localStorage.</summary>
public sealed class AppPreferences
{
    public string Theme { get; set; } = "dark";
    public string Language { get; set; } = "tr";
    public List<PreviewProfile> PreviewProfiles { get; set; } = [];

    /// <summary>Fallback profile a device resolves to when it has no explicit assignment and no
    /// auto-switch rule currently applies (docs/auto-profile-switch.md). Null means "no preference set" —
    /// falls back to <c>ProfileStore.First()</c>, same as before this existed.</summary>
    public string? DefaultProfileId { get; set; }

    /// <summary>Whether each Inspector section ("appearance", "typeFields", "actions", "css") is
    /// collapsed — only entries the user actually toggled are stored; a missing key falls back to that
    /// section's own hardcoded default (see Inspector.tsx's SECTION_DEFAULTS).</summary>
    public Dictionary<string, bool> CollapsedInspectorSections { get; set; } = [];
}
