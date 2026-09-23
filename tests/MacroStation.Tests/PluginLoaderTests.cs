using MacroStation.Core.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroStation.Tests;

public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string NewPluginFolder(string name)
    {
        var dir = Path.Combine(_dir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void LoadAll_on_a_missing_folder_returns_empty()
    {
        var result = PluginLoader.LoadAll(Path.Combine(_dir, "does-not-exist"), "0.1.0", NullLogger.Instance);

        Assert.Empty(result.Plugins);
    }

    [Fact]
    public void LoadAll_ignores_folders_without_a_manifest()
    {
        NewPluginFolder("NotAPlugin");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        Assert.Empty(result.Plugins);
    }

    [Fact]
    public void LoadAll_flags_unparsable_manifest_as_error()
    {
        var dir = NewPluginFolder("Broken");
        File.WriteAllText(Path.Combine(dir, "plugin.json"), "{ not json");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Error, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_sdk_version_mismatch_as_incompatible()
    {
        var dir = NewPluginFolder("TooNew");
        WriteManifest(dir, sdkVersion: "^99.0.0");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
        Assert.Contains("SDK", plugin.Detail);
    }

    [Fact]
    public void LoadAll_flags_server_version_below_minimum_as_incompatible()
    {
        var dir = NewPluginFolder("NeedsNewerServer");
        WriteManifest(dir, minServerVersion: "99.0.0");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_js_plugins_as_incompatible_until_the_runtime_exists()
    {
        var dir = NewPluginFolder("JsOne");
        WriteManifest(dir, kind: "js", entry: "index.js");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_missing_entry_file_as_error()
    {
        var dir = NewPluginFolder("NoDll");
        WriteManifest(dir, entry: "DoesNotExist.dll");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Error, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_duplicate_ids_as_error_on_the_second_one()
    {
        var dir1 = NewPluginFolder("First");
        WriteManifest(dir1, id: "dupe");
        var dir2 = NewPluginFolder("Second");
        WriteManifest(dir2, id: "dupe");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", NullLogger.Instance);

        Assert.Equal(2, result.Plugins.Count);
        Assert.Contains(result.Plugins, p => p.Status == PluginLoadStatus.Error);
    }

    private static void WriteManifest(string dir, string? id = null, string sdkVersion = "^0.1.0", string minServerVersion = "0.1.0", string kind = "csharp", string entry = "Plugin.dll")
    {
        var json = $$"""
        {
          "id": "{{id ?? Path.GetFileName(dir)}}",
          "name": "Test Plugin",
          "version": "1.0.0",
          "sdkVersion": "{{sdkVersion}}",
          "minServerVersion": "{{minServerVersion}}",
          "entry": "{{entry}}",
          "kind": "{{kind}}"
        }
        """;
        File.WriteAllText(Path.Combine(dir, "plugin.json"), json);
    }
}
