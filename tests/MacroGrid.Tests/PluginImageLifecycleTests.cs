using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Images;
using MacroGrid.Core.Variables;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginImageLifecycleTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-imglife-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTime _time = new();
    private PluginImageService _images = null!;
    private PluginManager _manager = null!;
    private VariableProviderHost _providerHost = null!;
    private string _pluginsDir = null!;

    public async Task InitializeAsync()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(_pluginsDir);
        var variables = new VariableStore();
        _images = new PluginImageService(NullLogger<PluginImageService>.Instance, null, _time);
        _providerHost = new VariableProviderHost([], variables, NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = new PluginManager(_pluginsDir, "1.0.0", new PluginStatusRegistry(), new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), _providerHost, variables, new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance,
            trustVerifier: TestPluginSigning.Lenient, images: _images);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        _images.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Source(string withCache = "")
    {
        var dir = Path.Combine(_root, "src-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), """{ "id": "vm", "name": "Voice", "version": "1.0.0", "minMacroGrid": "1.0.0", "entry": "index.js", "kind": "js" }""");
        File.WriteAllText(Path.Combine(dir, "index.js"), "host.log('hi');");
        if (withCache.Length > 0)
        {
            Directory.CreateDirectory(Path.Combine(dir, PluginImageCache.FolderName));
            File.WriteAllText(Path.Combine(dir, PluginImageCache.FolderName, "x.bin"), withCache);
        }
        return dir;
    }

    [Fact]
    public async Task Uninstall_deletes_the_cache_and_forgets_the_plugin()
    {
        await _manager.InstallFromFolderAsync(Source());
        _images.RequestImage("vm", "a");
        Assert.True(_images.Put("vm", "a", TestPictures.PngUrl()));
        var cache = Path.Combine(_pluginsDir, "vm", PluginImageCache.FolderName);
        Assert.True(Directory.Exists(cache));

        await _manager.UninstallAsync("vm");

        Assert.False(Directory.Exists(cache));
        Assert.False(_images.IsKnown("vm"));
    }

    [Fact]
    public async Task A_plugin_update_keeps_the_cache_and_never_copies_one_from_the_source_folder()
    {
        await _manager.InstallFromFolderAsync(Source());
        _images.RequestImage("vm", "a");
        _images.Put("vm", "a", TestPictures.PngUrl());

        await _manager.InstallFromFolderAsync(Source(withCache: "foreign"));

        Assert.Equal(TestPictures.PngUrl(), _images.TryGetDataUrl("vm", "a"));
        Assert.Single(Directory.GetFiles(Path.Combine(_pluginsDir, "vm", PluginImageCache.FolderName), "*.bin"));
    }

    [Fact]
    public async Task A_switched_off_plugin_still_serves_its_cached_pictures()
    {
        await _manager.InstallFromFolderAsync(Source());
        _images.RequestImage("vm", "a");
        _images.Put("vm", "a", TestPictures.PngUrl());

        await _manager.SetEnabledAsync("vm", false);

        Assert.Equal(TestPictures.PngUrl(), _images.TryGetDataUrl("vm", "a"));
    }
}
