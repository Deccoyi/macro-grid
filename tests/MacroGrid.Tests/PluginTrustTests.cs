using System.Net;
using System.Security.Cryptography;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Variables;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>Only official, signed C# plugins run: the verifier, the loader and every install path. The tests sign with a
/// throwaway key injected in place of the official one.</summary>
public sealed class PluginTrustTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-trust-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginsDir;
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;
    private readonly PluginStatusRegistry _status = new();

    public PluginTrustTests()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(_pluginsDir);
    }

    public async Task InitializeAsync()
    {
        var variables = new VariableStore();
        _providerHost = new VariableProviderHost([], variables, NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = NewManager(TestPluginSigning.Strict);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private PluginManager NewManager(PluginTrustVerifier verifier) =>
        new(_pluginsDir, "1.0.0", _status, new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), _providerHost, new VariableStore(), new PluginPermissionStore(_root), null,
            NullLogger<PluginManager>.Instance, trustVerifier: verifier);

    /// <summary>A copy of the compiled stub plugin (a real assembly load), signed with the test key unless told otherwise.</summary>
    private string NewStub(string id = "stub", bool sign = true)
    {
        var dir = Path.Combine(_root, "src-" + id + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var entryAssembly = typeof(StubPlugin.StubPlugin).Assembly.Location;
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(entryAssembly)!, "*.dll"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), $$"""
        { "id": "{{id}}", "name": "Stub", "version": "1.0.0", "minMacroGrid": "1.0.0", "entry": "{{Path.GetFileName(entryAssembly)}}", "kind": "csharp" }
        """);
        if (sign) TestPluginSigning.Sign(dir);
        return dir;
    }

    private static PluginTrustResult Verify(string dir, PluginTrustVerifier? verifier = null) =>
        (verifier ?? TestPluginSigning.Strict).Verify(dir, PluginManager.PeekManifest(dir));

    [Fact]
    public void A_valid_signed_package_is_trusted()
    {
        var result = Verify(NewStub());

        Assert.True(result.Allowed);
        Assert.False(result.Unsigned);
        Assert.Contains("plugin.json", result.SignedFiles!.Keys);
    }

    [Fact]
    public void A_package_without_a_signature_is_not_allowed()
    {
        var result = Verify(NewStub(sign: false));

        Assert.False(result.Allowed);
        Assert.Equal(PluginTrustVerifier.NotOfficialReason, result.Reason);
    }

    [Fact]
    public void A_signature_by_another_key_is_not_allowed()
    {
        var dir = NewStub(sign: false);
        using var other = TestPluginSigning.NewOtherKey();
        TestPluginSigning.Sign(dir, key: other);

        Assert.False(Verify(dir).Allowed);
    }

    [Fact]
    public void A_changed_file_is_not_allowed()
    {
        var dir = NewStub();
        var dll = Directory.GetFiles(dir, "MacroGrid.Tests.StubPlugin.dll").Single();
        File.AppendAllText(dll, "x");

        var result = Verify(dir);

        Assert.False(result.Allowed);
        Assert.Equal(PluginTrustVerifier.MismatchReason, result.Reason);
    }

    [Fact]
    public void A_missing_file_is_not_allowed()
    {
        var dir = NewStub();
        File.WriteAllText(Path.Combine(dir, "data.txt"), "a");
        TestPluginSigning.Sign(dir);
        File.Delete(Path.Combine(dir, "data.txt"));

        Assert.False(Verify(dir).Allowed);
    }

    [Theory]
    [InlineData("extra.dll")]
    [InlineData("run.ps1")]
    [InlineData("sub/tool.exe")]
    [InlineData("payload.js")]
    public void An_unlisted_loadable_file_is_not_allowed(string name)
    {
        var dir = NewStub();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, name))!);
        File.WriteAllText(Path.Combine(dir, name), "x");

        Assert.False(Verify(dir).Allowed);
    }

    [Fact]
    public void An_unlisted_data_file_is_fine()
    {
        var dir = NewStub();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{}");

        Assert.True(Verify(dir).Allowed);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("version")]
    [InlineData("kind")]
    public void A_signature_for_another_id_version_or_kind_is_not_allowed(string field)
    {
        var dir = NewStub(sign: false);
        TestPluginSigning.Sign(dir,
            id: field == "id" ? "other" : null,
            version: field == "version" ? "9.9.9" : null,
            kind: field == "kind" ? "js" : null);

        var result = Verify(dir);

        Assert.False(result.Allowed);
        Assert.Equal(PluginTrustVerifier.MismatchReason, result.Reason);
    }

    [Fact]
    public void A_development_build_lets_an_unsigned_plugin_through_and_marks_it()
    {
        var result = Verify(NewStub(sign: false), TestPluginSigning.Lenient);

        Assert.True(result.Allowed);
        Assert.True(result.Unsigned);
        Assert.Null(result.SignedFiles);
    }

    [Fact]
    public void Unsigned_loading_is_only_ever_on_in_a_Debug_build()
    {
#if !DEBUG
        Assert.False(PluginTrustVerifier.DevelopmentBuild);
#endif
        Assert.Equal(PluginTrustVerifier.DevelopmentBuild, PluginTrustVerifier.Official.AllowsUnsigned);
    }

    [Fact]
    public async Task A_signed_plugin_loads_at_start()
    {
        var dir = NewStub();
        CopyTo(dir, Path.Combine(_pluginsDir, "stub"));

        await _manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.Loaded, plugin.Status);
        Assert.False(plugin.Unsigned);
    }

    [Fact]
    public async Task An_unsigned_plugin_is_not_loaded_and_gets_NotAllowed()
    {
        CopyTo(NewStub(sign: false), Path.Combine(_pluginsDir, "stub"));

        await _manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.NotAllowed, plugin.Status);
        Assert.Equal(PluginTrustVerifier.NotOfficialReason, plugin.Detail);
        Assert.Null(_manager.GetSettingsPage("stub"));
    }

    [Fact]
    public async Task A_file_changed_after_install_is_caught_at_the_next_load()
    {
        var installed = await _manager.InstallFromFolderAsync(NewStub());
        Assert.Equal(PluginLoadStatus.Loaded, installed.Plugin.Status);

        File.AppendAllText(Path.Combine(_pluginsDir, "stub", "MacroGrid.Tests.StubPlugin.dll"), "x");
        var reloaded = await _manager.ReloadAsync("stub");

        Assert.Equal(PluginLoadStatus.NotAllowed, reloaded!.Status);
        Assert.Equal(PluginTrustVerifier.MismatchReason, reloaded.Detail);
    }

    [Fact]
    public async Task A_development_build_loads_an_unsigned_plugin_and_flags_it()
    {
        CopyTo(NewStub(sign: false), Path.Combine(_pluginsDir, "stub"));
        var manager = NewManager(TestPluginSigning.Lenient);

        await manager.StartAsync(CancellationToken.None);

        var plugin = Assert.Single(manager.Plugins);
        Assert.Equal(PluginLoadStatus.Loaded, plugin.Status);
        Assert.True(plugin.Unsigned);
        var notice = Assert.Single(_status.All, i => i.Id == "unsigned-plugins");
        Assert.Equal(MacroGrid.Plugin.Abstractions.StatusLevel.Error, notice.Level);

        await manager.UninstallAsync("stub");
        Assert.DoesNotContain(_status.All, i => i.Id == "unsigned-plugins");
        await manager.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Local_folder_install_refuses_an_unsigned_C_sharp_plugin_and_copies_nothing()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _manager.InstallFromFolderAsync(NewStub(sign: false)));

        Assert.Equal("Only official C# plugins can be installed.", ex.Message);
        Assert.False(Directory.Exists(Path.Combine(_pluginsDir, "stub")));
    }

    [Fact]
    public async Task Local_folder_install_accepts_a_signed_copy_of_an_official_plugin()
    {
        var result = await _manager.InstallFromFolderAsync(NewStub());

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task Local_folder_install_still_accepts_a_JavaScript_plugin()
    {
        var dir = Path.Combine(_root, "src-js");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), """
        { "id": "js-demo", "name": "Js", "version": "1.0.0", "minMacroGrid": "1.0.0", "entry": "index.js", "kind": "js" }
        """);
        File.WriteAllText(Path.Combine(dir, "index.js"), "// nothing");

        var result = await _manager.InstallFromFolderAsync(dir);

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task A_third_party_source_cannot_install_a_C_sharp_plugin_and_nothing_is_downloaded()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var installer = new PluginCatalogInstaller(new PluginPackageDownloader(new HttpClient(handler)), _manager,
            new PluginInstallOriginStore(_root), Path.Combine(_root, "staging"));
        var version = new PluginCatalogVersion("1.0.0", "1.0.0", null, null, null,
            "https://github.com/someone/their-plugins/releases/download/v1/x.zip", new string('a', 64), 10, null, null);
        var entry = new PluginCatalogEntry("x", "X", null, null, null, "csharp", [version]);

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            installer.InstallAsync(entry, version, "https://github.com/someone/their-plugins", isOfficial: false, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Refused, ex.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void The_loader_only_loads_files_the_signature_lists_and_checks_their_hash_again()
    {
        var dir = NewStub();
        var entry = Path.Combine(dir, "MacroGrid.Tests.StubPlugin.dll");
        var good = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry))).ToLowerInvariant();

        var listed = new PluginLoadContext("t1", entry, new Dictionary<string, string> { ["MacroGrid.Tests.StubPlugin.dll"] = good }, dir);
        Assert.NotNull(listed.LoadPluginAssembly(entry));
        listed.Unload();

        var unlisted = new PluginLoadContext("t2", entry, new Dictionary<string, string>(), dir);
        Assert.Throws<InvalidOperationException>(() => unlisted.LoadPluginAssembly(entry));
        unlisted.Unload();

        var swapped = new PluginLoadContext("t3", entry, new Dictionary<string, string> { ["MacroGrid.Tests.StubPlugin.dll"] = new string('0', 64) }, dir);
        Assert.Throws<InvalidOperationException>(() => swapped.LoadPluginAssembly(entry));
        swapped.Unload();
    }

    private static void CopyTo(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
