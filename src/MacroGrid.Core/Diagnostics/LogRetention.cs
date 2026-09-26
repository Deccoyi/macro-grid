using System.Globalization;

namespace MacroGrid.Core.Diagnostics;

/// <summary>
/// How long the daily log files (<c>server-yyyy-MM-dd.log</c>) are kept, so they never pile up on the PC:
/// files older than <see cref="MaxAgeDays"/> days are deleted, one day's file stops growing at
/// <see cref="MaxFileBytes"/>, and all files together stay under <see cref="MaxTotalBytes"/> (the oldest go first).
/// </summary>
public static class LogRetention
{
    public const int MaxAgeDays = 14;
    public const long MaxFileBytes = 5 * 1024 * 1024;
    public const long MaxTotalBytes = 20 * 1024 * 1024;

    public const string FilePrefix = "server-";
    public const string FileExtension = ".log";

    public static string FileName(DateOnly day) => $"{FilePrefix}{day:yyyy-MM-dd}{FileExtension}";

    /// <summary>Deletes what the rules above allow. Today's file is never deleted. Only files named like a log
    /// file are touched. Returns the names of the deleted files.</summary>
    public static IReadOnlyList<string> Prune(string dir, DateOnly today)
    {
        var deleted = new List<string>();
        if (!Directory.Exists(dir)) return deleted;

        var files = new List<(FileInfo File, DateOnly Day)>();
        foreach (var file in new DirectoryInfo(dir).EnumerateFiles($"{FilePrefix}*{FileExtension}"))
        {
            if (TryParseDay(file.Name, out var day)) files.Add((file, day));
        }

        var oldestKept = today.AddDays(-(MaxAgeDays - 1));
        var kept = new List<(FileInfo File, DateOnly Day)>();
        foreach (var entry in files)
        {
            if (entry.Day < oldestKept && TryDelete(entry.File)) deleted.Add(entry.File.Name);
            else kept.Add(entry);
        }

        var total = kept.Sum(f => f.File.Length);
        foreach (var entry in kept.OrderBy(f => f.Day))
        {
            if (total <= MaxTotalBytes) break;
            if (entry.Day >= today) continue;
            var length = entry.File.Length;
            if (TryDelete(entry.File))
            {
                total -= length;
                deleted.Add(entry.File.Name);
            }
        }
        return deleted;
    }

    internal static bool TryParseDay(string fileName, out DateOnly day)
    {
        day = default;
        if (!fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)) return false;
        var middle = fileName[FilePrefix.Length..^FileExtension.Length];
        return DateOnly.TryParseExact(middle, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
    }

    private static bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
