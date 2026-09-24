using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginManagerTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginsDir;
    private readonly string _sourcesDir;
    private readonly PluginStatusRegistry _status = new();
    private readonly ActionDispatcher _dispatcher = new([], NullLogger<ActionDispatcher>.Instance);
    private readonly VariableCatalog _catalog = new([]);
    private readonly VariableStore _variables = new();
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;

    public PluginManagerTests()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        _sourcesDir = Path.Combine(_root, "sources");
        Directory.CreateDirectory(_pluginsDir);
        Directory.CreateDirectory(_sourcesDir);
    }

    public async Task InitializeAsync()
    {
        _providerHost = new VariableProviderHost([], _variables, NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = new PluginManager(_pluginsDir, "0.1.0", _status, _dispatcher, _catalog, _providerHost, _variables, new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private string NewPluginFolder(string name)
    {
        var dir = Path.Combine(_pluginsDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>A ready-to-install copy of the compiled stub plugin (a real assembly load, not a mock).</summary>
    private string NewStubSource(string id = "stub")
    {
        var dir = Path.Combine(_sourcesDir, id + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var entryAssembly = typeof(StubPlugin.StubPlugin).Assembly.Location;
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(entryAssembly)!, "*.dll"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
        WriteManifest(dir, id: id, entry: Path.GetFileName(entryAssembly));
        return dir;
    }

    [Fact]
    public async Task Start_on_a_missing_folder_lists_nothing()
    {
        var manager = new PluginManager(Path.Combine(_root, "does-not-exist"), "0.1.0", _status, _dispatcher, _catalog, _providerHost, _variables, new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance);

        await manager.StartAsync(CancellationToken.None);

        Assert.Empty(manager.Plugins);
    }

    [Fact]
    public async Task Start_ignores_folders_without_a_manifest()
    {
        NewPluginFolder("NotAPlugin");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Empty(_manager.Plugins);
    }

    [Fact]
    public async Task Start_flags_unparsable_manifest_as_error()
    {
        File.WriteAllText(Path.Combine(NewPluginFolder("Broken"), "plugin.json"), "{ not json");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Error, Assert.Single(_manager.Plugins).Status);
    }

    [Fact]
    public async Task Start_flags_sdk_version_mismatch_as_incompatible()
    {
        WriteManifest(NewPluginFolder("TooNew"), sdkVersion: "^99.0.0");

        await _manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.Incompatible, plugin.Status);
        Assert.Contains("SDK", plugin.Detail);
    }

    [Fact]
    public async Task Start_flags_server_version_below_minimum_as_incompatible()
    {
        WriteManifest(NewPluginFolder("NeedsNewerServer"), minServerVersion: "99.0.0");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Incompatible, Assert.Single(_manager.Plugins).Status);
    }

    [Fact]
    public async Task A_js_plugin_waits_for_approval_of_its_permissions_and_runs_once_they_are_granted()
    {
        WriteJsPlugin(NewPluginFolder("js-one"), "js-one", ["variables", "actions"],
            "host.registerAction({ type: 'js-one.say', name: 'Say', run() { host.variables.set('js-one.said', 1); } });");

        await _manager.StartAsync(CancellationToken.None);

        var waiting = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.NeedsApproval, waiting.Status);
        Assert.Equal(["variables", "actions"], waiting.PendingPermissions);
        Assert.Empty(_dispatcher.Handlers);

        var approved = await _manager.ApproveAsync("js-one");

        Assert.Equal(PluginLoadStatus.Loaded, approved?.Status);
        Assert.Equal("js-one", _manager.GetActionPluginId("js-one.say"));
    }

    [Fact]
    public async Task A_js_plugin_that_asks_for_more_than_was_approved_waits_again()
    {
        var dir = NewPluginFolder("js-two");
        WriteJsPlugin(dir, "js-two", ["variables"], "host.variables.set('js-two.a', 1);");
        await _manager.StartAsync(CancellationToken.None);
        await _manager.ApproveAsync("js-two");

        WriteJsPlugin(dir, "js-two", ["variables", "input"], "host.variables.set('js-two.a', 1);");
        var info = await _manager.ReloadAsync("js-two");

        Assert.Equal(PluginLoadStatus.NeedsApproval, info?.Status);
    }

    [Fact]
    public async Task A_js_plugin_with_an_unknown_permission_is_an_error()
    {
        WriteJsPlugin(NewPluginFolder("js-three"), "js-three", ["root-access"], "");

        await _manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.Error, plugin.Status);
        Assert.Contains("root-access", plugin.Detail);
    }

    [Fact]
    public async Task Uninstalling_a_js_plugin_unloads_it_and_forgets_its_approval()
    {
        WriteJsPlugin(NewPluginFolder("js-four"), "js-four", ["variables"], "host.variables.set('js-four.a', 1);");
        await _manager.StartAsync(CancellationToken.None);
        await _manager.ApproveAsync("js-four");
        Assert.Equal(1.0, _variables.Get("js-four.a"));

        await _manager.UninstallAsync("js-four");

        Assert.Null(_variables.Get("js-four.a"));
        Assert.False(new PluginPermissionStore(_root).IsGranted("js-four", ["variables"]));
    }

    [Fact]
    public async Task Start_flags_missing_entry_file_as_error()
    {
        WriteManifest(NewPluginFolder("NoDll"), entry: "DoesNotExist.dll");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Error, Assert.Single(_manager.Plugins).Status);
    }

    [Fact]
    public async Task Start_flags_duplicate_ids_as_error_on_the_second_one()
    {
        WriteManifest(NewPluginFolder("First"), id: "dupe");
        WriteManifest(NewPluginFolder("Second"), id: "dupe");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(2, _manager.Plugins.Count);
        Assert.Contains(_manager.Plugins, p => p.Status == PluginLoadStatus.Error);
    }

    [Fact]
    public async Task Install_loads_the_plugin_immediately_without_a_restart()
    {
        await _manager.StartAsync(CancellationToken.None);

        var result = await _manager.InstallFromFolderAsync(NewStubSource());

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
        Assert.True(result.Plugin.HasSettings);

        var action = Assert.Single(_dispatcher.Handlers);
        var descriptor = Assert.IsAssignableFrom<IActionDescriptor>(action);
        Assert.Equal("Stub", descriptor.Category);
        Assert.Equal("stub", _manager.GetActionPluginId("stub.action"));
        Assert.NotNull(_manager.GetSettingsPage("stub"));
        Assert.Equal("stub ready", Assert.Single(_status.All, i => i.PluginId == "stub").Text);
        Assert.Contains(_catalog.All, v => v.Name == "stub.value");
        await WaitForAsync(() => _variables.Get("stub.value") is 42.0);
    }

    [Fact]
    public async Task Uninstall_removes_everything_the_plugin_registered_and_deletes_its_folder()
    {
        await _manager.StartAsync(CancellationToken.None);
        await _manager.InstallFromFolderAsync(NewStubSource());
        await WaitForAsync(() => _variables.Get("stub.value") is 42.0);

        var result = await _manager.UninstallAsync("stub");

        Assert.NotNull(result);
        Assert.False(result.Pending);
        Assert.Empty(_dispatcher.Handlers);
        Assert.Empty(_manager.Plugins);
        Assert.Null(_manager.GetSettingsPage("stub"));
        Assert.DoesNotContain(_status.All, i => i.PluginId == "stub");
        Assert.DoesNotContain(_catalog.All, v => v.Name == "stub.value");
        Assert.Null(_variables.Get("stub.value"));
        Assert.False(Directory.Exists(Path.Combine(_pluginsDir, "stub")));
    }

    [Fact]
    public async Task Reload_swaps_the_running_instance_and_keeps_the_registrations()
    {
        await _manager.StartAsync(CancellationToken.None);
        await _manager.InstallFromFolderAsync(NewStubSource());
        var before = Assert.Single(_dispatcher.Handlers);

        var info = await _manager.ReloadAsync("stub");

        Assert.Equal(PluginLoadStatus.Loaded, info?.Status);
        var after = Assert.Single(_dispatcher.Handlers);
        Assert.NotSame(before, after);
        await WaitForAsync(() => _variables.Get("stub.value") is 42.0);
    }

    [Fact]
    public async Task Installing_over_an_existing_plugin_replaces_it()
    {
        await _manager.StartAsync(CancellationToken.None);
        await _manager.InstallFromFolderAsync(NewStubSource());

        var result = await _manager.InstallFromFolderAsync(NewStubSource());

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
        Assert.Single(_manager.Plugins);
        Assert.Single(_dispatcher.Handlers);
    }

    [Fact]
    public async Task An_action_type_clash_fails_the_second_plugin_and_leaves_the_first_intact()
    {
        await _manager.StartAsync(CancellationToken.None);
        await _manager.InstallFromFolderAsync(NewStubSource("stub"));

        var second = await _manager.InstallFromFolderAsync(NewStubSource("stub-two"));

        Assert.Equal(PluginLoadStatus.Error, second.Plugin.Status);
        Assert.Contains("stub.action", second.Plugin.Detail);
        Assert.Equal("stub", _manager.GetActionPluginId("stub.action"));
        Assert.DoesNotContain(_status.All, i => i.PluginId == "stub-two");
    }

    [Fact]
    public async Task Describes_the_plugins_a_profile_needs_and_reports_the_missing_ones()
    {
        await _manager.StartAsync(CancellationToken.None);
        await _manager.InstallFromFolderAsync(NewStubSource());

        var required = _manager.DescribeRequiredPlugins(["stub.action", "core.hotkey", "unknown.thing"]);

        var stub = Assert.Single(required);
        Assert.Equal("stub", stub.Id);
        Assert.Equal(["stub.action"], stub.ActionTypes);

        var manifest = new ProfilePackageManifest(1, "P", DateTimeOffset.UtcNow, "0.2.0",
            [stub, new PackagePluginRef("gone", "Gone", "1.0.0", ["gone.act"])]);
        Assert.Equal(["gone"], _manager.MissingPlugins(manifest).Select(p => p.Id));
        Assert.Empty(_manager.MissingPlugins(null));
    }

    [Fact]
    public async Task Uninstalling_an_unknown_plugin_returns_null()
    {
        await _manager.StartAsync(CancellationToken.None);

        Assert.Null(await _manager.UninstallAsync("nope"));
    }

    private static void WriteJsPlugin(string dir, string id, string[] permissions, string script)
    {
        var list = string.Join(", ", permissions.Select(p => "\"" + p + "\""));
        var manifest = "{ \"id\": \"" + id + "\", \"name\": \"Js\", \"version\": \"1.0.0\", \"sdkVersion\": \"^0.3.0\", "
            + "\"minServerVersion\": \"0.1.0\", \"entry\": \"index.js\", \"kind\": \"js\", \"permissions\": [" + list + "] }";
        File.WriteAllText(Path.Combine(dir, "plugin.json"), manifest);
        File.WriteAllText(Path.Combine(dir, "index.js"), script);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(20);
        Assert.True(condition(), "Condition was not met in time.");
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
