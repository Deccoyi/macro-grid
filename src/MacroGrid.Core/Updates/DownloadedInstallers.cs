namespace MacroGrid.Core.Updates;

/// <summary>
/// Keeps the folder the updater downloads into (<c>%LocalAppData%\MacroGrid\updates</c>, one sub-folder per version) from filling up with
/// installers of about 63 MB each. At most one downloaded installer stays: the newest one that is newer than the running version, which is the
/// one a retry would reuse. Everything else the updater put there goes: older offers, versions that are installed already, half-finished
/// <c>.part</c> files. Only sub-folders named like a version are touched; anything else in there, and any file the person saved elsewhere, is left alone.
/// </summary>
public static class DownloadedInstallers
{
    /// <summary>Applies the rule to <paramref name="updatesRoot"/> and returns the paths it deleted. A path that cannot be deleted goes to <paramref name="onError"/> and never stops the rest.</summary>
    public static IReadOnlyList<string> CleanUp(string updatesRoot, ReleaseVersion running, Action<string, Exception>? onError = null)
    {
        var deleted = new List<string>();
        if (!Directory.Exists(updatesRoot)) return deleted;

        List<(string Path, ReleaseVersion Version)> folders;
        try
        {
            folders = Directory.EnumerateDirectories(updatesRoot)
                .Select(path => (Path: path, Ok: ReleaseVersion.TryParse(System.IO.Path.GetFileName(path), out var version), Version: version))
                .Where(f => f.Ok)
                .Select(f => (f.Path, f.Version))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onError?.Invoke(updatesRoot, ex);
            return deleted;
        }

        var keep = folders.Where(f => f.Version > running).OrderByDescending(f => f.Version).Select(f => f.Path).FirstOrDefault();

        foreach (var (path, _) in folders)
        {
            if (path == keep)
            {
                DeletePartFiles(path, deleted, onError);
                continue;
            }
            TryDelete(() => Directory.Delete(path, recursive: true), path, deleted, onError);
        }

        // With nothing left to keep, the root itself is empty; remove it too unless something else lives there.
        if (keep is null && !Directory.EnumerateFileSystemEntries(updatesRoot).Any())
            TryDelete(() => Directory.Delete(updatesRoot), updatesRoot, deleted, onError);

        return deleted;
    }

    private static void DeletePartFiles(string folder, List<string> deleted, Action<string, Exception>? onError)
    {
        try
        {
            foreach (var part in Directory.EnumerateFiles(folder, "*.part"))
                TryDelete(() => File.Delete(part), part, deleted, onError);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onError?.Invoke(folder, ex);
        }
    }

    private static void TryDelete(Action delete, string path, List<string> deleted, Action<string, Exception>? onError)
    {
        try
        {
            delete();
            deleted.Add(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            onError?.Invoke(path, ex);
        }
    }
}
