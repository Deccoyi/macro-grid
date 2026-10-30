using System.Diagnostics;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class CrashReportPathsTests
{
    private const string Install = @"C:\Program Files\Macro Grid\";

    private static readonly Redactor Redact = new(new RedactionContext(
        @"C:\Users\Jane Doe", @"C:\Users\Jane Doe\AppData\Roaming\MacroGrid", "janedoe", "OFFICE-PC", ["Jane's Phone", "Tab (2)"]));

    private static string Both(string line) => CrashReportPaths.Clean(Redact.Line(line), Install);

    [Theory]
    [InlineData(@"   at Plugin.Run() in C:\Users\Jane Doe\source\p\Run.cs:line 42", @"   at Plugin.Run() in <path>:line 42")]
    [InlineData(@"Loaded C:\Users\Jane Doe\AppData\Roaming\MacroGrid\plugins\a\a.dll", @"Loaded <path>")]
    [InlineData(@"Loaded C:\Program Files\Macro Grid\MacroGrid.Core.dll", @"Loaded <app>\MacroGrid.Core.dll")]
    [InlineData(@"   at X.Y() in D:\build\agent\src\X.cs:line 7", @"   at X.Y() in <path>:line 7")]
    [InlineData(@"Launch D:\Private\taxes.xlsx failed", "Launch <path> failed")]
    [InlineData(@"Open \\nas\share\notes.txt", "Open <path>")]
    [InlineData("listening on 127.0.0.1", "listening on 127.0.0.1")]
    [InlineData("see https://example.com/docs", "see https://<host>")]
    public void The_path_rows_of_the_plan_give_the_expected_text(string input, string expected)
    {
        Assert.Equal(expected, Both(input));
    }

    [Fact]
    public void A_folder_name_with_spaces_is_removed_up_to_the_file_name()
    {
        Assert.Equal("at <path> now", Both(@"at D:\My Private Files\Old Stuff\a.txt now"));
    }

    [Fact]
    public void A_forward_slash_path_under_the_install_folder_is_labelled_too()
    {
        Assert.Equal("Loaded <app>\\x/y.dll", CrashReportPaths.Clean("Loaded C:/Program Files/Macro Grid/x/y.dll", Install));
    }

    [Theory]
    [InlineData(@"Loaded <data>\logs\macro-grid-20261029.log")]
    [InlineData(@"Loaded <app>\a.dll")]
    [InlineData("in <path>:line 7")]
    public void A_label_already_in_place_is_never_touched_again(string line)
    {
        Assert.Equal(line, CrashReportPaths.Clean(line, Install));
    }

    [Theory]
    [InlineData(@"   at Reader.Open() in C:\Users\Jane Doe\Documents\Tax 2026.pdf:line 9", "   at Reader.Open() in <path>:line 9")]
    [InlineData(@"Could not find file 'C:\Users\Jane Doe\Documents\Tax 2026.pdf'.", "Could not find file '<path>'.")]
    [InlineData(@"   at Profiles.Load() in C:\Users\Jane Doe\AppData\Roaming\MacroGrid\profiles\Work.json:line 3", "   at Profiles.Load() in <path>:line 3")]
    [InlineData(@"Could not find file 'C:\Users\Jane Doe\AppData\Roaming\MacroGrid\profiles\Work.json'.", "Could not find file '<path>'.")]
    [InlineData(@"Opened C:\Users\Jane Doe\AppData\Roaming\MacroGrid\logs\macro-grid-20261029.log", @"Opened <data>\logs\macro-grid-20261029.log")]
    [InlineData(@"in <data>\\x\y", "in <path>")]
    [InlineData(@"Opened <data>\logs\..\profiles\Work.json", "Opened <path>")]
    [InlineData("Opened <data>/logs/../profiles/Work.json", "Opened <path>")]
    [InlineData(@"Opened <data>\logs\sub/..\..\profiles\Work.json", "Opened <path>")]
    [InlineData(@"Opened <data>\LOGS\macro-grid-20261029.log", @"Opened <data>\LOGS\macro-grid-20261029.log")]
    [InlineData("Opened <data>/logs/macro-grid-20261029.log", "Opened <data>/logs/macro-grid-20261029.log")]
    public void A_path_under_home_or_data_is_removed_except_the_log_folder(string input, string expected)
    {
        Assert.Equal(expected, Both(input));
    }

    [Theory]
    [InlineData("No such host is known. (nas.home.lan:443)", "No such host is known. (<host>)")]
    [InlineData("HttpRequestException: GET https://nas.home.lan:8443/api/profiles/Work?x=1 failed", "HttpRequestException: GET https://<host> failed")]
    [InlineData("connect ws://printer.local/ws failed", "connect ws://<host> failed")]
    [InlineData("connect to 192.168.1.20:8765 failed", "connect to <ip>:8765 failed")]
    [InlineData("http://127.0.0.1:5000/api/ping", "http://127.0.0.1:5000/api/ping")]
    [InlineData("http://localhost:5000/x", "http://localhost:5000/x")]
    [InlineData("took 12.30:45 s", "took 12.30:45 s")]
    [InlineData("http://LOCALHOST/p/Work", "http://LOCALHOST/p/Work")]
    [InlineData("http://[::1]:5000/p/Work", "http://[::1]:5000/p/Work")]
    [InlineData("http://localhost.evil.lan/p/Work", "http://<host>")]
    [InlineData("http://127.0.0.1.nip.io/x", "http://<host>")]
    [InlineData("http://localhost.evil.lan:5000/p/Work", "http://<host>")]
    [InlineData("http://127.0.0.1x/p/Work", "http://<host>")]
    public void The_host_of_a_url_or_a_host_and_port_is_removed(string input, string expected)
    {
        Assert.Equal(expected, Both(input));
    }

    [Theory]
    [InlineData("adapter 00:1A:2b:3C:4d:5E up", "adapter <mac> up")]
    [InlineData("adapter 00-1A-2B-3C-4D-5E up", "adapter <mac> up")]
    [InlineData("time 12:34:56 stays", "time 12:34:56 stays")]
    public void A_mac_address_is_removed(string input, string expected)
    {
        Assert.Equal(expected, Both(input));
    }

    [Fact]
    public void A_two_letter_user_name_and_pc_name_are_removed_only_when_asked()
    {
        var context = new RedactionContext(UserName: "jo", MachineName: "pc");
        const string line = "user jo on PC, jolly projects";
        Assert.Equal(line, new Redactor(context).Line(line));
        Assert.Equal("user <user> on <pc>, jolly projects", new Redactor(context, minNameLength: 2).Line(line));
    }

    [Fact]
    public void A_line_of_8000_characters_is_handled_in_well_under_a_second()
    {
        foreach (var line in new[] { new string('a', 8000), @"C:\" + string.Concat(Enumerable.Repeat("a b\\", 2000)), string.Concat(Enumerable.Repeat(@"\\x ", 2600)) })
        {
            var watch = Stopwatch.StartNew();
            CrashReportPaths.Clean(line, Install);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"took {watch.Elapsed}");
        }
    }
}
