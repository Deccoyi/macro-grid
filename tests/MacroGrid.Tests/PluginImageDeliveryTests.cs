using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins.Images;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>A plugin picture from the profile's reference to the device: what the layout carries and what the widget state re-sends once a missing picture arrives.</summary>
public sealed class PluginImageDeliveryTests : IDisposable
{
    private sealed class Socket : WebSocket
    {
        private readonly List<Envelope> _sent = [];
        private readonly List<string> _raw = [];

        public List<Envelope> Sent { get { lock (_sent) return [.. _sent]; } }
        public string AllText { get { lock (_sent) return string.Concat(_raw); } }

        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            SendAsync((ReadOnlyMemory<byte>)buffer, messageType, endOfMessage, cancellationToken).AsTask();

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var text = Encoding.UTF8.GetString(buffer.Span);
            lock (_sent)
            {
                _raw.Add(text);
                _sent.Add(Envelope.Parse(text)!);
            }
            return ValueTask.CompletedTask;
        }

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public List<WidgetStateMessage> States(string widgetId) =>
            [.. Sent.Where(e => e.Type == MessageTypes.WidgetState).Select(e => e.DataAs<WidgetStateMessage>()!).Where(s => s.WidgetId == widgetId)];
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-imgdeliv-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginDir;
    private readonly ManualTime _time = new();
    private readonly PluginImageService _images;
    private readonly RecordingImageSource _source = new();
    private readonly ProfileStore _profiles;
    private readonly VariableStore _variables = new();
    private readonly SessionRegistry _sessions = new();
    private readonly AssetStore _assets = new();
    private readonly LayoutSender _layouts;
    private readonly WidgetStateService _service;

    private static readonly string PngUrl = TestPictures.PngUrl(300, fill: 9);

    public PluginImageDeliveryTests()
    {
        _pluginDir = Path.Combine(_root, "plugins", "vm");
        Directory.CreateDirectory(_pluginDir);
        _profiles = new ProfileStore(Path.Combine(_root, "profiles"));
        _images = new PluginImageService(NullLogger<PluginImageService>.Instance, new ProblemList(), _time);
        _images.Attach("vm", "Voice plugin", _pluginDir, _source);
        _layouts = new LayoutSender(_assets, images: _images);
        _service = new WidgetStateService(_variables, _sessions, _profiles, new ToggleStateStore(), _layouts, NullLogger<WidgetStateService>.Instance, images: _images);
    }

    public void Dispose()
    {
        _service.Dispose();
        _images.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static Widget Button(string id, string? icon = null, Dictionary<string, DynamicBinding>? dynamic = null) =>
        new() { Id = id, Text = id, Style = new WidgetStyle { Icon = icon }, Dynamic = dynamic ?? [] };

    private static Dictionary<string, DynamicBinding> IconRule(string variable, string whenOn, string? other = null) => new()
    {
        ["style.icon"] = new DynamicBinding
        {
            Cases = [new DynamicCase(new ConditionNode { Variable = variable, Operator = "==", Value = "true" }, whenOn)],
            Default = other,
        },
    };

    private Profile Save(string id, params Page[] pages)
    {
        var profile = new Profile { Id = id, Name = id, Pages = [.. pages] };
        _profiles.Save(profile);
        return profile;
    }

    private static Page PageOf(string id, params Widget[] widgets) => new() { Id = id, Widgets = [.. widgets] };

    private ClientSession Connect(Socket socket, string profileId, string pageId, params string[] capabilities)
    {
        var session = new ClientSession(socket) { DeviceId = "d-" + Guid.NewGuid().ToString("N"), ProfileId = profileId, PageId = pageId, Capabilities = [.. capabilities] };
        _sessions.Add(session);
        return session;
    }

    private void Cache(string imageId, string? dataUrl = null)
    {
        _images.RequestImage("vm", imageId);
        Assert.True(_images.Put("vm", imageId, dataUrl ?? PngUrl));
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), "the expected message never arrived");
    }

    // ---- the layout ----

    [Fact]
    public async Task The_layout_carries_asset_references_for_cached_pictures_and_never_the_reference_or_the_data()
    {
        Cache("have");
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/have"), Button("w2", "plugin-image:vm/have"), Button("w3", "plugin-image:vm/missing")));
        var socket = new Socket();
        var session = new ClientSession(socket) { Capabilities = [ClientCapabilities.Assets] };

        await _layouts.SendFullAsync(session, profile, "a");

        var widgets = socket.Sent[0].Data!["profile"]!["pages"]![0]!["widgets"]!.AsArray();
        var icon = widgets[0]!["style"]!["icon"]!.GetValue<string>();
        Assert.StartsWith(AssetStore.RefPrefix, icon);
        Assert.Equal(icon, widgets[1]!["style"]!["icon"]!.GetValue<string>());
        Assert.Null(widgets[2]!["style"]!["icon"]);
        Assert.Equal(PngUrl, _assets.Get(icon[AssetStore.RefPrefix.Length..]));
        Assert.DoesNotContain("plugin-image", socket.AllText);
        Assert.DoesNotContain("base64", socket.AllText);
    }

    [Fact]
    public async Task A_client_without_assets_gets_the_picture_inline_but_still_never_the_reference()
    {
        Cache("have");
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/have")));
        var socket = new Socket();

        await _layouts.SendFullAsync(new ClientSession(socket), profile, "a");

        Assert.Equal(PngUrl, socket.Sent[0].Data!["profile"]!["pages"]![0]!["widgets"]![0]!["style"]!["icon"]!.GetValue<string>());
        Assert.DoesNotContain("plugin-image", socket.AllText);
    }

    [Fact]
    public async Task A_missing_picture_leaves_the_icon_out_and_asks_the_plugin()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/voice:1")));
        var socket = new Socket();

        await _layouts.SendFullAsync(new ClientSession(socket) { Capabilities = [ClientCapabilities.Assets] }, profile, "a");
        _time.Advance(TimeSpan.Zero);

        Assert.Null(socket.Sent[0].Data!["profile"]!["pages"]![0]!["widgets"]![0]!["style"]!["icon"]);
        Assert.Equal([["voice:1"]], _source.Calls);
    }

    [Fact]
    public async Task Malformed_references_and_the_rules_of_a_dynamic_icon_never_reach_the_device()
    {
        var rule = IconRule("test.on", "plugin-image:vm/on", "plugin-image:vm/off");
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:not valid"), Button("w2", dynamic: rule)));
        var socket = new Socket();

        await _layouts.SendFullAsync(new ClientSession(socket), profile, "a");

        Assert.DoesNotContain("plugin-image", socket.AllText);
        Assert.Null(socket.Sent[0].Data!["profile"]!["pages"]![0]!["widgets"]![0]!["style"]!["icon"]);
    }

    [Fact]
    public async Task A_patch_after_the_picture_arrived_carries_it_as_an_asset()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/late")));
        var socket = new Socket();
        var session = new ClientSession(socket) { Capabilities = [ClientCapabilities.Assets, ClientCapabilities.LayoutPatch] };
        await _layouts.SendFullAsync(session, profile, "a");
        Cache("late");

        var result = await _layouts.SendUpdateAsync(session, profile, "a");

        Assert.Equal(LayoutSendKind.Patch, result.Kind);
        var upsert = Assert.Single(socket.Sent[^1].Data!["pages"]![0]!["widgets"]!.AsArray());
        Assert.StartsWith(AssetStore.RefPrefix, upsert!["style"]!["icon"]!.GetValue<string>());
        Assert.DoesNotContain("plugin-image", socket.AllText);
    }

    // ---- widget state ----

    [Fact]
    public async Task A_picture_that_arrives_later_is_sent_as_widget_state_to_the_widgets_that_use_it()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/voice:1"), Button("w2", "plugin-image:vm/voice:2"), Button("w3")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _layouts.SendFullAsync(session, profile, "a");
        await _service.SendInitialAsync(session, profile.Pages[0], default);
        Assert.Empty(socket.States("w1"));

        Cache("voice:1");

        await WaitFor(() => socket.States("w1").Count > 0);
        var style = socket.States("w1")[0].Style!;
        Assert.StartsWith(AssetStore.RefPrefix, style["icon"]);
        Assert.Equal(PngUrl, _assets.Get(style["icon"][AssetStore.RefPrefix.Length..]));
        await Task.Delay(100);
        Assert.Empty(socket.States("w2"));
        Assert.Empty(socket.States("w3"));
        Assert.DoesNotContain("plugin-image", socket.AllText);
    }

    [Fact]
    public async Task Every_device_showing_the_widget_gets_it_and_a_device_on_another_profile_does_not()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/x")));
        Save("q", PageOf("a", Button("w1", "plugin-image:vm/x")));
        var first = new Socket();
        var second = new Socket();
        var elsewhere = new Socket();
        var s1 = Connect(first, "p", "a", ClientCapabilities.Assets);
        var s2 = Connect(second, "p", "a", ClientCapabilities.Assets);
        var s3 = Connect(elsewhere, "p", "a", ClientCapabilities.Assets);
        s3.ProfileId = "other-profile-without-it";
        foreach (var s in new[] { s1, s2 }) await _service.SendInitialAsync(s, profile.Pages[0], default);

        Cache("x");

        await WaitFor(() => first.States("w1").Count > 0 && second.States("w1").Count > 0);
        await Task.Delay(100);
        Assert.Empty(elsewhere.States("w1"));
    }

    [Fact]
    public async Task A_widget_on_a_page_that_is_not_shown_gets_its_picture_when_the_page_is_shown()
    {
        var profile = Save("p", PageOf("a", Button("w1")), PageOf("b", Button("w2", "plugin-image:vm/y")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _layouts.SendFullAsync(session, profile, "a");
        await _service.SendInitialAsync(session, profile.Pages[0], default);

        Cache("y");
        await Task.Delay(150);
        Assert.Empty(socket.States("w2"));

        session.PageId = "b";
        await _service.SendInitialAsync(session, profile.Pages[1], default);

        Assert.StartsWith(AssetStore.RefPrefix, Assert.Single(socket.States("w2")).Style!["icon"]);
    }

    [Fact]
    public async Task A_picture_that_arrives_between_the_layout_and_the_first_state_is_not_lost()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/z")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _layouts.SendFullAsync(session, profile, "a");

        Cache("z");
        await _service.SendInitialAsync(session, profile.Pages[0], default);

        await WaitFor(() => socket.States("w1").Any(s => s.Style is not null && s.Style.ContainsKey("icon")));
    }

    [Fact]
    public async Task A_widget_whose_reference_was_edited_away_is_not_touched_by_a_later_arrival()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/gone")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _layouts.SendFullAsync(session, profile, "a");
        _images.RequestImage("vm", "gone");

        Save("p", PageOf("a", Button("w1", "plugin-image:vm/other")));
        Assert.True(_images.Put("vm", "gone", PngUrl));

        await Task.Delay(150);
        Assert.Empty(socket.States("w1"));
    }

    [Fact]
    public async Task After_a_profile_switch_an_arrival_for_the_old_profile_sends_nothing()
    {
        var old = Save("old", PageOf("a", Button("w1", "plugin-image:vm/o")));
        Save("new", PageOf("a", Button("w9")));
        var socket = new Socket();
        var session = Connect(socket, "old", "a", ClientCapabilities.Assets);
        await _layouts.SendFullAsync(session, old, "a");
        session.ProfileId = "new";

        Cache("o");

        await Task.Delay(150);
        Assert.Empty(socket.States("w1"));
        Assert.Empty(socket.States("w9"));
    }

    [Fact]
    public async Task A_dynamic_icon_shows_the_picture_of_the_matching_rule_once_it_is_cached()
    {
        var profile = Save("p", PageOf("a", Button("w1", dynamic: IconRule("test.on", "plugin-image:vm/on", "plugin-image:vm/off"))));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        _variables.Set("test.on", true);
        await _service.SendInitialAsync(session, profile.Pages[0], default);

        Cache("on");

        await WaitFor(() => socket.States("w1").Any(s => s.Style is { } st && st.ContainsKey("icon")));
        _variables.Set("test.on", false);
        await _service.FlushAsync();
        Cache("off", TestPictures.PngUrl(300, fill: 5));
        await WaitFor(() => socket.States("w1").Count(s => s.Style is { } st && st.ContainsKey("icon")) >= 2);
        Assert.DoesNotContain("plugin-image", socket.AllText);
    }

    [Fact]
    public async Task A_picture_icon_and_an_unrelated_dynamic_property_travel_together()
    {
        var dynamic = new Dictionary<string, DynamicBinding>
        {
            ["style.background"] = new DynamicBinding { Default = "#112233" },
        };
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/both", dynamic)));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _service.SendInitialAsync(session, profile.Pages[0], default);
        Assert.Equal("#112233", Assert.Single(socket.States("w1")).Style!["background"]);

        Cache("both");

        await WaitFor(() => socket.States("w1").Count == 2);
        var style = socket.States("w1")[1].Style!;
        Assert.Equal("#112233", style["background"]);
        Assert.StartsWith(AssetStore.RefPrefix, style["icon"]);
    }

    [Fact]
    public async Task A_picture_that_is_gone_from_the_cache_takes_the_icon_back_with_an_empty_style()
    {
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/evicted")));
        Cache("evicted");
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await _service.SendInitialAsync(session, profile.Pages[0], default);
        Assert.StartsWith(AssetStore.RefPrefix, Assert.Single(socket.States("w1")).Style!["icon"]);
        Directory.Delete(Path.Combine(_pluginDir, PluginImageCache.FolderName), recursive: true);
        // the in-memory index still lists the file; a read finds it gone and forgets it

        _service.Refresh("w1", session);

        await WaitFor(() => socket.States("w1").Count == 2);
        Assert.Empty(socket.States("w1")[1].Style!);
    }

    [Fact]
    public async Task A_cached_picture_renders_while_the_plugin_is_not_running_and_an_uncached_one_stays_off_without_error()
    {
        Cache("kept");
        _images.Detach("vm");
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:vm/kept"), Button("w2", "plugin-image:vm/never")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);

        await _layouts.SendFullAsync(session, profile, "a");
        await _service.SendInitialAsync(session, profile.Pages[0], default);
        _time.Advance(TimeSpan.FromSeconds(2));

        var widgets = socket.Sent.First(e => e.Type == MessageTypes.LayoutFull).Data!["profile"]!["pages"]![0]!["widgets"]!.AsArray();
        Assert.StartsWith(AssetStore.RefPrefix, widgets[0]!["style"]!["icon"]!.GetValue<string>());
        Assert.Null(widgets[1]!["style"]!["icon"]);
        Assert.DoesNotContain(socket.Sent, e => e.Type == MessageTypes.Error);
        Assert.Empty(_source.Calls.Skip(1)); // nothing was sent to the stopped plugin
    }

    [Fact]
    public async Task A_plugin_folder_that_becomes_known_later_shows_the_pictures_it_cached_earlier()
    {
        using var late = new PluginImageService(NullLogger<PluginImageService>.Instance, new ProblemList(), _time);
        var lateDir = Path.Combine(_root, "plugins", "late");
        new PluginImageCache(lateDir).Put("p1", TestPictures.Png(300));
        using var service = new WidgetStateService(_variables, _sessions, _profiles, new ToggleStateStore(), new LayoutSender(_assets, images: late), NullLogger<WidgetStateService>.Instance, images: late);
        var profile = Save("p", PageOf("a", Button("w1", "plugin-image:late/p1")));
        var socket = new Socket();
        var session = Connect(socket, "p", "a", ClientCapabilities.Assets);
        await service.SendInitialAsync(session, profile.Pages[0], default);
        Assert.Empty(socket.States("w1"));

        late.Attach("late", "Late", lateDir, null);

        await WaitFor(() => socket.States("w1").Count > 0);
    }

    // ---- what stays out of backups, diagnostics and package checks ----

    [Fact]
    public void A_backup_and_a_restore_point_do_not_hold_the_picture_cache()
    {
        using var world = new BackupTestWorld();
        var cacheDir = Path.Combine(world.Root, "plugins", "vm", PluginImageCache.FolderName);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, "abc.bin"), TestPictures.Png(64, fill: 0x5A));
        world.AddProfile("P");

        world.Service.CreateRestorePoint(MacroGrid.Core.Backup.BackupService.ReasonImport);
        var zips = new List<byte[]> { world.Service.BuildBackup() };
        zips.AddRange(Directory.GetFiles(world.RestorePointsFolder, "*.mgbackup").Select(File.ReadAllBytes));

        Assert.Equal(2, zips.Count);
        foreach (var bytes in zips)
        {
            using var archive = new ZipArchive(new MemoryStream(bytes));
            Assert.DoesNotContain(archive.Entries, e => e.FullName.Contains("image-cache") || e.FullName.EndsWith(".bin"));
            foreach (var entry in archive.Entries)
            {
                using var buffer = new MemoryStream();
                entry.Open().CopyTo(buffer);
                Assert.DoesNotContain(new string('Z', 16), Encoding.Latin1.GetString(buffer.ToArray()));
            }
        }
    }

    [Fact]
    public void The_data_folder_report_never_lists_cached_pictures()
    {
        var data = Path.Combine(_root, "data");
        var cacheDir = Path.Combine(data, "plugins", "vm", PluginImageCache.FolderName);
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, PluginImageCache.FileNameFor("voice:private")), TestPictures.Png());

        var text = string.Join("\n", DataDirReport.Inventory(data).Select(l => l.Text));

        Assert.Contains("vm/", text);
        Assert.DoesNotContain(".bin", text);
        Assert.DoesNotContain("image-cache", text);
    }

    [Fact]
    public void A_package_check_ignores_the_cache_folder_but_still_counts_other_files()
    {
        var folder = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(Path.Combine(folder, PluginImageCache.FolderName));
        File.WriteAllText(Path.Combine(folder, "plugin.json"), """{ "id": "demo.one", "name": "Demo", "version": "1.0.0", "entry": "index.js", "kind": "js", "minMacroGrid": "1.0.0", "permissions": ["variables"] }""");
        File.WriteAllText(Path.Combine(folder, "index.js"), "host.log('x');");
        File.WriteAllText(Path.Combine(folder, "LICENSE"), "MIT");
        using (var big = File.Create(Path.Combine(folder, PluginImageCache.FolderName, "big.bin"))) big.SetLength(101L * 1024 * 1024);

        Assert.False(MacroGrid.Core.Plugins.PluginFolderValidator.Validate(folder, "1.0.0").HasErrors);

        using (var big = File.Create(Path.Combine(folder, "other.bin"))) big.SetLength(101L * 1024 * 1024);
        Assert.True(MacroGrid.Core.Plugins.PluginFolderValidator.Validate(folder, "1.0.0").HasErrors);
    }

    [Fact]
    public void The_strict_signature_check_ignores_pictures_in_the_cache_but_not_code_hidden_there()
    {
        var dir = Path.Combine(_root, "signed");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "plugin.json"), """{ "id": "demo.one", "name": "Demo", "version": "1.0.0", "entry": "index.js", "kind": "js", "minMacroGrid": "1.0.0" }""");
        File.WriteAllText(Path.Combine(dir, "index.js"), "host.log('x');");
        TestPluginSigning.Sign(dir);
        var manifest = MacroGrid.Core.Plugins.PluginManager.PeekManifest(dir);
        Directory.CreateDirectory(Path.Combine(dir, PluginImageCache.FolderName));
        File.WriteAllBytes(Path.Combine(dir, PluginImageCache.FolderName, "abc.bin"), TestPictures.Png());

        Assert.True(TestPluginSigning.Strict.VerifyStrict(dir, manifest).Allowed);

        File.WriteAllText(Path.Combine(dir, PluginImageCache.FolderName, "evil.js"), "x");
        Assert.False(TestPluginSigning.Strict.VerifyStrict(dir, manifest).Allowed);
    }
}
