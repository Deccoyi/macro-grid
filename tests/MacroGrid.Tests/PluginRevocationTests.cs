using System.Net;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
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
    private readonly ProblemList _problems = new();
    private byte[]? _listBody;
    private byte[]? _indexBody;
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
        _handler = new FakeHttpHandler(request =>
            (request.RequestUri!.AbsoluteUri.Contains(PluginSourceUrls.IndexFileName) ? _indexBody : _listBody) is { } body
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(_handler);
        _catalog = new OfficialCatalog(http, new PluginCatalogStateStore(_root), new PluginCatalogClient(http, _clock), _clock, TestPluginSigning.PublicKey, new Random(1));
        _manager = new PluginManager(_pluginsDir, "1.0.0", new PluginStatusRegistry(), new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), _providerHost, new VariableStore(), new PluginPermissionStore(_root), null,
            NullLogger<PluginManager>.Instance, problems: _problems, trustVerifier: TestPluginSigning.Strict, officialCatalog: _catalog, origins: _origins);
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

    private static PluginInstallOrigin FromOfficial() => new("https://github.com/Deccoyi/macro-grid-plugin", "1.0.0", PluginTrust.Official);

    [Fact]
    public async Task An_official_plugin_on_the_list_does_not_run_and_the_reason_is_shown()
    {
        await PublishListAsync(("demo", "1.0.0"));

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        Assert.Equal(PluginLoadStatus.NotAllowed, result.Plugin.Status);
        Assert.Contains("Unsafe build.", result.Plugin.Detail);
    }

    [Fact]
    public async Task The_origin_is_recorded_before_the_first_load_and_forgotten_on_uninstall_or_a_folder_install()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        Assert.Equal(PluginTrust.Official, _origins.Get("demo")!.Trust);
        Assert.False(_origins.Get("demo")!.Hold);

        await _manager.InstallFromFolderAsync(NewJs("demo"));
        Assert.Null(_origins.Get("demo"));

        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        await _manager.UninstallAsync("demo");
        Assert.Null(_origins.Get("demo"));
    }

    [Fact]
    public async Task A_refused_install_keeps_the_old_origin()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        var bad = Path.Combine(_root, "bad");
        Directory.CreateDirectory(bad);
        File.WriteAllText(Path.Combine(bad, "plugin.json"), "{ not json");
        await Assert.ThrowsAnyAsync<Exception>(() => _manager.InstallFromFolderAsync(bad));

        Assert.NotNull(_origins.Get("demo"));
    }

    [Fact]
    public void An_origin_file_from_before_the_new_members_reads_with_defaults()
    {
        File.WriteAllText(Path.Combine(_root, "plugin-installs.json"),
            """{ "old": { "sourceUrl": "https://x", "version": "1.0.0", "trust": "Official" } }""");

        var origin = new PluginInstallOriginStore(_root).Get("old")!;

        Assert.False(origin.Hold);
        Assert.False(origin.ContentsSigned);
        Assert.Null(origin.CodeFiles);
    }

    private static PluginInstallOrigin FromOfficialSigned() => FromOfficial() with { ContentsSigned = true };

    [Fact]
    public async Task An_official_javascript_plugin_with_a_contents_signature_stops_when_a_file_is_edited()
    {
        var dir = NewJs("demo");
        TestPluginSigning.Sign(dir);
        var installed = await _manager.InstallFromFolderAsync(dir, FromOfficialSigned());
        Assert.Equal(PluginLoadStatus.Loaded, installed.Plugin.Status);

        File.WriteAllText(Path.Combine(_pluginsDir, "demo", "index.js"), "// edited");
        var reloaded = await _manager.ReloadAsync("demo");

        Assert.Equal(PluginLoadStatus.NotAllowed, reloaded!.Status);
        Assert.Contains(ProblemCodes.FilesChanged, _problems.Snapshot().Select(p => p.Code));
    }

    [Fact]
    public async Task A_file_added_next_to_a_signed_official_javascript_plugin_stops_it_too()
    {
        var dir = NewJs("demo");
        TestPluginSigning.Sign(dir);
        await _manager.InstallFromFolderAsync(dir, FromOfficialSigned());

        File.WriteAllText(Path.Combine(_pluginsDir, "demo", "extra.js"), "// added");

        Assert.Equal(PluginLoadStatus.NotAllowed, (await _manager.ReloadAsync("demo"))!.Status);
    }

    [Fact]
    public async Task An_official_javascript_plugin_without_a_contents_signature_or_from_elsewhere_is_not_checked()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        File.WriteAllText(Path.Combine(_pluginsDir, "demo", "index.js"), "// edited");
        Assert.Equal(PluginLoadStatus.Loaded, (await _manager.ReloadAsync("demo"))!.Status);

        var dir = NewJs("other");
        TestPluginSigning.Sign(dir);
        await _manager.InstallFromFolderAsync(dir, FromOfficialSigned() with { Trust = PluginTrust.ThirdParty });
        File.WriteAllText(Path.Combine(_pluginsDir, "other", "index.js"), "// edited");
        Assert.Equal(PluginLoadStatus.Loaded, (await _manager.ReloadAsync("other"))!.Status);
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

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"), new PluginInstallOrigin("https://github.com/someone/their-plugins", "1.0.0", PluginTrust.ThirdParty));

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
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        var result = await _manager.InstallFromFolderAsync(NewJs("demo", "1.1.0"), FromOfficial());

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
    }

    [Fact]
    public async Task Without_a_saved_list_everything_loads()
    {

        var result = await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        Assert.Equal(PluginLoadStatus.Loaded, result.Plugin.Status);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task Applying_the_list_stops_a_listed_plugin_starts_a_cleared_one_and_leaves_the_others_alone()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        await _manager.InstallFromFolderAsync(NewJs("other"), FromOfficial());
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

    private PluginCatalogMonitor NewMonitor(ProblemList problems) => new(_catalog, _manager, _origins, problems, _clock);

    private static string[] Codes(ProblemList problems) => [.. problems.Snapshot().Select(p => p.Code)];

    [Fact]
    public async Task Only_official_plugins_make_the_service_watch_the_catalog()
    {
        var monitor = NewMonitor(_problems);
        await _manager.InstallFromFolderAsync(NewJs("local"));
        Assert.False(monitor.HasWatchedPlugins());

        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        Assert.True(monitor.HasWatchedPlugins());
    }

    [Fact]
    public async Task A_due_safety_list_is_fetched_and_applied_to_the_running_plugins()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        _listBody = TestPluginSigning.SignEnvelope("""{ "kind": "revoked", "sequence": 1, "formatVersion": 1, "plugins": [ { "id": "demo", "reason": "Unsafe build." } ] }""");
        var problems = _problems;

        await NewMonitor(problems).RunOnceAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.NotAllowed, _manager.Plugins.Single(p => p.Id == "demo").Status);
        Assert.Contains(ProblemCodes.Revoked, Codes(problems));
    }

    [Fact]
    public async Task A_failed_look_changes_nothing()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());

        await NewMonitor(_problems).RunOnceAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Loaded, _manager.Plugins.Single(p => p.Id == "demo").Status);
    }

    [Fact]
    public async Task An_installed_withdrawn_version_gets_a_warning_that_goes_when_it_no_longer_applies()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        var problems = _problems;
        var monitor = NewMonitor(problems);
        _indexBody = TestPluginSigning.SignEnvelope($$"""
            { "kind": "index", "sequence": 1, "formatVersion": 2, "name": "Official", "plugins": [
              { "id": "demo", "name": "Demo", "kind": "js", "versions": [
                { "version": "1.0.0", "minMacroGrid": "1.0.0", "withdrawn": true, "url": "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/demo.zip", "sha256": "{{new string('a', 64)}}", "size": 10 } ] } ] }
            """);
        await _catalog.GetIndexAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true);

        monitor.UpdateLines();
        Assert.Contains(ProblemCodes.Withdrawn, Codes(problems));

        _clock.Advance(TimeSpan.FromMinutes(5));
        _indexBody = TestPluginSigning.SignEnvelope($$"""
            { "kind": "index", "sequence": 2, "formatVersion": 2, "name": "Official", "plugins": [
              { "id": "demo", "name": "Demo", "kind": "js", "versions": [
                { "version": "1.0.0", "minMacroGrid": "1.0.0", "url": "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/demo.zip", "sha256": "{{new string('a', 64)}}", "size": 10 } ] } ] }
            """);
        await _catalog.GetIndexAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true);

        monitor.UpdateLines();
        Assert.DoesNotContain(ProblemCodes.Withdrawn, Codes(problems));
    }

    [Fact]
    public async Task A_safety_list_unchecked_for_two_weeks_gets_one_quiet_line_that_a_good_check_removes()
    {
        await _manager.InstallFromFolderAsync(NewJs("demo"), FromOfficial());
        var problems = _problems;
        var monitor = NewMonitor(problems);
        _catalog.GetRevokedAsync(TimeSpan.Zero, CancellationToken.None).GetAwaiter().GetResult();

        monitor.UpdateLines();
        Assert.DoesNotContain(ProblemCodes.CatalogStale, Codes(problems));

        _clock.Advance(PluginCatalogPolicy.StaleAfter + TimeSpan.FromHours(1));
        monitor.UpdateLines();
        Assert.Contains(ProblemCodes.CatalogStale, Codes(problems));
        Assert.Equal(PluginLoadStatus.Loaded, _manager.Plugins.Single(p => p.Id == "demo").Status);

        await PublishListAsync();
        monitor.UpdateLines();
        Assert.DoesNotContain(ProblemCodes.CatalogStale, Codes(problems));
    }

    private PluginCatalogInstaller NewInstaller(FakeHttpHandler download) =>
        new(new PluginPackageDownloader(new HttpClient(download)), _manager, Path.Combine(_root, "staging"), _catalog);

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
