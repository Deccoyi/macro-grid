using MacroGrid.Core.Plugins;

namespace MacroGrid.Tests;

public sealed class PluginFolderValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-validator-" + Guid.NewGuid().ToString("N"));

    public PluginFolderValidatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string Folder(string manifestJson, string script = "host.log('hi');", string name = "plugin")
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), manifestJson);
        File.WriteAllText(Path.Combine(dir, "index.js"), script);
        File.WriteAllText(Path.Combine(dir, "LICENSE"), "MIT");
        return dir;
    }

    private static string Manifest(string id = "demo.one", string version = "1.0.0", string extra = "\"minMacroGrid\": \"1.0.0\", \"permissions\": [\"variables\"]") =>
        "{ \"id\": \"" + id + "\", \"name\": \"Demo\", \"version\": \"" + version + "\", \"entry\": \"index.js\", \"kind\": \"js\"" + (extra.Length > 0 ? ", " + extra : "") + " }";

    private static PluginFolderReport Check(string dir) => PluginFolderValidator.Validate(dir, "1.0.0");

    [Fact]
    public void A_good_folder_has_no_findings()
    {
        var report = Check(Folder(Manifest()));

        Assert.Empty(report.Findings);
        Assert.Equal("demo.one", report.Manifest?.Id);
    }

    [Fact]
    public void A_missing_folder_is_an_error()
    {
        var report = Check(Path.Combine(_root, "nope"));

        Assert.True(report.HasErrors);
        Assert.Null(report.Manifest);
    }

    [Fact]
    public void A_missing_or_broken_manifest_is_an_error()
    {
        var dir = Path.Combine(_root, "empty");
        Directory.CreateDirectory(dir);
        Assert.Contains("plugin.json not found", Check(dir).Findings.Single().Message);

        Assert.Contains("could not be read", Check(Folder("{ not json", name: "broken")).Findings.Single().Message);
    }

    [Theory]
    [InlineData("bad id")]
    [InlineData("user")]
    [InlineData("con")]
    [InlineData("ends.")]
    public void An_id_the_installer_refuses_is_an_error(string id) =>
        Assert.Contains(Check(Folder(Manifest(id: id))).Findings, f => f.Level == PluginFindingLevel.Error && f.Message.Contains("id"));

    [Fact]
    public void A_version_that_is_not_three_parts_is_an_error() =>
        Assert.Contains(Check(Folder(Manifest(version: "1.0"))).Findings, f => f.Level == PluginFindingLevel.Error && f.Message.Contains("version"));

    [Fact]
    public void A_missing_or_too_new_minimum_version_is_an_error()
    {
        Assert.Contains("minMacroGrid", Check(Folder(Manifest(extra: ""), name: "a")).Findings.Single().Message);
        Assert.Contains("2.0.0", Check(Folder(Manifest(extra: "\"minMacroGrid\": \"2.0.0\""), name: "b")).Findings.Single().Message);
    }

    [Fact]
    public void A_legacy_minimum_field_is_a_warning_only()
    {
        var report = Check(Folder(Manifest(extra: "\"macroGrid\": \"1.0.0\""), name: "legacy"));

        var finding = Assert.Single(report.Findings);
        Assert.Equal(PluginFindingLevel.Warning, finding.Level);
    }

    [Theory]
    [InlineData("../outside.js")]
    [InlineData("missing.js")]
    public void An_entry_outside_the_folder_or_missing_is_an_error(string entry)
    {
        var json = Manifest().Replace("index.js", entry);

        Assert.Contains(Check(Folder(json, name: "e")).Findings, f => f.Level == PluginFindingLevel.Error && f.Message.StartsWith("Entry file not found"));
    }

    [Fact]
    public void A_script_that_does_not_parse_is_an_error() =>
        Assert.Contains(Check(Folder(Manifest(), script: "function (", name: "s")).Findings, f => f.Level == PluginFindingLevel.Error && f.Message.Contains("does not parse"));

    [Fact]
    public void Permissions_are_checked_like_the_loader_does()
    {
        var unknown = Check(Folder(Manifest(extra: "\"minMacroGrid\": \"1.0.0\", \"permissions\": [\"root-access\"]"), name: "p1"));
        Assert.Contains("Unknown permission 'root-access'", Assert.Single(unknown.Findings).Message);

        var twice = Check(Folder(Manifest(extra: "\"minMacroGrid\": \"1.0.0\", \"permissions\": [\"variables\", \"Variables\"]"), name: "p2"));
        Assert.Equal(PluginFindingLevel.Warning, Assert.Single(twice.Findings).Level);

        var port = Check(Folder(Manifest(extra: "\"minMacroGrid\": \"1.0.0\", \"permissions\": [\"http:localhost:70000\"]"), name: "p3"));
        Assert.Contains("port", Assert.Single(port.Findings).Message);
    }

    [Fact]
    public void A_bad_widget_is_an_error()
    {
        var json = Manifest(extra: "\"minMacroGrid\": \"1.0.0\", \"widgets\": [ { \"id\": \"bad id\", \"name\": \"W\", \"entry\": \"w.js\" } ]");

        Assert.Contains(Check(Folder(json, name: "w")).Findings, f => f.Level == PluginFindingLevel.Error && f.Message.StartsWith("Widget "));
    }

    [Fact]
    public void An_ignored_icon_and_a_broken_locale_are_warnings()
    {
        var dir = Folder(Manifest(extra: "\"minMacroGrid\": \"1.0.0\", \"icon\": \"missing.svg\""), name: "i");
        Directory.CreateDirectory(Path.Combine(dir, "locales"));
        File.WriteAllText(Path.Combine(dir, "locales", "tr.json"), "[1]");

        var report = Check(dir);

        Assert.Equal(2, report.Findings.Count);
        Assert.All(report.Findings, f => Assert.Equal(PluginFindingLevel.Warning, f.Level));
    }

    [Fact]
    public void Saved_data_and_a_missing_license_are_warnings()
    {
        var dir = Folder(Manifest(), name: "d");
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{}");
        File.Delete(Path.Combine(dir, "LICENSE"));

        var report = Check(dir);

        Assert.Equal(2, report.Findings.Count);
        Assert.False(report.HasErrors);
    }

    [Fact]
    public void An_unknown_extra_manifest_field_is_accepted_by_the_loader_and_the_check()
    {
        var dir = Folder(Manifest(extra: "\"$schema\": \"https://example.invalid/plugin.schema.json\", \"minMacroGrid\": \"1.0.0\""), name: "x");

        Assert.Empty(Check(dir).Findings);
        Assert.Equal("demo.one", PluginManager.PeekManifest(dir).Id);
    }
}
