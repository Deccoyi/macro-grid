using System.Globalization;
using System.Text;

namespace MacroGrid.Core.Diagnostics;

/// <summary>
/// The text the crash report window shows, saves and copies: English always, built in memory from the crash markers and the tail of the log
/// file the newest marker names. Every line goes through <see cref="Redactor.Line"/> and then <see cref="CrashReportPaths.Clean"/>. Nothing
/// else from the data folder is read.
/// </summary>
public static class CrashReport
{
    public const int MaxLogLines = 300;
    public const int MaxLogBytes = 64 * 1024;
    public const int MaxReportBytes = 160 * 1024;

    // The log file of a day is at most 5 MiB; the tail never needs more than the end of it.
    private const long TailReadBytes = 1024 * 1024;
    private const int MaxTypeLength = 200;
    private const string NewLine = "\r\n";

    public static string Build(CrashMarkerSet set, string logsDir, Redactor redactor, string installDir)
    {
        string Clean(string text) => CrashReportPaths.Clean(redactor.Line(text), installDir);

        var lines = new List<string>
        {
            Clean("Macro Grid crash report"),
            Clean("Nothing has been sent. Read this text before you share it."),
            Clean("The full log files: Help > Export logs in the editor."),
            "",
        };

        var newest = set.Markers.Count > 0 ? set.Markers[0] : null;
        if (newest is null)
        {
            lines.Add(Clean("A crash happened, but the details could not be read."));
        }
        else
        {
            lines.Add(Clean($"Crashed at:   {Time(newest.CrashedAt)}"));
            lines.Add(Clean($"While:        {Phase(newest.Phase)}"));
            lines.Add(Clean($"Version:      {newest.MacroGrid}"));
            lines.Add(Clean($"Windows:      {newest.Os}"));
            lines.Add(Clean($"Runtime:      {newest.Runtime}"));
            lines.Add(Clean($"Running for:  {Uptime(newest.UptimeSeconds)}"));
            lines.Add(Clean($"Plugins:      {Plugins(newest.Plugins)}"));
            lines.Add("");
            lines.Add("Error:");
            foreach (var line in newest.Details.Split('\n')) lines.Add(Clean(line.TrimEnd('\r')));
        }

        var earlier = set.Markers.Skip(1)
            .Select(m => (Time: (DateTimeOffset?)m.CrashedAt, Text: $"{Shorten(m.ExceptionType)} ({Phase(m.Phase)})"))
            .Concat(set.Unreadable.Select(t => (Time: t, Text: "(the details of this crash could not be read)")))
            .OrderByDescending(e => e.Time ?? DateTimeOffset.MinValue)
            .ToList();
        if (earlier.Count > 0)
        {
            lines.Add("");
            lines.Add($"Earlier crashes in this report ({earlier.Count.ToString(CultureInfo.InvariantCulture)}):");
            foreach (var (time, text) in earlier)
                lines.Add(Clean($"  {(time is { } t ? Time(t) : "(time unknown)")}  {text}"));
        }

        var head = Join(lines);
        if (newest is not null)
        {
            var budget = Math.Clamp(MaxReportBytes - Encoding.UTF8.GetByteCount(head) - 1024, 0, MaxLogBytes);
            lines.Add("");
            lines.AddRange(LogSection(newest, logsDir, Clean, budget));
        }

        var report = Join(lines);
        // Only a hand-made marker can get here; the log tail was already cut to fit.
        if (Encoding.UTF8.GetByteCount(report) > MaxReportBytes)
        {
            var bytes = Encoding.UTF8.GetBytes(report).AsSpan(0, MaxReportBytes - 3);
            report = Encoding.UTF8.GetString(bytes).TrimEnd('�') + "...";
        }
        return report;
    }

    private static List<string> LogSection(CrashMarker marker, string logsDir, Func<string, string> clean, int budget)
    {
        var tail = ReadTail(marker, logsDir);
        if (tail is null) return [clean("Log before the crash: no log file was found for that day.")];

        var kept = new List<string>();
        long used = 0;
        for (var i = tail.Count - 1; i >= 0 && kept.Count < MaxLogLines; i--)
        {
            var line = clean(tail[i]);
            var size = Encoding.UTF8.GetByteCount(line) + 2;
            if (used + size > budget) break;
            used += size;
            kept.Add(line);
        }
        kept.Reverse();

        var result = new List<string> { clean($"Log before the crash ({marker.LogFile}, newest {kept.Count.ToString(CultureInfo.InvariantCulture)} lines):") };
        result.AddRange(kept);
        return result;
    }

    /// <summary>The last lines of the log file the marker names, only of the bytes that existed at the crash; null when there is no such file.</summary>
    private static List<string>? ReadTail(CrashMarker marker, string logsDir)
    {
        // The name comes from a file in the data folder: only a name like a log file is followed, never a path.
        if (marker.LogFile is not { } name || !LogRetention.TryParseDay(name, out _)) return null;
        try
        {
            var path = Path.Combine(logsDir, name);
            if (!File.Exists(path)) return null;
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var end = Math.Min(Math.Max(marker.LogLength, 0), file.Length);
            var start = Math.Max(0, end - TailReadBytes);
            var buffer = new byte[end - start];
            file.Position = start;
            file.ReadExactly(buffer);

            var lines = Encoding.UTF8.GetString(buffer).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (start > 0) lines.RemoveAt(0); // starts in the middle of a line
            if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Join(List<string> lines) => string.Join(NewLine, lines) + NewLine;

    private static string Time(DateTimeOffset time) => time.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    private static string Phase(CrashPhase phase) => phase == CrashPhase.Starting ? "starting" : "running";

    private static string Shorten(string text) => text.Length <= MaxTypeLength ? text : text[..MaxTypeLength] + "...";

    private static string Uptime(long seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return string.Create(CultureInfo.InvariantCulture, $"{(long)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}");
    }

    private static string Plugins(IReadOnlyList<CrashPlugin>? plugins) =>
        plugins is null ? "not known yet (the crash happened while starting)"
        : plugins.Count == 0 ? "none"
        : string.Join(", ", plugins.Select(p => $"{p.Id} {p.Version} ({p.Status})"));
}
