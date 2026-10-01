using System.Text.Json;

namespace MacroGrid.Core.Diagnostics;

/// <summary>One line of an exported Error List, already written out as text. The editor sends its own lines in this shape.</summary>
public sealed record ExportLine(
    string? Severity, string? Code, string? Message, string? Source, int Count = 1, string? Page = null, string? Widget = null,
    DateTimeOffset? FirstAt = null, DateTimeOffset? LastAt = null);

/// <summary>Builds the document of the Error List export: the editor's lines plus the server's own, every text cleaned and redacted.</summary>
public static class ProblemExport
{
    public const int MaxLines = 500;
    public const int MaxBodyBytes = 256 * 1024;

    private const int MaxText = 300;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The refusal text when the editor's request is too big, or null when it is fine.</summary>
    public static string? Refusal(int lineCount) =>
        lineCount > MaxLines ? $"Too many lines to export (at most {MaxLines}). Clear the list or filter it first." : null;

    public static string Build(IEnumerable<ExportLine> editorLines, IEnumerable<Problem> serverProblems, Redactor redactor, DateTimeOffset now, string version)
    {
        var lines = new List<ExportLine>();
        lines.AddRange(serverProblems.Select(p => new ExportLine(
            p.Severity.ToString().ToLowerInvariant(), p.Code, p.Message, p.SourceName, p.Count, FirstAt: p.FirstAt, LastAt: p.LastAt)));
        lines.AddRange(editorLines);

        var document = new
        {
            exportedAt = now,
            macroGrid = version,
            note = "Personal details were removed automatically where they could be recognized. Look through the file before sharing it.",
            lines = lines.Take(MaxLines).Select(l => new
            {
                severity = Text(l.Severity, redactor, 20),
                code = Text(l.Code, redactor, 20),
                message = Text(l.Message, redactor, MaxText),
                source = Text(l.Source, redactor, 100),
                count = Math.Max(1, l.Count),
                firstAt = l.FirstAt,
                lastAt = l.LastAt,
                page = l.Page is null ? null : Text(l.Page, redactor, 100),
                widget = l.Widget is null ? null : Text(l.Widget, redactor, 100),
            }),
        };
        return JsonSerializer.Serialize(document, Options);
    }

    private static string Text(string? value, Redactor redactor, int max) => PlainText.Clean(redactor.Line(PlainText.Clean(value, Redactor.MaxLineLength)), max);
}
