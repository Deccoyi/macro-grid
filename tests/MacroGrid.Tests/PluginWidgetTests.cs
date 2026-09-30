using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginWidgetTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-widgets-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginsDir;
    private readonly AssetStore _assets = new();
    private readonly ProblemList _problems = new();
    private readonly PluginWidgetCatalog _catalog;
    private readonly PluginWidgetEventHub _events;
    private VariableProviderHost _providerHost = null!;
    private PluginManager _manager = null!;

    public PluginWidgetTests()
    {
        _pluginsDir = Path.Combine(_root, "plugins");
        Directory.CreateDirectory(_pluginsDir);
        _catalog = new PluginWidgetCatalog(_assets);
        _events = new PluginWidgetEventHub(_problems);
    }

    public async Task InitializeAsync()
    {
        var variables = new VariableStore();
        _providerHost = new VariableProviderHost([], variables, NullLogger<VariableProviderHost>.Instance);
        await _providerHost.StartAsync(CancellationToken.None);
        _manager = new PluginManager(_pluginsDir, "1.4.0", new PluginStatusRegistry(), new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), _providerHost, variables, new PluginPermissionStore(_root), null, NullLogger<PluginManager>.Instance,
            trustVerifier: TestPluginSigning.Lenient, problems: _problems, widgetCatalog: _catalog, widgetEvents: _events);
    }

    public async Task DisposeAsync()
    {
        await _manager.StopAsync(CancellationToken.None);
        await _providerHost.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    // ---- helpers ----

    private static PluginManifest Manifest(params PluginWidgetManifest[] widgets) => new()
    {
        Id = "w", Name = "W", Version = "1.0.0", MinMacroGrid = "1.4.0", Entry = "index.js", Kind = PluginKind.Js, Widgets = widgets,
    };

    private static PluginWidgetManifest Widget(string id = "gauge", string entry = "widgets/gauge.js") => new() { Id = id, Name = "Gauge", Entry = entry };

    private string Dir(string name = "w")
    {
        var dir = Path.Combine(_root, "src-" + name + Guid.NewGuid().ToString("N")[..4]);
        Directory.CreateDirectory(Path.Combine(dir, "widgets"));
        File.WriteAllText(Path.Combine(dir, "widgets", "gauge.js"), "const { canvas } = await macroGrid.ready;");
        return dir;
    }

    private PluginWidgetValidation Validate(string dir, PluginManifest manifest, bool verified = true) => PluginWidgetValidator.Validate(dir, manifest, verified);

    private string InstallJsPlugin(string id, string widgetsJson, string permissionsJson = "[]")
    {
        var dir = Path.Combine(_pluginsDir, id);
        Directory.CreateDirectory(Path.Combine(dir, "widgets"));
        File.WriteAllText(Path.Combine(dir, "widgets", "gauge.js"), "// widget code");
        File.WriteAllText(Path.Combine(dir, "index.js"), "");
        File.WriteAllText(Path.Combine(dir, "plugin.json"),
            "{ \"id\": \"" + id + "\", \"name\": \"Js\", \"version\": \"1.0.0\", \"minMacroGrid\": \"1.0.0\", \"entry\": \"index.js\", \"kind\": \"js\", "
            + "\"permissions\": " + permissionsJson + ", \"widgets\": " + widgetsJson + " }");
        return dir;
    }

    // ---- manifest reading ----

    [Fact]
    public void Widgets_are_read_from_the_manifest_json()
    {
        var json = """
        { "id": "p", "name": "P", "version": "1.0.0", "entry": "i.js", "kind": "js", "widgets": [
          { "id": "gauge", "name": "Gauge", "entry": "widgets/gauge.js", "size": { "w": 3, "h": 2 }, "fps": 30, "interactive": true,
            "options": { "keepLoaded": { "default": false } },
            "settings": [ { "key": "source", "label": "Value", "kind": "Variable" } ] } ] }
        """;

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var widget = Assert.Single(manifest.Widgets!);
        Assert.Equal(3, widget.Size!.W);
        Assert.True(widget.Interactive);
        Assert.False(widget.Options!["keepLoaded"].Default);
        Assert.Equal(SettingFieldKind.Variable, widget.Settings![0].Kind);
    }

    [Fact]
    public void Options_that_start_off_are_the_declared_ones_with_default_false()
    {
        var manifest = new PluginWidgetManifest
        {
            Id = "gauge",
            Name = "Gauge",
            Entry = "widgets/gauge.js",
            Options = new Dictionary<string, PluginWidgetOption> { ["keepLoaded"] = new() { Default = false }, ["storage"] = new() },
        };
        var widget = new ValidatedWidget(manifest, "", [], 15, new PluginWidgetSize(2, 2), ["keepLoaded", "storage"]);

        Assert.Equal(["keepLoaded"], widget.OptionsOffByDefault);
    }

    [Fact]
    public void The_notifications_option_is_refused_because_it_is_not_supported()
    {
        var widget = Widget() with { Options = new Dictionary<string, PluginWidgetOption> { ["notifications"] = new() } };

        var result = Validate(Dir(), Manifest(widget));

        Assert.Empty(result.Widgets);
        Assert.Contains("not supported", Assert.Single(result.Problems).Reason);
    }

    // ---- icon ----

    private PluginWidgetValidation ValidateWithIcon(string svg)
    {
        var dir = Dir();
        File.WriteAllText(Path.Combine(dir, "widgets", "gauge.svg"), svg);
        return Validate(dir, Manifest(Widget() with { Icon = "widgets/gauge.svg" }));
    }

    [Fact]
    public void A_plain_svg_icon_is_accepted()
    {
        var result = ValidateWithIcon("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><defs><linearGradient id=\"g\"/></defs><path fill=\"url(#g)\" stroke-linejoin=\"round\" d=\"M2 2h20v20H2z\"/></svg>");

        var ok = Assert.Single(result.Widgets);
        Assert.NotNull(ok.IconPath);
        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData("<svg viewBox=\"0 0 1 1\"><script>alert(1)</script></svg>")]
    [InlineData("<svg viewBox=\"0 0 1 1\" onload=\"x()\"></svg>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"><image href=\"https://example.com/a.png\"/></svg>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"><a xlink:href=\"javascript:x()\"><path d=\"M0 0\"/></a></svg>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"><foreignObject><div/></foreignObject></svg>")]
    [InlineData("<svg viewBox=\"0 0 1 1\"><rect style=\"fill:url(https://example.com/x)\"/></svg>")]
    [InlineData("<html><body/></html>")]
    public void An_icon_with_scripts_links_or_no_svg_leaves_the_widget_out(string svg)
    {
        var result = ValidateWithIcon(svg);

        Assert.Empty(result.Widgets);
        Assert.StartsWith("icon:", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void A_big_icon_is_refused()
    {
        var result = ValidateWithIcon("<svg viewBox=\"0 0 1 1\">" + new string(' ', PluginWidgetLimits.MaxIconBytes) + "</svg>");

        Assert.Contains("larger", Assert.Single(result.Problems).Reason);
    }

    // ---- validator ----

    [Fact]
    public void A_valid_widget_passes_with_defaults()
    {
        var dir = Dir();

        var result = Validate(dir, Manifest(Widget()));

        var ok = Assert.Single(result.Widgets);
        Assert.Empty(result.Problems);
        Assert.Equal(PluginWidgetLimits.DefaultFps, ok.Fps);
        Assert.Equal(new PluginWidgetSize(2, 2), ok.Size);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("")]
    [InlineData("a/b")]
    public void A_bad_id_is_refused(string id)
    {
        var result = Validate(Dir(), Manifest(Widget(id)));

        Assert.Empty(result.Widgets);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void A_duplicate_id_leaves_the_second_out_and_keeps_the_first()
    {
        var result = Validate(Dir(), Manifest(Widget("a"), Widget("a")));

        Assert.Single(result.Widgets);
        Assert.Contains("same id", Assert.Single(result.Problems).Reason);
    }

    [Theory]
    [InlineData("../evil.js")]
    [InlineData("widgets/../../evil.js")]
    [InlineData("C:/evil.js")]
    [InlineData("widgets/gauge.txt")]
    [InlineData("widgets/missing.js")]
    public void An_entry_that_escapes_or_is_not_a_script_is_refused(string entry)
    {
        var result = Validate(Dir(), Manifest(Widget(entry: entry)));

        Assert.Empty(result.Widgets);
        Assert.StartsWith("entry:", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void An_unverified_plugin_gets_stricter_frame_rate_and_script_size()
    {
        var dir = Dir();
        File.WriteAllBytes(Path.Combine(dir, "widgets", "big.js"), new byte[PluginWidgetLimits.MaxEntryBytesUnverified + 1]);
        var fast = Widget() with { Fps = 60 };
        var big = Widget("big", "widgets/big.js");

        var verified = Validate(dir, Manifest(fast, big), verified: true);
        var unverified = Validate(dir, Manifest(fast, big), verified: false);

        Assert.Equal(60, verified.Widgets.Single(w => w.Manifest.Id == "gauge").Fps);
        Assert.Contains(verified.Widgets, w => w.Manifest.Id == "big");
        Assert.Equal(30, unverified.Widgets.Single().Fps);
        Assert.Contains("larger than", Assert.Single(unverified.Problems).Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void A_frame_rate_out_of_range_is_refused(int fps)
    {
        var result = Validate(Dir(), Manifest(Widget() with { Fps = fps }));

        Assert.Empty(result.Widgets);
    }

    [Fact]
    public void Password_and_file_settings_are_refused_because_profiles_are_shared()
    {
        var withPassword = Widget() with { Settings = [new SettingField("key", "Key", SettingFieldKind.Password)] };
        var withFile = Widget("f") with { Settings = [new SettingField("path", "Path", SettingFieldKind.File) { FileFilter = "*" }] };
        var withVariable = Widget("v") with { Settings = [new SettingField("source", "Value", SettingFieldKind.Variable)] };

        var result = Validate(Dir(), Manifest(withPassword, withFile, withVariable));

        Assert.Equal("v", Assert.Single(result.Widgets).Manifest.Id);
        Assert.Equal(2, result.Problems.Count);
    }

    [Fact]
    public void An_unknown_option_is_refused()
    {
        var widget = Widget() with { Options = new() { ["network"] = new PluginWidgetOption() } };

        var result = Validate(Dir(), Manifest(widget));

        Assert.Empty(result.Widgets);
        Assert.Contains("Unknown option", Assert.Single(result.Problems).Reason);
    }

    [Fact]
    public void Assets_must_be_images_or_fonts_inside_the_folder_and_are_capped_in_total()
    {
        var dir = Dir();
        File.WriteAllBytes(Path.Combine(dir, "widgets", "a.png"), new byte[10]);
        File.WriteAllBytes(Path.Combine(dir, "widgets", "huge.png"), new byte[PluginWidgetLimits.MaxAssetBytesPerPlugin + 1]);
        File.WriteAllText(Path.Combine(dir, "widgets", "s.svg"), "<svg/>");

        var ok = Validate(dir, Manifest(Widget() with { Assets = ["widgets/a.png"] }));
        var svg = Validate(dir, Manifest(Widget() with { Assets = ["widgets/s.svg"] }));
        var huge = Validate(dir, Manifest(Widget() with { Assets = ["widgets/huge.png"] }));

        Assert.Single(ok.Widgets);
        Assert.Contains("asset", Assert.Single(svg.Problems).Reason);
        Assert.Contains("larger than", Assert.Single(huge.Problems).Reason);
    }

    [Fact]
    public void At_most_sixteen_widgets_are_taken()
    {
        var widgets = Enumerable.Range(0, 18).Select(i => Widget("w" + i)).ToArray();

        var result = Validate(Dir(), Manifest(widgets));

        Assert.Equal(16, result.Widgets.Count);
        Assert.Equal(2, result.Problems.Count);
    }

    // ---- catalog ----

    [Fact]
    public void The_catalog_stores_the_script_and_images_as_assets_and_says_why_a_widget_is_unavailable()
    {
        var dir = Dir();
        File.WriteAllBytes(Path.Combine(dir, "widgets", "a.png"), [1, 2, 3]);
        var validation = Validate(dir, Manifest(Widget() with { Assets = ["widgets/a.png"] }));

        _catalog.Set("w", "W", PluginWidgetAvailability.Available, true, validation.Widgets, validation.Problems);
        var info = _catalog.Resolve("w", "gauge", out var reason)!;

        Assert.Null(reason);
        Assert.StartsWith("asset:", info.CodeRef);
        Assert.Contains("macroGrid.ready", _assets.Get(info.CodeRef["asset:".Length..]));
        Assert.StartsWith("data:image/png;base64,", _assets.Get(info.AssetRefs["widgets/a.png"]["asset:".Length..]));

        Assert.Null(_catalog.Resolve("w", "nope", out reason));
        Assert.Equal("noWidget", reason);
        Assert.Null(_catalog.Resolve("other", "gauge", out reason));
        Assert.Equal("missing", reason);

        _catalog.Set("w", "W", PluginWidgetAvailability.NeedsApproval, false, [], []);
        Assert.Null(_catalog.Resolve("w", "gauge", out reason));
        Assert.Equal("needsApproval", reason);
    }

    [Fact]
    public void A_new_script_version_is_a_new_reference()
    {
        var dir = Dir();
        _catalog.Set("w", "W", PluginWidgetAvailability.Available, true, Validate(dir, Manifest(Widget())).Widgets, []);
        var first = _catalog.Resolve("w", "gauge", out _)!.CodeRef;

        File.WriteAllText(Path.Combine(dir, "widgets", "gauge.js"), "// changed");
        _catalog.Set("w", "W", PluginWidgetAvailability.Available, true, Validate(dir, Manifest(Widget())).Widgets, []);

        Assert.NotEqual(first, _catalog.Resolve("w", "gauge", out _)!.CodeRef);
    }

    // ---- manager: approval and loading ----

    [Fact]
    public async Task A_plugin_whose_widgets_declare_options_waits_for_approval_and_then_offers_them()
    {
        InstallJsPlugin("gaugeplug", """[ { "id": "gauge", "name": "Gauge", "entry": "widgets/gauge.js", "options": { "keepLoaded": {} } } ]""");

        await _manager.StartAsync(CancellationToken.None);

        var waiting = Assert.Single(_manager.Plugins);
        Assert.Equal(PluginLoadStatus.NeedsApproval, waiting.Status);
        Assert.Contains("widget:gauge:keepLoaded", waiting.PendingPermissions!);
        Assert.Null(_catalog.Resolve("gaugeplug", "gauge", out var reason));
        Assert.Equal("needsApproval", reason);

        var loaded = await _manager.ApproveAsync("gaugeplug");

        Assert.Equal(PluginLoadStatus.Loaded, loaded!.Status);
        Assert.NotNull(_catalog.Resolve("gaugeplug", "gauge", out _));
        Assert.False(_catalog.Resolve("gaugeplug", "gauge", out _)!.Verified);
    }

    [Fact]
    public async Task A_plugin_with_widgets_but_no_options_or_permissions_loads_without_a_prompt()
    {
        InstallJsPlugin("plain", """[ { "id": "gauge", "name": "Gauge", "entry": "widgets/gauge.js" } ]""");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Loaded, Assert.Single(_manager.Plugins).Status);
        Assert.NotNull(_catalog.Resolve("plain", "gauge", out _));
    }

    [Fact]
    public async Task A_bad_widget_is_left_out_on_its_own_and_reported_while_the_plugin_and_its_other_widgets_run()
    {
        InstallJsPlugin("mixed", """[ { "id": "good", "name": "Good", "entry": "widgets/gauge.js" }, { "id": "bad", "name": "Bad", "entry": "widgets/nope.js" } ]""");

        await _manager.StartAsync(CancellationToken.None);

        Assert.Equal(PluginLoadStatus.Loaded, Assert.Single(_manager.Plugins).Status);
        Assert.NotNull(_catalog.Resolve("mixed", "good", out _));
        Assert.Null(_catalog.Resolve("mixed", "bad", out var reason));
        Assert.Equal("invalid", reason);
        var problem = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.WidgetInvalid);
        Assert.Contains("bad", problem.Message);
    }

    [Fact]
    public async Task Removing_the_plugin_removes_its_widgets()
    {
        InstallJsPlugin("plain", """[ { "id": "gauge", "name": "Gauge", "entry": "widgets/gauge.js" } ]""");
        await _manager.StartAsync(CancellationToken.None);

        await _manager.UninstallAsync("plain");

        Assert.Null(_catalog.Resolve("plain", "gauge", out var reason));
        Assert.Equal("missing", reason);
    }

    // ---- events ----

    [Fact]
    public void A_retained_event_is_given_to_a_widget_that_comes_on_screen_later()
    {
        var seen = new List<PluginWidgetEvent>();
        _events.Posted += seen.Add;

        _events.Post("W", new PluginWidgetEvent("w", "gauge", "status", JsonValue.Create("ok"), null, null, Retain: true));
        _events.Post("W", new PluginWidgetEvent("w", "gauge", "ping", JsonValue.Create(1), null, null, Retain: false));
        _events.Post("W", new PluginWidgetEvent("w", "gauge", "status", JsonValue.Create("fine"), null, null, Retain: true));

        Assert.Equal(3, seen.Count);
        var retained = Assert.Single(_events.RetainedFor("w", "gauge", "any", "phone"));
        Assert.Equal("fine", retained.Data!.GetValue<string>());
    }

    [Fact]
    public void A_retained_event_for_one_device_is_not_given_to_another()
    {
        _events.Post("W", new PluginWidgetEvent("w", "gauge", "status", JsonValue.Create("x"), null, "tablet", Retain: true));

        Assert.Empty(_events.RetainedFor("w", "gauge", "any", "phone"));
        Assert.Single(_events.RetainedFor("w", "gauge", "any", "tablet"));
    }

    [Fact]
    public void Too_large_and_too_many_events_are_dropped_with_one_report()
    {
        var seen = 0;
        _events.Posted += _ => seen++;

        _events.Post("W", new PluginWidgetEvent("w", "gauge", "big", JsonValue.Create(new string('x', PluginWidgetEventHub.MaxEventBytes + 1)), null, null, false));
        for (var i = 0; i < 100; i++)
            _events.Post("W", new PluginWidgetEvent("w", "gauge", "n", JsonValue.Create(i), null, null, false));

        Assert.Equal(PluginWidgetEventHub.MaxEventsPerSecond, seen);
        Assert.Equal(2, _problems.Snapshot().Count(p => p.Code == ProblemCodes.WidgetEventDropped));
    }
}
