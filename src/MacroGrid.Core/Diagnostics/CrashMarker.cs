using System.Runtime.InteropServices;

namespace MacroGrid.Core.Diagnostics;

public enum CrashPhase { Starting, Running }

public sealed record CrashPlugin(string Id, string Version, string Status);

/// <summary>
/// What the crash handler leaves behind: one raw (not redacted) file in the person's own data folder, read by the next start to build the
/// crash report. <see cref="Plugins"/> is null when the crash came before the plugins were loaded; <see cref="LogFile"/> and
/// <see cref="LogLength"/> name today's log file and how many bytes it held at the crash (null and 0 when there is none).
/// </summary>
public sealed record CrashMarker(
    int Format, DateTimeOffset CrashedAt, CrashPhase Phase, bool IsTerminating, string MacroGrid, string Os, string Runtime,
    long UptimeSeconds, string ExceptionType, string Details, IReadOnlyList<CrashPlugin>? Plugins, string? LogFile, long LogLength)
{
    public const int CurrentFormat = 1;

    public static CrashMarker From(
        Exception ex, CrashPhase phase, DateTimeOffset now, string version, TimeSpan uptime, IReadOnlyList<CrashPlugin>? plugins, string logsDir)
    {
        var type = ex.GetType().FullName ?? ex.GetType().Name;
        string details;
        try { details = ex.ToString(); }
        catch (Exception) { details = type; }
        if (details.Length > CrashMarkers.MaxDetailsLength)
            details = string.Concat(details.AsSpan(0, CrashMarkers.MaxDetailsLength - 3), "...");

        string? logFile = null;
        long logLength = 0;
        try
        {
            var name = LogRetention.FileName(DateOnly.FromDateTime(now.DateTime));
            var info = new FileInfo(Path.Combine(logsDir, name));
            if (info.Exists) (logFile, logLength) = (name, info.Length);
        }
        catch (Exception)
        {
            // No log information is better than no marker.
        }

        return new CrashMarker(
            CurrentFormat, now, phase, true, version, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription,
            (long)uptime.TotalSeconds, type, details, plugins, logFile, logLength);
    }
}
