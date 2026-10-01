using System.IO.Compression;
using System.Security.Cryptography;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.PluginTool;

namespace MacroGrid.Tests;

public sealed class PluginToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-tool-" + Guid.NewGuid().ToString("N"));

    public PluginToolTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = PluginToolApp.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private string GoodPlugin(string name = "good", string script = "host.log('hi');")
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"),
            "{ \"id\": \"demo.one\", \"name\": \"Demo\", \"version\": \"1.2.3\", \"minMacroGrid\": \"1.0.0\", \"entry\": \"index.js\", \"kind\": \"js\", \"permissions\": [\"variables\"] }");
        File.WriteAllText(Path.Combine(dir, "index.js"), script);
        File.WriteAllText(Path.Combine(dir, "LICENSE"), "MIT");
        return dir;
    }

    [Fact]
    public void No_command_or_an_unknown_one_is_a_usage_error()
    {
        Assert.Equal(PluginToolApp.Usage, Run().Code);
        Assert.Equal(PluginToolApp.Usage, Run("frobnicate").Code);
        Assert.Equal(PluginToolApp.Ok, Run("--help").Code);
    }

    [Fact]
    public void Validate_of_a_good_folder_prints_ok_and_exits_zero()
    {
        var result = Run("validate", GoodPlugin());

        Assert.Equal(PluginToolApp.Ok, result.Code);
        Assert.Equal("OK: demo.one 1.2.3", result.Out.Trim());
    }

    [Fact]
    public void Validate_of_a_broken_folder_lists_the_errors_and_exits_one()
    {
        var dir = GoodPlugin(script: "function (");

        var result = Run("validate", dir);

        Assert.Equal(PluginToolApp.Failed, result.Code);
        var lines = result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.StartsWith("error: index.js does not parse", lines[0]);
        Assert.Equal("FAILED: 1 error(s)", lines[^1]);
    }

    [Fact]
    public void Validate_without_a_folder_is_a_usage_error() =>
        Assert.Equal(PluginToolApp.Usage, Run("validate").Code);

    [Fact]
    public void Pack_writes_a_zip_that_extracts_back_without_saved_data_or_hidden_files()
    {
        var dir = GoodPlugin();
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "src", "lib.js"), "// lib");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(dir, "storage.json"), "{}");
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        File.WriteAllText(Path.Combine(dir, ".git", "config"), "x");
        File.WriteAllText(Path.Combine(dir, ".hidden"), "x");
        var outDir = Path.Combine(_root, "out");

        var result = Run("pack", dir, "--out", outDir);

        Assert.Equal(PluginToolApp.Ok, result.Code);
        var zipPath = Path.Combine(outDir, "demo.one-1.2.3.zip");
        using (var zip = ZipFile.OpenRead(zipPath))
            Assert.Equal(["LICENSE", "index.js", "plugin.json", "src/lib.js"], zip.Entries.Select(e => e.FullName).ToArray());

        var extracted = Path.Combine(_root, "extracted");
        PluginZip.ExtractSafely(File.ReadAllBytes(zipPath), extracted);
        Assert.Equal("// lib", File.ReadAllText(Path.Combine(extracted, "src", "lib.js")));
        Assert.False(File.Exists(Path.Combine(extracted, "settings.json")));
    }

    [Fact]
    public void Pack_is_byte_identical_twice_and_the_sha256_file_matches()
    {
        var dir = GoodPlugin();
        var first = Run("pack", dir, "--out", Path.Combine(_root, "a"));
        File.SetLastWriteTimeUtc(Path.Combine(dir, "index.js"), new DateTime(2020, 5, 5, 5, 5, 5, DateTimeKind.Utc));
        Run("pack", dir, "--out", Path.Combine(_root, "b"));

        var a = File.ReadAllBytes(Path.Combine(_root, "a", "demo.one-1.2.3.zip"));
        var b = File.ReadAllBytes(Path.Combine(_root, "b", "demo.one-1.2.3.zip"));
        Assert.Equal(a, b);

        var hash = Convert.ToHexStringLower(SHA256.HashData(a));
        Assert.Equal(hash, File.ReadAllText(Path.Combine(_root, "a", "demo.one-1.2.3.zip.sha256")));
        Assert.Contains("sha256=" + hash, first.Out);
    }

    [Fact]
    public void Pack_of_a_folder_with_errors_writes_nothing()
    {
        var dir = GoodPlugin(script: "function (");
        var outDir = Path.Combine(_root, "out");

        var result = Run("pack", dir, "--out", outDir);

        Assert.Equal(PluginToolApp.Failed, result.Code);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public void Pack_with_an_unknown_option_is_a_usage_error() =>
        Assert.Equal(PluginToolApp.Usage, Run("pack", GoodPlugin(), "--zip", "x").Code);
}
