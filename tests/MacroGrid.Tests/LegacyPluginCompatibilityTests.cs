using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>
/// Loads a real plugin binary — <see cref="LegacyPlugin.LegacyPlugin"/>, compiled against the actually-published SDK
/// 0.4.0 NuGet package, from before the server and the SDK shared one version (docs/guides/versioning.md) — on the
/// current server, with the legacy manifest fields (<c>sdkVersion</c>, no <c>macroGrid</c>) it would ship with.
/// <see cref="PluginCompatibilityTests"/> already checks the rule against strings; this proves the rule against a
/// real assembly: that <see cref="PluginLoadContext"/> really hands a 0.4.0.0-referencing plugin the server's own,
/// newer copy of the SDK assembly, and that the plugin actually initializes.
/// </summary>
public sealed class LegacyPluginCompatibilityTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-legacy-plugin-" + Guid.NewGuid().ToString("N"));
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;

    public async Task InitializeAsync()
    {
        var pluginDir = Path.Combine(_root, "plugins", "legacy");
        Directory.CreateDirectory(pluginDir);

        var entryAssembly = typeof(LegacyPlugin.LegacyPlugin).Assembly.Location;
        File.Copy(entryAssembly, Path.Combine(pluginDir, Path.GetFileName(entryAssembly)));

        File.WriteAllText(Path.Combine(pluginDir, "plugin.json"), """
        {
          "id": "legacy",
          "name": "Legacy Plugin",
          "version": "1.0.0",
          "sdkVersion": "^0.4.0",
          "minServerVersion": "0.1.0",
          "entry": "MacroGrid.Tests.LegacyPlugin.dll",
          "kind": "csharp"
        }
        """);

        _providerHost = new VariableProviderHost([], new VariableStore(), NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = new PluginManager(Path.Combine(_root, "plugins"), PluginSdk.Version, new PluginStatusRegistry(),
            new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance), new VariableCatalog([]), _providerHost,
            new VariableStore(), new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance, trustVerifier: TestPluginSigning.Lenient);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_plugin_built_against_sdk_0_4_0_loads_on_the_current_server()
    {
        await _manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.Loaded, plugin.Status);
    }

    /// <summary>The optional tree items (IPluginTreeProvider) are opt-in: a plugin that predates them gets no
    /// chevron in the Plugins tool window, no provider, and no change-log entry.</summary>
    [Fact]
    public async Task A_plugin_without_a_tree_provider_has_no_tree_items()
    {
        await _manager.StartAsync(CancellationToken.None);

        Assert.False(Assert.Single(_manager.Plugins).HasTreeItems);
        Assert.Null(_manager.GetTreeProvider("legacy"));
        Assert.Equal(0, _manager.TreeChanges.Revision);
    }
}
