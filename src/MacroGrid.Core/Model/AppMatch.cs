namespace MacroGrid.Core.Model;

/// <summary>One "switch to this profile when this app is in the foreground" rule (docs/auto-profile-switch.md).</summary>
public sealed class AppMatch
{
    /// <summary>Executable name, e.g. "Player.exe" — matched case-insensitively against the foreground
    /// window's owning process.</summary>
    public string ProcessName { get; set; } = "";

    /// <summary>Optional extra filter: the window title must contain this (case-insensitive) too. Null/empty
    /// means any window of that process matches.</summary>
    public string? TitleContains { get; set; }
}
