using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MacroGrid.Core.Diagnostics;

public sealed record LogFileInfo(string Name, long Bytes);

public sealed record LogExportPlugin(string Id, string Name, string Version, string Status);

/// <summary>What <c>info.json</c> in the log export says about this installation: versions and numbers only, never settings or names of people.</summary>
public sealed record LogExportInfo(
    string MacroGrid, string Os, string Runtime, string? Language, IReadOnlyList<LogExportPlugin> Plugins,
    int Profiles, int Pages, int Devices, IReadOnlyList<ExportLine> Problems, DateTimeOffset ExportedAt);

/// <summary>
/// The zip the Help menu's "Export logs" writes: the daily log files (newest first, redacted line by line, at most <see cref="MaxBytes"/> of text),
/// an <c>info.json</c> and a <c>README.txt</c>. Only files named like a log file are read, from the one log folder; profiles, preferences, plugin
/// data and pairing files are never included.
/// </summary>
public static class LogExport
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The log files an export would read, newest first.</summary>
    public static IReadOnlyList<LogFileInfo> List(string logsDir)
    {
        if (!Directory.Exists(logsDir)) return [];
        return new DirectoryInfo(logsDir).EnumerateFiles($"{LogRetention.FilePrefix}*{LogRetention.FileExtension}")
            .Select(f => (File: f, Valid: LogRetention.TryParseDay(f.Name, out var day), Day: day))
            .Where(x => x.Valid)
            .OrderByDescending(x => x.Day)
            .Select(x => new LogFileInfo(x.File.Name, x.File.Length))
            .ToList();
    }

    public static byte[] Build(string logsDir, LogExportInfo info, Redactor redactor, long maxBytes = MaxBytes)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var remaining = maxBytes;
            var left = new List<string>();
            foreach (var file in List(logsDir))
            {
                if (remaining <= 0) { left.Add(file.Name); continue; }
                var lines = ReadRedacted(Path.Combine(logsDir, file.Name), redactor);
                var kept = Newest(lines, remaining, out var cut);
                var entry = zip.CreateEntry($"logs/{file.Name}", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                {
                    if (cut) writer.WriteLine("[Earlier lines of this day were left out to keep the export small.]");
                    foreach (var line in kept) writer.WriteLine(line);
                }
                remaining -= kept.Sum(l => (long)Encoding.UTF8.GetByteCount(l) + 2);
                if (cut) remaining = 0;
            }

            WriteText(zip, "info.json", JsonSerializer.Serialize(new
            {
                info.ExportedAt, info.MacroGrid, info.Os, info.Runtime, info.Language, info.Plugins,
                counts = new { profiles = info.Profiles, pages = info.Pages, pairedDevices = info.Devices },
                logFilesLeftOut = left,
                problems = info.Problems,
            }, JsonOptions));
            WriteText(zip, "README.txt", ReadMe);
        }
        return stream.ToArray();
    }

    private const string ReadMe =
        "Macro Grid log export\r\n\r\n" +
        "logs/       The daily log files, newest first. Where a day was too large only its newest lines are kept.\r\n" +
        "info.json   The version, the computer's system, the installed plugins (no settings), counts, and the Error List.\r\n\r\n" +
        "Personal details (folders, user and computer names, device names, addresses, keys and similar) were removed automatically\r\n" +
        "where they could be recognized. That is best effort: look through the files before you share them.\r\n" +
        "Nothing was sent anywhere. Profiles, preferences, plugin data and pairing files are not part of this export.\r\n";

    private static List<string> ReadRedacted(string path, Redactor redactor)
    {
        var lines = new List<string>();
        // Today's file is still being written, so it is opened for sharing.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file, Encoding.UTF8);
        while (reader.ReadLine() is { } line) lines.Add(redactor.Line(line));
        return lines;
    }

    /// <summary>The newest lines (a suffix of the list) that fit into the byte budget.</summary>
    private static List<string> Newest(List<string> lines, long budget, out bool cut)
    {
        long used = 0;
        var start = lines.Count;
        while (start > 0)
        {
            var size = Encoding.UTF8.GetByteCount(lines[start - 1]) + 2;
            if (used + size > budget) break;
            used += size;
            start--;
        }
        cut = start > 0;
        return lines.GetRange(start, lines.Count - start);
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
