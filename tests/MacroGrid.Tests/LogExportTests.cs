using System.IO.Compression;
using System.Text;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class LogExportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-logexport-" + Guid.NewGuid().ToString("N"));
    private string Logs => Path.Combine(_root, "logs");

    public LogExportTests() => Directory.CreateDirectory(Logs);

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly Redactor Redact = new(new RedactionContext(UserName: "jane", MachineName: "OFFICE-PC"));

    private static LogExportInfo Info() => new("1.2.3", "Windows", ".NET", "en", [new LogExportPlugin("a", "A", "1.0", "Loaded")], 2, 5, 1, [], DateTimeOffset.UtcNow);

    private void Write(string name, params string[] lines) => File.WriteAllLines(Path.Combine(Logs, name), lines);

    private static Dictionary<string, string> Read(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void The_zip_holds_the_log_files_info_and_readme_redacted_and_nothing_else()
    {
        Write("server-2026-10-01.log", "start by jane");
        Write("server-2026-10-02.log", "pc OFFICE-PC");
        Write("notes.txt", "foreign");
        Write("server-bad.log", "foreign");
        Directory.CreateDirectory(Path.Combine(_root, "profiles"));
        File.WriteAllText(Path.Combine(_root, "profiles", "p.json"), "secret profile");

        var entries = Read(LogExport.Build(Logs, Info(), Redact));

        Assert.Equal(["README.txt", "info.json", "logs/server-2026-10-01.log", "logs/server-2026-10-02.log"], entries.Keys.Order(StringComparer.Ordinal));
        Assert.Contains("<user>", entries["logs/server-2026-10-01.log"]);
        Assert.Contains("<pc>", entries["logs/server-2026-10-02.log"]);
        Assert.DoesNotContain("foreign", string.Concat(entries.Values));
        Assert.DoesNotContain("secret profile", string.Concat(entries.Values));
        Assert.Contains("\"macroGrid\": \"1.2.3\"", entries["info.json"]);
    }

    [Fact]
    public void Files_are_listed_newest_first()
    {
        Write("server-2026-09-30.log", "a");
        Write("server-2026-10-02.log", "b");
        Assert.Equal(["server-2026-10-02.log", "server-2026-09-30.log"], LogExport.List(Logs).Select(f => f.Name));
        Assert.Empty(LogExport.List(Path.Combine(_root, "none")));
    }

    [Fact]
    public void The_size_cap_cuts_the_oldest_file_at_its_start_and_leaves_older_ones_out()
    {
        Write("server-2026-10-03.log", "new-1", "new-2");
        Write("server-2026-10-02.log", "mid-1", "mid-2", "mid-3", "mid-4");
        Write("server-2026-10-01.log", "old-1");

        // Every line is 5 bytes plus 2 for the line break: the newest file takes 14, so two lines of the next one fit.
        var entries = Read(LogExport.Build(Logs, Info(), Redact, maxBytes: 7 * 2 + 7 * 2));

        Assert.DoesNotContain("logs/server-2026-10-01.log", entries.Keys);
        var middle = entries["logs/server-2026-10-02.log"];
        Assert.StartsWith("[Earlier lines", middle);
        Assert.Contains("mid-4", middle);
        Assert.DoesNotContain("mid-1", middle);
        Assert.Contains("server-2026-10-01.log", entries["info.json"]);
    }

    [Fact]
    public void A_file_that_is_open_for_writing_is_still_read()
    {
        var path = Path.Combine(Logs, "server-2026-10-01.log");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        writer.Write(Encoding.UTF8.GetBytes("still open\n"));
        writer.Flush();

        Assert.Contains("still open", Read(LogExport.Build(Logs, Info(), Redact))["logs/server-2026-10-01.log"]);
    }

    [Fact]
    public void A_missing_folder_gives_a_zip_with_only_info_and_readme()
    {
        var entries = Read(LogExport.Build(Path.Combine(_root, "none"), Info(), Redact));
        Assert.Equal(["README.txt", "info.json"], entries.Keys.Order(StringComparer.Ordinal));
    }
}
