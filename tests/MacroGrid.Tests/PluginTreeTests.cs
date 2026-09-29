using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>The optional plugin tree items (<see cref="IPluginTreeProvider"/>): the reader's limits, the change log
/// the editor polls, and a real plugin that opts in. A plugin that does not opt in is covered by
/// <see cref="LegacyPluginCompatibilityTests"/> (a real binary built against an SDK that predates the interface).</summary>
public sealed class PluginTreeTests : IAsyncLifetime
{
    private sealed class FakeProvider(Func<string?, string?, CancellationToken, Task<PluginTreePage>> get) : IPluginTreeProvider
    {
        public event Action<string?>? TreeItemsChanged { add { } remove { } }

        public Task<PluginTreePage> GetTreeItemsAsync(string? parentId, string? continuationToken, CancellationToken cancellationToken) =>
            get(parentId, continuationToken, cancellationToken);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tree-tests-" + Guid.NewGuid().ToString("N"));
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;

    public async Task InitializeAsync()
    {
        var variables = new VariableStore();
        _providerHost = new VariableProviderHost([], variables, NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = new PluginManager(Path.Combine(_root, "plugins"), "1.0.0", new PluginStatusRegistry(),
            new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance), new VariableCatalog([]), _providerHost,
            variables, new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance, trustVerifier: TestPluginSigning.Lenient);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    // ---- the reader ----

    [Fact]
    public async Task Reader_passes_the_parent_and_token_through_and_returns_the_page()
    {
        string? seenParent = "unset", seenToken = "unset";
        var provider = new FakeProvider((parent, token, _) =>
        {
            (seenParent, seenToken) = (parent, token);
            return Task.FromResult(new PluginTreePage([new PluginTreeItem("a", "A", HasChildren: true)], "next"));
        });

        var page = await PluginTreeReader.ReadAsync(provider, "folder", "t1", TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal("folder", seenParent);
        Assert.Equal("t1", seenToken);
        Assert.Equal("a", Assert.Single(page.Items).Id);
        Assert.Equal("next", page.ContinuationToken);
    }

    [Fact]
    public async Task Reader_treats_an_empty_parent_as_the_top_level()
    {
        string? seenParent = "unset";
        var provider = new FakeProvider((parent, _, _) => { seenParent = parent; return Task.FromResult(new PluginTreePage([])); });

        await PluginTreeReader.ReadAsync(provider, "", null, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Null(seenParent);
    }

    [Fact]
    public async Task Reader_drops_items_without_an_id_and_duplicates()
    {
        var provider = new FakeProvider((_, _, _) => Task.FromResult(new PluginTreePage(
        [
            new PluginTreeItem("a", "A"),
            new PluginTreeItem("", "No id"),
            new PluginTreeItem("a", "A again"),
            new PluginTreeItem("b", "B"),
        ])));

        var page = await PluginTreeReader.ReadAsync(provider, null, null, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(["a", "b"], page.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Reader_caps_a_page_at_500_items()
    {
        var provider = new FakeProvider((_, _, _) => Task.FromResult(new PluginTreePage(
            [.. Enumerable.Range(0, 800).Select(i => new PluginTreeItem($"i{i}", $"Item {i}"))])));

        var page = await PluginTreeReader.ReadAsync(provider, null, null, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(PluginTreeReader.MaxItemsPerPage, page.Items.Count);
    }

    [Fact]
    public async Task Reader_times_out_a_provider_that_ignores_its_token()
    {
        var provider = new FakeProvider(async (_, _, _) =>
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return new PluginTreePage([]);
        });

        await Assert.ThrowsAsync<TimeoutException>(() =>
            PluginTreeReader.ReadAsync(provider, null, null, TimeSpan.FromMilliseconds(100), CancellationToken.None));
    }

    [Fact]
    public async Task Reader_reports_the_callers_cancellation_as_cancellation_not_as_a_timeout()
    {
        var provider = new FakeProvider(async (_, _, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new PluginTreePage([]);
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PluginTreeReader.ReadAsync(provider, null, null, TimeSpan.FromSeconds(10), cts.Token));
    }

    // ---- the change log ----

    [Fact]
    public void Change_log_returns_only_what_changed_after_the_given_revision()
    {
        var log = new PluginTreeChangeLog();
        log.Record("p", null);
        var seen = log.Revision;
        log.Record("p", "folder");
        log.Record("q", "");

        var changes = log.Since(seen);

        Assert.False(changes.Reset);
        Assert.Equal(log.Revision, changes.Revision);
        Assert.Equal([("p", "folder"), ("q", (string?)null)], changes.Changes.Select(c => (c.PluginId, c.ParentId)));
    }

    [Fact]
    public void Change_log_asks_for_a_reset_when_the_revision_is_older_than_what_it_kept()
    {
        var log = new PluginTreeChangeLog();
        for (var i = 0; i < PluginTreeChangeLog.Capacity + 10; i++) log.Record("p", null);

        Assert.True(log.Since(1).Reset);
        Assert.False(log.Since(log.Revision - 5).Reset);
    }

    [Fact]
    public void Change_log_asks_for_a_reset_after_a_server_restart()
    {
        var log = new PluginTreeChangeLog();

        Assert.True(log.Since(42).Reset);
    }

    [Fact]
    public void Change_log_answers_a_fresh_editor_with_the_revision_only()
    {
        var log = new PluginTreeChangeLog();
        log.Record("p", null);

        var changes = log.Since(-1);

        Assert.Empty(changes.Changes);
        Assert.False(changes.Reset);
        Assert.Equal(1, changes.Revision);
    }

    // ---- a real plugin that opts in ----

    private async Task<PluginInstallResult> InstallStubAsync()
    {
        var dir = Path.Combine(_root, "source");
        Directory.CreateDirectory(dir);
        var entryAssembly = typeof(StubPlugin.StubPlugin).Assembly.Location;
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(entryAssembly)!, "*.dll"))
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), $$"""
        { "id": "stub", "name": "Stub", "version": "1.0.0", "macroGrid": "1.0.0", "entry": "{{Path.GetFileName(entryAssembly)}}", "kind": "csharp" }
        """);
        return await _manager.InstallFromFolderAsync(dir);
    }

    [Fact]
    public async Task A_plugin_that_implements_the_provider_reports_HasTreeItems_and_lists_its_levels()
    {
        var result = await InstallStubAsync();

        Assert.True(result.Plugin.HasTreeItems);
        var provider = Assert.IsAssignableFrom<IPluginTreeProvider>(_manager.GetTreeProvider("stub"));

        var top = await PluginTreeReader.ReadAsync(provider, null, null, PluginTreeReader.DefaultTimeout, CancellationToken.None);
        var sounds = await PluginTreeReader.ReadAsync(provider, "sounds", null, PluginTreeReader.DefaultTimeout, CancellationToken.None);

        Assert.True(Assert.Single(top.Items).HasChildren);
        Assert.Equal(["Applause", "Drum roll"], sounds.Items.Select(i => i.Label));
        Assert.All(sounds.Items, i => Assert.True(i.HasSettings));
    }

    [Fact]
    public async Task Item_settings_round_trip_and_a_change_reaches_the_change_log()
    {
        await InstallStubAsync();
        var settings = Assert.IsAssignableFrom<IPluginTreeItemSettings>(_manager.GetTreeProvider("stub"));
        var before = _manager.TreeChanges.Revision;

        Assert.Equal("name", Assert.Single(settings.GetItemFields("s1")).Key);
        settings.SaveItem("s1", new JsonObject { ["name"] = "Cheer" });

        Assert.Equal("Cheer", settings.LoadItem("s1")["name"]!.GetValue<string>());
        var change = Assert.Single(_manager.TreeChanges.Since(before).Changes);
        Assert.Equal(("stub", "sounds", false), (change.PluginId, change.ParentId, change.WholePlugin));
    }

    [Fact]
    public async Task Loading_and_uninstalling_a_provider_marks_the_whole_plugin_changed_and_unsubscribes()
    {
        var before = _manager.TreeChanges.Revision;
        await InstallStubAsync();
        var provider = (IPluginTreeItemSettings)_manager.GetTreeProvider("stub")!;

        Assert.Contains(_manager.TreeChanges.Since(before).Changes, c => c is { PluginId: "stub", WholePlugin: true });

        await _manager.UninstallAsync("stub");
        var afterUninstall = _manager.TreeChanges.Revision;
        Assert.Contains(_manager.TreeChanges.Since(afterUninstall - 1).Changes, c => c is { PluginId: "stub", WholePlugin: true });
        Assert.Null(_manager.GetTreeProvider("stub"));

        // The host no longer listens: a late event from the unloaded instance records nothing.
        provider.SaveItem("s1", new JsonObject { ["name"] = "Late" });
        Assert.Equal(afterUninstall, _manager.TreeChanges.Revision);
    }

    [Fact]
    public void An_unknown_plugin_has_no_tree_provider()
    {
        Assert.Null(_manager.GetTreeProvider("nope"));
    }
}
