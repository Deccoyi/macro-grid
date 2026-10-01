using System.Text.Json;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public class ProblemExportTests
{
    private static readonly Redactor Redact = new(new RedactionContext(HomeFolder: @"C:\Users\Jane", UserName: "jane", MachineName: "OFFICE-PC"));

    [Fact]
    public void Server_and_editor_lines_are_in_one_document_and_redacted()
    {
        var problems = new ProblemList();
        problems.Report("p", "Lights", ProblemSeverity.Error, "P110", @"Cannot open C:\Users\Jane\a.txt for jane");
        var editor = new[] { new ExportLine("warning", "W300", "No free cell on OFFICE-PC", "Editor", 2, Page: "Main", Widget: "Button_1") };

        var json = ProblemExport.Build(editor, problems.Snapshot(), Redact, DateTimeOffset.Parse("2026-10-01T10:00:00Z"), "1.2.3");

        Assert.DoesNotContain("Jane", json);
        Assert.DoesNotContain("OFFICE", json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1.2.3", doc.RootElement.GetProperty("macroGrid").GetString());
        var lines = doc.RootElement.GetProperty("lines");
        Assert.Equal(2, lines.GetArrayLength());
        Assert.Equal("P110", lines[0].GetProperty("code").GetString());
        Assert.Equal(@"Cannot open <home>\a.txt for <user>", lines[0].GetProperty("message").GetString());
        Assert.Equal("Button_1", lines[1].GetProperty("widget").GetString());
        Assert.Equal(2, lines[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public void Text_is_cleaned_and_cut()
    {
        var editor = new[] { new ExportLine("error", "E1", new string('x', 1000) + "\u0000", "Editor") };
        var json = ProblemExport.Build(editor, [], Redact, DateTimeOffset.UtcNow, "1");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("lines")[0].GetProperty("message").GetString()!.Length <= 300);
    }

    [Fact]
    public void Too_many_lines_are_refused()
    {
        Assert.Null(ProblemExport.Refusal(ProblemExport.MaxLines));
        Assert.NotNull(ProblemExport.Refusal(ProblemExport.MaxLines + 1));
    }

    [Fact]
    public void The_document_never_holds_more_than_the_cap()
    {
        var editor = Enumerable.Range(0, ProblemExport.MaxLines + 50).Select(i => new ExportLine("info", "I", "m" + i, "Editor"));
        using var doc = JsonDocument.Parse(ProblemExport.Build(editor, [], Redact, DateTimeOffset.UtcNow, "1"));
        Assert.Equal(ProblemExport.MaxLines, doc.RootElement.GetProperty("lines").GetArrayLength());
    }
}
