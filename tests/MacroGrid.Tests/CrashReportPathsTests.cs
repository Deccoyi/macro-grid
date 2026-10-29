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
    [InlineData(@"   at Plugin.Run() in C:\Users\Jane Doe\source\p\Run.cs:line 42", @"   at Plugin.Run() in <home>\source\p\Run.cs:line 42")]
    [InlineData(@"Loaded C:\Users\Jane Doe\AppData\Roaming\MacroGrid\plugins\a\a.dll", @"Loaded <data>\plugins\a\a.dll")]
    [InlineData(@"Loaded C:\Program Files\Macro Grid\MacroGrid.Core.dll", @"Loaded <app>\MacroGrid.Core.dll")]
    [InlineData(@"   at X.Y() in D:\build\agent\src\X.cs:line 7", @"   at X.Y() in <path>:line 7")]
    [InlineData(@"Launch D:\Private\taxes.xlsx failed", "Launch <path> failed")]
    [InlineData(@"Open \\nas\share\notes.txt", "Open <path>")]
    [InlineData("listening on 127.0.0.1", "listening on 127.0.0.1")]
    [InlineData("see https://example.com/docs", "see https://example.com/docs")]
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
    [InlineData(@"in <home>\source\Run.cs:line 4")]
    [InlineData(@"Loaded <data>\plugins\a.dll")]
    [InlineData(@"Loaded <app>\a.dll")]
    [InlineData(@"in <data>\\x\y")]
    [InlineData("in <path>:line 7")]
    public void A_label_already_in_place_is_never_touched_again(string line)
    {
        Assert.Equal(line, CrashReportPaths.Clean(line, Install));
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
