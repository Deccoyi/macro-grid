using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class DataDirReportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-report-" + Guid.NewGuid().ToString("N"));

    public DataDirReportTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "profiles"));
        Directory.CreateDirectory(Path.Combine(_dir, "plugins"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void A_profile_file_that_is_not_in_the_list_is_a_problem()
    {
        File.WriteAllText(Path.Combine(_dir, "profiles", "a.json"), "{ \"id\": \"a\" }");
        File.WriteAllText(Path.Combine(_dir, "profiles", "b.json"), "{ \"id\": \"b\" }");

        var lines = DataDirReport.CompareProfiles(_dir, ["a"]);

        Assert.Contains(lines, l => !l.Problem && l.Text.Contains("a.json is loaded"));
        Assert.Contains(lines, l => l.Problem && l.Text.Contains("b.json") && l.Text.Contains("NOT in the profile list"));
        Assert.Contains(lines, l => l.Text == "Profiles: 2 file(s) on disk, 1 in the profile list");
    }

    [Fact]
    public void A_profile_file_that_cannot_be_parsed_is_a_problem()
    {
        File.WriteAllText(Path.Combine(_dir, "profiles", "broken.json"), "{ not json");

        var lines = DataDirReport.CompareProfiles(_dir, []);

        Assert.Contains(lines, l => l.Problem && l.Text.Contains("broken.json") && l.Text.Contains("cannot be read"));
    }

    [Fact]
    public void A_plugin_folder_that_is_not_in_the_list_is_a_problem_and_a_failed_one_is_too()
    {
        foreach (var id in new[] { "obs", "sound", "lost" })
        {
            Directory.CreateDirectory(Path.Combine(_dir, "plugins", id));
            File.WriteAllText(Path.Combine(_dir, "plugins", id, "plugin.json"), "{}");
        }
        Directory.CreateDirectory(Path.Combine(_dir, "plugins", "no-manifest"));

        var lines = DataDirReport.ComparePlugins(_dir, new Dictionary<string, string> { ["obs"] = "Loaded", ["sound"] = "Error" });

        Assert.Contains(lines, l => !l.Problem && l.Text == "Plugin folder obs: Loaded");
        Assert.Contains(lines, l => l.Problem && l.Text == "Plugin folder sound: Error");
        Assert.Contains(lines, l => l.Problem && l.Text.Contains("lost") && l.Text.Contains("NOT in the plugin list"));
        Assert.DoesNotContain(lines, l => l.Text.Contains("no-manifest"));
        Assert.Contains(lines, l => l.Text == "Plugins: 3 folder(s) with a plugin.json, 2 in the plugin list");
    }

    [Fact]
    public void The_inventory_lists_the_folders_and_flags_a_missing_data_folder()
    {
        File.WriteAllText(Path.Combine(_dir, "profiles", "a.json"), "{}");

        var lines = DataDirReport.Inventory(_dir);
        Assert.Contains(lines, l => l.Text.Contains("Folder 'profiles'") && l.Text.Contains("a.json (2 B"));

        var missing = DataDirReport.Inventory(Path.Combine(_dir, "nope"));
        Assert.Single(missing);
        Assert.True(missing[0].Problem);
    }

    [Fact]
    public void The_write_probe_passes_in_a_writable_folder_and_reports_a_missing_one()
    {
        Assert.Null(DataDirReport.WriteProbe(_dir));
        Assert.Empty(Directory.GetFiles(_dir, ".write-probe-*"));
        Assert.NotNull(DataDirReport.WriteProbe(Path.Combine(_dir, "does", "not", "exist")));
    }
}
