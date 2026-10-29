using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MacroGrid.Core.Diagnostics;

/// <summary>What a start found in the crash marker folder: the readable markers (newest first), the time of each one that could not be read
/// (null when even the file name gives none), and every marker file, so all of them can be deleted once the report was built.</summary>
public sealed record CrashMarkerSet(IReadOnlyList<CrashMarker> Markers, IReadOnlyList<DateTimeOffset?> Unreadable, IReadOnlyList<string> Files)
{
    public static CrashMarkerSet Empty { get; } = new([], [], []);
}

/// <summary>Writes, reads and deletes the crash marker files (<c>crash-yyyyMMdd-HHmmss-fff.json</c> in the crashes folder of the data folder).
/// Writing and deleting never throw: they run while the process dies or after the report was shown.</summary>
public static partial class CrashMarkers
{
    public const string FolderName = "crashes";
    public const int MaxDetailsLength = 32 * 1024;
    public const long MaxFileBytes = 128 * 1024;
    public const int MaxMarkersRead = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Writes the marker as a new file (never replaces one). Returns false when it could not be written.</summary>
    public static bool TryWrite(string crashesDir, CrashMarker marker)
    {
        try
        {
            Directory.CreateDirectory(crashesDir);
            var json = JsonSerializer.SerializeToUtf8Bytes(marker, JsonOptions);
            var stamp = marker.CrashedAt.LocalDateTime.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            for (var n = 1; n < 1000; n++)
            {
                var path = Path.Combine(crashesDir, n == 1 ? $"crash-{stamp}.json" : $"crash-{stamp}-{n}.json");
                FileStream file;
                try { file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
                catch (IOException) when (File.Exists(path)) { continue; }

                try
                {
                    using (file) file.Write(json);
                    return true;
                }
                catch (Exception)
                {
                    TryDelete(path);
                    return false;
                }
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Reads the folder: only files named like a marker count. The newest <see cref="MaxMarkersRead"/> are read, older ones are only listed
    /// in <see cref="CrashMarkerSet.Files"/>. A file that is empty, too large, not valid JSON or of a newer format counts as unreadable.</summary>
    public static CrashMarkerSet Read(string crashesDir)
    {
        try
        {
            if (!Directory.Exists(crashesDir)) return CrashMarkerSet.Empty;
            var found = new List<(string Path, string Stamp, int Suffix)>();
            foreach (var path in Directory.EnumerateFiles(crashesDir, "crash-*.json"))
            {
                var match = FileName().Match(System.IO.Path.GetFileName(path));
                if (!match.Success) continue;
                found.Add((path, match.Groups[1].Value, match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1));
            }

            var ordered = found.OrderByDescending(f => f.Stamp, StringComparer.Ordinal).ThenByDescending(f => f.Suffix).ToList();
            var markers = new List<CrashMarker>();
            var unreadable = new List<DateTimeOffset?>();
            for (var i = 0; i < Math.Min(ordered.Count, MaxMarkersRead); i++)
            {
                if (TryReadOne(ordered[i].Path) is { } marker) markers.Add(marker);
                else unreadable.Add(TimeFromStamp(ordered[i].Stamp));
            }
            return new CrashMarkerSet(markers, unreadable, ordered.Select(f => f.Path).ToList());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CrashMarkerSet.Empty;
        }
    }

    /// <summary>Deletes the files; one that is locked or already gone is skipped.</summary>
    public static void Delete(IEnumerable<string> paths)
    {
        foreach (var path in paths) TryDelete(path);
    }

    private static CrashMarker? TryReadOne(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes) return null;
            var marker = JsonSerializer.Deserialize<CrashMarker>(File.ReadAllBytes(path), JsonOptions);
            if (marker is null || marker.Format != CrashMarker.CurrentFormat) return null;
            return marker with
            {
                MacroGrid = marker.MacroGrid ?? "", Os = marker.Os ?? "", Runtime = marker.Runtime ?? "",
                ExceptionType = marker.ExceptionType ?? "", Details = marker.Details ?? "",
                Plugins = marker.Plugins?.Where(p => p is not null).Select(p => new CrashPlugin(p.Id ?? "", p.Version ?? "", p.Status ?? "")).ToList(),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? TimeFromStamp(string stamp)
    {
        if (!DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return null;
        return new DateTimeOffset(time, TimeZoneInfo.Local.GetUtcOffset(time));
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception)
        {
            // It shows again on the next start; accepted.
        }
    }

    [GeneratedRegex(@"^crash-(\d{8}-\d{6}-\d{3})(?:-(\d+))?\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex FileName();
}
