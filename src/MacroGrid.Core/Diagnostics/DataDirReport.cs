using System.Text.Json;

namespace MacroGrid.Core.Diagnostics;

/// <summary>One line of the startup report: <see cref="Problem"/> lines are logged as warnings.</summary>
public readonly record struct ReportLine(bool Problem, string Text);

/// <summary>
/// What is on disk in the data folder compared with what the server actually loaded from it. Written to the log at every
/// start, so a start that sees less than the folder holds (a profile file that is not in the profile list, a plugin folder that is
/// not loaded, a folder the server cannot write to) shows up in the log instead of only as an empty editor.
/// </summary>
public static class DataDirReport
{
    /// <summary>The entries of the data folder, its <c>profiles</c> folder and the names in <c>plugins</c>: name, size, last write (UTC).</summary>
    public static IReadOnlyList<ReportLine> Inventory(string dataDir)
    {
        var lines = new List<ReportLine>();
        if (!Directory.Exists(dataDir))
        {
            lines.Add(new(true, $"The data folder does not exist: {dataDir}"));
            return lines;
        }

        foreach (var (label, dir) in new[] { ("data", dataDir), ("profiles", Path.Combine(dataDir, "profiles")), ("plugins", Path.Combine(dataDir, "plugins")) })
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    lines.Add(new(label == "data", $"Folder '{label}' is missing: {dir}"));
                    continue;
                }

                var entries = new DirectoryInfo(dir).EnumerateFileSystemInfos().OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(e => e is FileInfo f ? $"{f.Name} ({f.Length} B, {f.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z)" : $"{e.Name}/")
                    .ToList();
                lines.Add(new(false, $"Folder '{label}' ({dir}) holds {entries.Count} entries: {(entries.Count == 0 ? "(none)" : string.Join("; ", entries))}"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lines.Add(new(true, $"Folder '{label}' could not be listed: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}"));
            }
        }

        return lines;
    }

    /// <summary>Every <c>profiles/*.json</c> against the ids the profile store holds in memory.</summary>
    public static IReadOnlyList<ReportLine> CompareProfiles(string dataDir, IReadOnlyCollection<string> loadedIds)
    {
        var lines = new List<ReportLine>();
        var dir = Path.Combine(dataDir, "profiles");
        var onDisk = 0;
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    onDisk++;
                    var name = Path.GetFileName(file);
                    string? id = null;
                    string? failure = null;
                    try
                    {
                        using var document = JsonDocument.Parse(File.ReadAllText(file));
                        id = document.RootElement.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                    {
                        failure = $"{ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}";
                    }

                    if (failure is not null)
                        lines.Add(new(true, $"Profile file {name} exists but cannot be read: {failure}"));
                    else if (id is not null && loadedIds.Contains(id))
                        lines.Add(new(false, $"Profile file {name} is loaded (id {id})"));
                    else
                        lines.Add(new(true, $"Profile file {name} (id {id ?? "?"}) is on disk but NOT in the profile list"));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lines.Add(new(true, $"The profiles folder could not be read: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}"));
        }

        lines.Add(new(false, $"Profiles: {onDisk} file(s) on disk, {loadedIds.Count} in the profile list"));
        return lines;
    }

    /// <summary>Every plugin folder that has a <c>plugin.json</c> against the plugin ids the manager lists (with their status).</summary>
    public static IReadOnlyList<ReportLine> ComparePlugins(string dataDir, IReadOnlyDictionary<string, string> loadedStatusById)
    {
        var lines = new List<ReportLine>();
        var dir = Path.Combine(dataDir, "plugins");
        var found = 0;
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (var pluginDir in Directory.EnumerateDirectories(dir))
                {
                    if (!File.Exists(Path.Combine(pluginDir, "plugin.json"))) continue;
                    found++;
                    var folder = Path.GetFileName(pluginDir);
                    if (loadedStatusById.TryGetValue(folder, out var status))
                        lines.Add(new(status is "Error" or "Incompatible", $"Plugin folder {folder}: {status}"));
                    else
                        lines.Add(new(true, $"Plugin folder {folder} has a plugin.json but is NOT in the plugin list"));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lines.Add(new(true, $"The plugins folder could not be read: {ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}"));
        }

        lines.Add(new(false, $"Plugins: {found} folder(s) with a plugin.json, {loadedStatusById.Count} in the plugin list"));
        return lines;
    }

    /// <summary>Writes a small file into the data folder, reads it back and deletes it. Returns null when all of that worked, otherwise what failed.</summary>
    public static string? WriteProbe(string dataDir)
    {
        var path = Path.Combine(dataDir, $".write-probe-{Guid.NewGuid():N}.tmp");
        try
        {
            var content = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, content);
            var read = File.ReadAllText(path);
            File.Delete(path);
            return read == content ? null : "the probe file was written but read back with different content";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}";
        }
    }
}
