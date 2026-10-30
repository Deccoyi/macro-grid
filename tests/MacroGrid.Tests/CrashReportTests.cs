using System.Text;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class CrashReportTests : IDisposable
{
    private const string Install = @"C:\Program Files\Macro Grid\";
    private const string LogName = "server-2026-10-29.log";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-crashreport-" + Guid.NewGuid().ToString("N"));
    private string Logs => Path.Combine(_root, "logs");

    public CrashReportTests() => Directory.CreateDirectory(Logs);

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly Redactor Redact = new(new RedactionContext(
        @"C:\Users\Jane Doe", @"C:\Users\Jane Doe\AppData\Roaming\MacroGrid", "janedoe", "OFFICE-PC", ["Jane's Phone", "Tab (2)"]));

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 14, 3, 12, TimeSpan.FromHours(3));

    private static CrashMarker Marker(
        string details = "System.InvalidOperationException: boom", DateTimeOffset? at = null, CrashPhase phase = CrashPhase.Running,
        IReadOnlyList<CrashPlugin>? plugins = null, string? logFile = LogName, long logLength = 0, string type = "System.InvalidOperationException") => new(
        1, at ?? Now, phase, true, "0.5.0", "Microsoft Windows 10.0.26200", ".NET 10.0.1", 3723, type, details, plugins, logFile, logLength);

    private static CrashMarkerSet Set(params CrashMarker[] markers) => new(markers, [], []);

    private string Build(CrashMarkerSet set) => CrashReport.Build(set, Logs, Redact, Install);

    private static string[] Lines(string report) => report.Split("\r\n");

    private string WriteLog(IEnumerable<string> lines)
    {
        var text = string.Concat(lines.Select(l => l + "\r\n"));
        File.WriteAllText(Path.Combine(Logs, LogName), text, new UTF8Encoding(false));
        return text;
    }

    [Fact]
    public void The_report_has_the_header_the_fields_and_the_error()
    {
        var report = Build(Set(Marker(plugins: [new CrashPlugin("com.example.timer", "1.2.0", "Loaded"), new CrashPlugin("com.example.obs", "0.3.1", "Error")])));

        var lines = Lines(report);
        Assert.Equal("Macro Grid crash report", lines[0]);
        Assert.Equal("Nothing has been sent. Read this text before you share it.", lines[1]);
        Assert.Equal("The full log files: Help > Export logs in the editor.", lines[2]);
        Assert.Contains("Crashed at:   2026-10-29 14:03:12 +03:00", lines);
        Assert.Contains("While:        running", lines);
        Assert.Contains("Version:      0.5.0", lines);
        Assert.Contains("Windows:      Microsoft Windows 10.0.26200", lines);
        Assert.Contains("Runtime:      .NET 10.0.1", lines);
        Assert.Contains("Running for:  01:02:03", lines);
        Assert.Contains("Plugins:      com.example.timer 1.2.0 (Loaded), com.example.obs 0.3.1 (Error)", lines);
        Assert.Contains("Error:", lines);
        Assert.Contains("System.InvalidOperationException: boom", lines);
        Assert.EndsWith("\r\n", report);
    }

    [Theory]
    [InlineData(@"   at Plugin.Run() in C:\Users\Jane Doe\source\p\Run.cs:line 42", @"   at Plugin.Run() in <path>:line 42")]
    [InlineData(@"Loaded C:\Users\Jane Doe\AppData\Roaming\MacroGrid\plugins\a\a.dll", "Loaded <path>")]
    [InlineData(@"Loaded C:\Program Files\Macro Grid\MacroGrid.Core.dll", @"Loaded <app>\MacroGrid.Core.dll")]
    [InlineData(@"   at X.Y() in D:\build\agent\src\X.cs:line 7", @"   at X.Y() in <path>:line 7")]
    [InlineData(@"Launch D:\Private\taxes.xlsx failed", "Launch <path> failed")]
    [InlineData(@"Open \\nas\share\notes.txt", "Open <path>")]
    [InlineData("InvalidOperationException: device Jane's Phone was removed", "InvalidOperationException: device <device 1> was removed")]
    [InlineData("user janedoe on OFFICE-PC", "user <user> on <pc>")]
    [InlineData("connect to 192.168.1.20:8765 failed", "connect to <ip>:8765 failed")]
    [InlineData("listening on 127.0.0.1", "listening on 127.0.0.1")]
    // The next two rows follow what Redactor gives (the plan listed a slightly different but equally safe text).
    [InlineData("GET /api?token=abc123&x=1", "GET /api?token=<redacted>")]
    [InlineData("Authorization: Bearer eyJhbGciOi", "Authorization: <redacted> <redacted>")]
    [InlineData("pairing pin: 482913", "pairing pin: <redacted>")]
    [InlineData("mail jane@example.com", "mail <email>")]
    [InlineData("see https://example.com/docs", "see https://<host>")]
    public void The_redaction_table_holds_end_to_end_for_the_error_and_the_log(string input, string expected)
    {
        WriteLog([input]);
        var marker = Marker(details: input, logLength: new FileInfo(Path.Combine(Logs, LogName)).Length);

        var lines = Lines(Build(Set(marker)));

        Assert.Equal(2, lines.Count(l => l == expected));
    }

    [Fact]
    public void The_log_tail_stops_at_the_length_of_the_crash()
    {
        var before = WriteLog(["13:00:00 [INFO] one", "13:00:01 [INFO] two"]);
        var length = Encoding.UTF8.GetByteCount(before);
        File.AppendAllText(Path.Combine(Logs, LogName), "13:05:00 [INFO] written by the next start\r\n");

        var report = Build(Set(Marker(logLength: length)));

        Assert.Contains("13:00:01 [INFO] two", report);
        Assert.DoesNotContain("next start", report);
        Assert.Contains($"Log before the crash ({LogName}, newest 2 lines):", report);
    }

    [Fact]
    public void A_partial_last_line_at_the_crash_length_is_kept_as_far_as_it_goes()
    {
        var text = WriteLog(["first", "second line"]);

        var report = Build(Set(Marker(logLength: Encoding.UTF8.GetByteCount(text) - 7)));

        var lines = Lines(report);
        Assert.Contains("first", lines);
        Assert.Contains("second", lines);
        Assert.DoesNotContain("second line", report);
    }

    [Fact]
    public void The_log_tail_keeps_the_newest_300_lines()
    {
        WriteLog(Enumerable.Range(1, 1000).Select(i => $"line {i}"));

        var report = Build(Set(Marker(logLength: new FileInfo(Path.Combine(Logs, LogName)).Length)));

        var lines = Lines(report);
        Assert.Contains("line 1000", lines);
        Assert.Contains("line 701", lines);
        Assert.DoesNotContain("line 700", lines);
        Assert.Contains($"Log before the crash ({LogName}, newest 300 lines):", lines);
    }

    [Fact]
    public void The_log_tail_keeps_at_most_64_KiB_and_the_whole_text_stays_under_160_KiB()
    {
        WriteLog(Enumerable.Range(1, 300).Select(i => $"{i:000} " + string.Concat(Enumerable.Repeat("word ", 200))));
        var marker = Marker(details: "Boom" + string.Concat(Enumerable.Repeat("\r\n   at Some.Method() in " + new string('y', 900), 140)), logLength: new FileInfo(Path.Combine(Logs, LogName)).Length);

        var report = Build(Set(marker));

        var logPart = report[report.IndexOf("Log before the crash (", StringComparison.Ordinal)..];
        Assert.True(Encoding.UTF8.GetByteCount(logPart) <= CrashReport.MaxLogBytes + 200, $"log part {Encoding.UTF8.GetByteCount(logPart)}");
        Assert.Contains("300 " + string.Concat(Enumerable.Repeat("word ", 200)), report);
        Assert.True(Encoding.UTF8.GetByteCount(report) <= CrashReport.MaxReportBytes);
    }

    [Fact]
    public void A_huge_exception_and_a_huge_log_still_fit_into_the_whole_cap()
    {
        WriteLog(Enumerable.Range(1, 300).Select(_ => string.Concat(Enumerable.Repeat("word ", 1400))));
        var marker = Marker(details: string.Concat(Enumerable.Repeat(new string('é', 7900) + "\r\n", 4)), logLength: new FileInfo(Path.Combine(Logs, LogName)).Length);

        var report = Build(Set(marker));

        Assert.True(Encoding.UTF8.GetByteCount(report) <= CrashReport.MaxReportBytes);
        Assert.Contains(new string('é', 100), report);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("server-2026-10-30.log")]
    [InlineData(@"..\preferences.json")]
    public void A_missing_log_file_is_said_and_has_no_log_section(string? logFile)
    {
        var report = Build(Set(Marker(logFile: logFile)));

        Assert.Contains("Log before the crash: no log file was found for that day.", report);
        Assert.DoesNotContain("Log before the crash (", report);
    }

    [Fact]
    public void Several_markers_show_the_newest_in_full_and_the_others_one_line_each()
    {
        var newest = Marker(details: "NEWEST DETAILS", at: Now);
        var older = Marker(details: "OLDER DETAILS", at: Now.AddDays(-1), phase: CrashPhase.Starting, type: "System.NullReferenceException");
        var set = new CrashMarkerSet([newest, older], [new DateTimeOffset(2026, 10, 27, 18, 44, 10, TimeSpan.FromHours(3)), null], []);

        var report = Build(set);

        var lines = Lines(report);
        Assert.Contains("NEWEST DETAILS", lines);
        Assert.DoesNotContain("OLDER DETAILS", report);
        Assert.Contains("Earlier crashes in this report (3):", lines);
        Assert.Contains("  2026-10-28 14:03:12 +03:00  System.NullReferenceException (starting)", lines);
        Assert.Contains("  2026-10-27 18:44:10 +03:00  (the details of this crash could not be read)", lines);
        Assert.Contains("  (time unknown)  (the details of this crash could not be read)", lines);
        Assert.True(Array.IndexOf(lines, "  2026-10-28 14:03:12 +03:00  System.NullReferenceException (starting)")
            < Array.IndexOf(lines, "  2026-10-27 18:44:10 +03:00  (the details of this crash could not be read)"));
    }

    [Fact]
    public void Only_unreadable_markers_say_that_the_details_could_not_be_read()
    {
        var report = Build(new CrashMarkerSet([], [Now], []));

        Assert.Contains("A crash happened, but the details could not be read.", report);
        Assert.Contains("Earlier crashes in this report (1):", report);
        Assert.DoesNotContain("Crashed at:", report);
    }

    [Fact]
    public void Plugins_null_says_they_are_not_known_yet_and_an_empty_list_says_none()
    {
        Assert.Contains("Plugins:      not known yet (the crash happened while starting)", Build(Set(Marker(plugins: null, phase: CrashPhase.Starting))));
        Assert.Contains("Plugins:      none", Build(Set(Marker(plugins: []))));
    }

    [Fact]
    public void Nothing_from_the_other_files_of_the_data_folder_appears()
    {
        Directory.CreateDirectory(Path.Combine(_root, "profiles"));
        File.WriteAllText(Path.Combine(_root, "profiles", "p.json"), "PROFILE-SECRET");
        File.WriteAllText(Path.Combine(_root, "devices.json"), "DEVICE-SECRET");
        File.WriteAllText(Path.Combine(_root, "preferences.json"), "PREFERENCES-SECRET");
        File.WriteAllText(Path.Combine(Logs, "notes.txt"), "FOREIGN-LOG-FOLDER-FILE");
        WriteLog(["a normal line"]);

        var report = Build(Set(Marker(logLength: new FileInfo(Path.Combine(Logs, LogName)).Length)));

        Assert.Contains("a normal line", report);
        Assert.DoesNotContain("SECRET", report);
        Assert.DoesNotContain("FOREIGN", report);
    }
}
