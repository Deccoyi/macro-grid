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
            "{ \"id\": \"demo.one\", \"name\": \"Demo\", \"version\": \"1.2.3\", \"minMacroGrid\": \"1.0.0\", \"entry\": \"index.js\", \"kind\": \"js\", \"permissions\": [\"variables\", \"actions\"] }");
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

    [Fact]
    public void New_creates_a_plugin_that_validates_without_errors()
    {
        var result = Run("new", "my-first", "--name", "My \"first\" plugin", "--out", _root);

        Assert.Equal(PluginToolApp.Ok, result.Code);
        var dir = Path.Combine(_root, "my-first");
        Assert.True(File.Exists(Path.Combine(dir, "index.js")));
        Assert.Contains("my-first.bump", File.ReadAllText(Path.Combine(dir, "index.js")));
        Assert.Equal(0, Run("validate", dir).Code);
    }

    [Fact]
    public void New_refuses_a_bad_id_and_a_folder_that_is_in_use()
    {
        Assert.Equal(PluginToolApp.Failed, Run("new", "bad id", "--out", _root).Code);

        Assert.Equal(PluginToolApp.Ok, Run("new", "twice", "--out", _root).Code);
        var second = Run("new", "twice", "--out", _root);
        Assert.Equal(PluginToolApp.Failed, second.Code);
        Assert.Contains("already exists", second.Err);
    }

    private const string CounterScript = """
        let count = 0;
        host.variables.set('demo.one.count', count);
        host.registerAction({ type: 'demo.one.bump', name: 'Bump', run(ctx, s) { count += (s.by || 1); host.variables.set('demo.one.count', count); } });
        host.status('main', 'ready', 'Ok');
        """;

    private static string[] DataFolders() =>
        [.. Directory.GetDirectories(Path.GetTempPath(), "macrogrid-plugin-*")];

    [Fact]
    public void Run_lists_the_actions_variables_and_status()
    {
        var result = Run("run", GoodPlugin(script: CounterScript));

        Assert.Equal(PluginToolApp.Ok, result.Code);
        Assert.Contains("permissions: variables", result.Out);
        Assert.Contains("action: demo.one.bump (Bump)", result.Out);
        Assert.Contains("status: main [Ok] ready", result.Out);
        Assert.Contains("variable: demo.one.count = 0", result.Out);
    }

    [Fact]
    public void Run_with_an_action_changes_the_variable()
    {
        var result = Run("run", GoodPlugin(script: CounterScript, name: "act"), "--action", "demo.one.bump", "--settings", "{\"by\": 5}");

        Assert.Equal(PluginToolApp.Ok, result.Code);
        Assert.Contains("ran: demo.one.bump", result.Out);
        Assert.Contains("variable: demo.one.count = 5", result.Out);
    }

    [Fact]
    public void Run_with_an_unknown_action_fails()
    {
        var result = Run("run", GoodPlugin(script: CounterScript, name: "unk"), "--action", "demo.one.nope");

        Assert.Equal(PluginToolApp.Failed, result.Code);
        Assert.Contains("no action", result.Err);
    }

    [Fact]
    public void Run_with_a_permission_denied_exits_one()
    {
        var dir = GoodPlugin(script: "host.registerAction({ type: 'demo.one.a', run() {} });", name: "deny");
        File.WriteAllText(Path.Combine(dir, "plugin.json"),
            "{ \"id\": \"demo.one\", \"name\": \"Demo\", \"version\": \"1.2.3\", \"minMacroGrid\": \"1.0.0\", \"entry\": \"index.js\", \"kind\": \"js\", \"permissions\": [\"variables\", \"actions\"] }");

        var result = Run("run", dir, "--deny", "actions");

        Assert.Equal(PluginToolApp.Failed, result.Code);
        Assert.Contains("did not start", result.Err);
    }

    [Fact]
    public void Run_refuses_the_servers_own_port_and_leaves_no_data_folder()
    {
        var before = DataFolders().Length;
        var dir = GoodPlugin(name: "net", script: "host.registerAction({ type: 'demo.one.call', run() { try { host.http.get('http://localhost:9820/'); } catch (e) { host.variables.set('demo.one.refused', e.message); } } });");
        File.WriteAllText(Path.Combine(dir, "plugin.json"),
            "{ \"id\": \"demo.one\", \"name\": \"Demo\", \"version\": \"1.2.3\", \"minMacroGrid\": \"1.0.0\", \"entry\": \"index.js\", \"kind\": \"js\", \"permissions\": [\"variables\", \"actions\", \"http:localhost:9820\"] }");

        var result = Run("run", dir, "--action", "demo.one.call");

        Assert.Equal(PluginToolApp.Ok, result.Code);
        Assert.Contains("variable: demo.one.refused", result.Out);
        Assert.Equal(before, DataFolders().Length);
    }

    [Fact]
    public void Run_of_a_folder_with_errors_does_not_start_it()
    {
        var result = Run("run", GoodPlugin(script: "function (", name: "bad"));

        Assert.Equal(PluginToolApp.Failed, result.Code);
        Assert.DoesNotContain("permissions:", result.Out);
    }
}
