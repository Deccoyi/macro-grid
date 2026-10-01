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
}
