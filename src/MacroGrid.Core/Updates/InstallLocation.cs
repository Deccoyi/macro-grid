namespace MacroGrid.Core.Updates;

/// <summary>Tells an installed copy (put there by the installer, and so upgradable by it) from a portable one.</summary>
public static class InstallLocation
{
    /// <summary>
    /// True when the folder the installer recorded for the app (its uninstall entry's <c>InstallLocation</c>) is the folder the running
    /// exe lives in. Null or empty means the app was never installed; a different folder means the running copy is a portable one.
    /// </summary>
    public static bool IsSameFolder(string? recordedLocation, string runningFolder)
    {
        if (string.IsNullOrWhiteSpace(recordedLocation) || string.IsNullOrWhiteSpace(runningFolder)) return false;
        try
        {
            return string.Equals(Normalize(recordedLocation), Normalize(runningFolder), StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
