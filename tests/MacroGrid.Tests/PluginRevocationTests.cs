using System.Net;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Variables;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>The official safety list at load time, while running and before an install. A list that is missing or could not be
/// fetched never switches anything off.</summary>
public sealed class PluginRevocationTests : IAsyncLifetime
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-revoked-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginsDir;
    private readonly ManualTime _clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly PluginInstallOriginStore _origins;
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;
    private OfficialCatalog _catalog = null!;
    private FakeHttpHandler _handler = null!;
    private byte[]? _listBody;
    private long _sequence;

    public PluginRevocationTests()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(_pluginsDir);
        _origins = new PluginInstallOriginStore(_root);
    }

    public async Task InitializeAsync()
    {
        _providerHost = new VariableProviderHost([], new VariableStore(), NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _handler = new FakeHttpHandler(_ => _listBody is { } body
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(_handler);
        _catalog = new OfficialCatalog(http, new PluginCatalogStateStore(_root), new PluginCatalogClient(http, _clock), _clock, TestPluginSigning.PublicKey, new Random(1));
        _manager = new PluginManager(_pluginsDir, "1.0.0", new PluginStatusRegistry(), new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), _providerHost, new VariableStore(), new PluginPermissionStore(_root), null,
            NullLogger<PluginManager>.Instance, trustVerifier: TestPluginSigning.Strict, officialCatalog: _catalog, origins: _origins);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Makes the safety list name these plugins (empty = a list that names nothing) and fetches it.</summary>
    private async Task PublishListAsync(params (string Id, string Version)[] revoked)
    {
        var plugins = string.Join(",", revoked.Select(r => $$"""{ "id": "{{r.Id}}", "versions": ["{{r.Version}}"], "reason": "Unsafe build." }"""));
        _listBody = TestPluginSigning.SignEnvelope($$"""{ "kind": "revoked", "sequence": {{++_sequence}}, "formatVersion": 1, "plugins": [ {{plugins}} ] }""");
        _clock.Advance(TimeSpan.FromMinutes(5));
        Assert.NotNull(await _catalog.GetRevokedAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true));
    }

    private string NewJs(string id, string version = "1.0.0")
    {
        var dir = Path.Combine(_root, "src-" + id + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), $$"""
        { "id": "{{id}}", "name": "Js", "version": "{{version}}", "minMacroGrid": "1.0.0", "entry": "index.js", "kind": "js" }
        """);
        File.WriteAllText(Path.Combine(dir, "index.js"), "// nothing");
        return dir;
    }

    private string NewSignedCsharp(string id)
    {
        var dir = Path.Combine(_root, "src-" + id + "-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(dir);
        var assembly = typeof(StubPlugin.StubPlugin).Assembly.Location;
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(assembly)!, "*.dll"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), $$"""
        { "id": "{{id}}", "name": "Stub", "version": "1.0.0", "minMacroGrid": "1.0.0", "entry": "{{Path.GetFileName(assembly)}}", "kind": "csharp" }
        """);
        TestPluginSigning.Sign(dir);
        return dir;
    }

    private void FromOfficial(string id) => _origins.Set(id, new PluginInstallOrigin("https://github.com/Deccoyi/macro-grid-plugin", "1.0.0", PluginTrust.Official));

    [Fact]
    public async Task An_official_plugin_on_the_list_does_not_run_and_the_reason_is_shown()
    {
        await PublishListAsync(("demo", "1.0.0"));
        FromOfficial("demo");

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"));

        Assert.Equal(PluginLoadStatus.NotAllowed, result.Plugin.Status);
        Assert.Contains("Unsafe build.", result.Plugin.Detail);
    }

    [Fact]
    public async Task A_plugin_with_no_official_origin_is_not_covered()
    {
        await PublishListAsync(("demo", "1.0.0"));

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"));

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task A_third_party_plugin_is_not_covered()
    {
        await PublishListAsync(("demo", "1.0.0"));
        _origins.Set("demo", new PluginInstallOrigin("https://github.com/someone/their-plugins", "1.0.0", PluginTrust.ThirdParty));

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"));

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task A_signed_C_sharp_plugin_on_the_list_is_refused_whatever_its_origin()
    {
        await PublishListAsync(("stub", "1.0.0"));

        var result = await _manager.InstallFromFolderAsync(NewSignedCsharp("stub"));

        Assert.Equal(PluginLoadStatus.NotAllowed, result.Plugin.Status);
        Assert.Contains("Unsafe build.", result.Plugin.Detail);
    }

    [Fact]
    public async Task Another_version_of_a_listed_plugin_still_loads()
    {
        await PublishListAsync(("demo", "1.0.0"));
        FromOfficial("demo");

        var result = await _manager.InstallFromFolderAsync(NewJs("demo", "1.1.0"));

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task Without_a_saved_list_everything_loads()
    {
        FromOfficial("demo");

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"));

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task Applying_the_list_stops_a_listed_plugin_starts_a_cleared_one_and_leaves_the_others_alone()
    {
        FromOfficial("demo");
        FromOfficial("other");
        await _manager.InstallFromFolderAsync(NewJs("demo"));
        await _manager.InstallFromFolderAsync(NewJs("other"));
        Assert.Empty(await _manager.ApplyRevocationsAsync());

        await PublishListAsync(("demo", "1.0.0"));
        var changed = await _manager.ApplyRevocationsAsync();

        Assert.Equal(["demo"], changed);
        Assert.Equal(PluginLoadStatus.NotAllowed, _manager.Plugins.Single(p => p.Id == "demo").Status);
        Assert.Equal(PluginLoadStatus.Loaded, _manager.Plugins.Single(p => p.Id == "other").Status);

        await PublishListAsync();
        changed = await _manager.ApplyRevocationsAsync();

        Assert.Equal(["demo"], changed);
        Assert.Equal(PluginLoadStatus.Loaded, _manager.Plugins.Single(p => p.Id == "demo").Status);
        Assert.Empty(await _manager.ApplyRevocationsAsync());
    }

    [Fact]
    public async Task An_uncovered_plugin_on_the_list_is_reloaded_once_and_keeps_running()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"));
        await PublishListAsync(("demo", "1.0.0"));

        await _manager.ApplyRevocationsAsync();

        Assert.Equal(PluginLoadStatus.Loaded, _manager.Plugins.Single(p => p.Id == "demo").Status);
        Assert.Empty(await _manager.ApplyRevocationsAsync());
    }

    private PluginCatalogInstaller NewInstaller(FakeHttpHandler download) =>
        new(new PluginPackageDownloader(new HttpClient(download)), _manager, _origins, Path.Combine(_root, "staging"), _catalog);

    private static (PluginCatalogEntry Entry, PluginCatalogVersion Version) NewEntry()
    {
        var version = new PluginCatalogVersion("1.0.0", "1.0.0", null, null, null,
            "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/x.zip", new string('a', 64), 10, null, null);
        return (new PluginCatalogEntry("x", "X", null, null, null, "js", [version]), version);
    }

    [Fact]
    public async Task The_installer_refuses_a_listed_version_before_any_download()
    {
        await PublishListAsync(("x", "1.0.0"));
        var download = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var (entry, version) = NewEntry();

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            NewInstaller(download).InstallAsync(entry, version, "https://github.com/Deccoyi/macro-grid-plugin", isOfficial: true, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Revoked, ex.Code);
        Assert.Equal(0, download.Calls);
    }

    [Fact]
    public async Task The_installer_goes_on_when_the_list_cannot_be_fetched_and_there_is_no_copy()
    {
        _listBody = null;
        var download = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var (entry, version) = NewEntry();

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            NewInstaller(download).InstallAsync(entry, version, "https://github.com/Deccoyi/macro-grid-plugin", isOfficial: true, CancellationToken.None));

        Assert.NotEqual(PluginDownloadException.Revoked, ex.Code);
        Assert.True(download.Calls > 0);
    }
}
