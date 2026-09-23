using MacroStation.Core.Plugins;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroStation.Tests;

public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        // A loaded plugin's DLL stays memory-mapped by its (collectible but not yet GC'd)
        // AssemblyLoadContext for a moment after LoadAll returns — best-effort cleanup only.
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
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
        var result = PluginLoader.LoadAll(Path.Combine(_dir, "does-not-exist"), "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        Assert.Empty(result.Plugins);
    }

    [Fact]
    public void LoadAll_ignores_folders_without_a_manifest()
    {
        NewPluginFolder("NotAPlugin");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        Assert.Empty(result.Plugins);
    }

    [Fact]
    public void LoadAll_flags_unparsable_manifest_as_error()
    {
        var dir = NewPluginFolder("Broken");
        File.WriteAllText(Path.Combine(dir, "plugin.json"), "{ not json");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Error, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_sdk_version_mismatch_as_incompatible()
    {
        var dir = NewPluginFolder("TooNew");
        WriteManifest(dir, sdkVersion: "^99.0.0");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
        Assert.Contains("SDK", plugin.Detail);
    }

    [Fact]
    public void LoadAll_flags_server_version_below_minimum_as_incompatible()
    {
        var dir = NewPluginFolder("NeedsNewerServer");
        WriteManifest(dir, minServerVersion: "99.0.0");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_js_plugins_as_incompatible_until_the_runtime_exists()
    {
        var dir = NewPluginFolder("JsOne");
        WriteManifest(dir, kind: "js", entry: "index.js");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
    }

    [Fact]
    public void LoadAll_flags_missing_entry_file_as_error()
    {
        var dir = NewPluginFolder("NoDll");
        WriteManifest(dir, entry: "DoesNotExist.dll");

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

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

        var result = PluginLoader.LoadAll(_dir, "0.1.0", new PluginStatusRegistry(), NullLogger.Instance);

        Assert.Equal(2, result.Plugins.Count);
        Assert.Contains(result.Plugins, p => p.Status == PluginLoadStatus.Error);
    }

    [Fact]
    public void LoadAll_collects_a_plugins_described_action_settings_page_and_status_item()
    {
        var dir = NewPluginFolder("Stub");
        var entryAssembly = typeof(StubPlugin.StubPlugin).Assembly.Location;
        var entryName = Path.GetFileName(entryAssembly);
        CopyBuildOutput(Path.GetDirectoryName(entryAssembly)!, dir);
        WriteManifest(dir, id: "stub", entry: entryName);

        var statusRegistry = new PluginStatusRegistry();
        var result = PluginLoader.LoadAll(_dir, "0.1.0", statusRegistry, NullLogger.Instance);

        var plugin = Assert.Single(result.Plugins);
        Assert.Equal(PluginLoadStatus.Loaded, plugin.Status);
        Assert.True(plugin.HasSettings);

        var action = Assert.Single(result.Actions);
        var descriptor = Assert.IsAssignableFrom<IActionDescriptor>(action);
        Assert.Equal("Stub", descriptor.Category);
        Assert.Single(descriptor.Fields);

        Assert.True(result.SettingsPages.ContainsKey("stub"));
        Assert.Equal("stub", result.ActionPluginIds["stub.action"]);

        var statusItem = Assert.Single(statusRegistry.All, i => i.PluginId == "stub");
        Assert.Equal("stub hazır", statusItem.Text);
    }

    private static void CopyBuildOutput(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*.dll"))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
    }

    private static void WriteManifest(string dir, string? id = null, string sdkVersion = "^0.3.0", string minServerVersion = "0.1.0", string kind = "csharp", string entry = "Plugin.dll")
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
