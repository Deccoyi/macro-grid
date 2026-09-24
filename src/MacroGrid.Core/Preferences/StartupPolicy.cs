namespace MacroGrid.Core.Preferences;

/// <summary>
/// Decides whether the editor window opens when Macro Grid starts. Windows starts the app at sign-in with
/// <see cref="AutostartArgument"/> (the per-user Run entry carries it), so the two ways of starting can be told apart:
/// a start by the person follows <see cref="AppPreferences.LaunchMode"/> (window by default), a start by Windows follows
/// <see cref="AppPreferences.AutostartMode"/> (tray by default). An unknown value falls back to the default.
/// </summary>
public static class StartupPolicy
{
    public const string Window = "window";
    public const string Tray = "tray";

    /// <summary>Command-line argument the Windows startup entry passes to the exe.</summary>
    public const string AutostartArgument = "--autostart";

    public static bool ShouldOpenEditor(IEnumerable<string> args, AppPreferences preferences)
    {
        var startedByWindows = args.Any(a => string.Equals(a, AutostartArgument, StringComparison.OrdinalIgnoreCase));

        return startedByWindows
            ? string.Equals(preferences.AutostartMode, Window, StringComparison.OrdinalIgnoreCase)
            : !string.Equals(preferences.LaunchMode, Tray, StringComparison.OrdinalIgnoreCase);
    }
}
