using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginWidgetRouterTests : IAsyncLifetime
{
    private sealed class RecordingSocket : WebSocket
    {
        public List<Envelope> Sent { get; } = [];
        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            lock (Sent) Sent.Add(Envelope.Parse(Encoding.UTF8.GetString(buffer))!);
            return Task.CompletedTask;
        }
        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            lock (Sent) Sent.Add(Envelope.Parse(Encoding.UTF8.GetString(buffer.Span))!);
            return ValueTask.CompletedTask;
        }
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public List<Envelope> Of(string type) { lock (Sent) return [.. Sent.Where(e => e.Type == type)]; }
    }

    private sealed class FakeHandler(Func<PluginWidgetMessage, CancellationToken, Task<JsonNode?>> onMessage) : IPluginWidgetHandler
    {
        public List<PluginWidgetMessage> Seen { get; } = [];
        public Task<JsonNode?> OnWidgetMessageAsync(PluginWidgetMessage message, CancellationToken cancellationToken)
        {
            lock (Seen) Seen.Add(message);
            return onMessage(message, cancellationToken);
        }
    }

    private sealed class FakeHost : IPluginWidgetHost
    {
        public IPluginWidgetHandler? Handler { get; set; }
        public IPluginWidgetHandler? GetWidgetHandler(string pluginId) => pluginId == "gp" ? Handler : null;
    }

    private sealed class FakeAction(string type, Action<ActionContext>? onRun = null) : IActionHandler
    {
        public string Type => type;
        public string DisplayName => type;
        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            onRun?.Invoke(context);
            if (settings["fail"] is not null) throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        }
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-router-" + Guid.NewGuid().ToString("N"));
    private readonly AssetStore _assets = new();
    private readonly ProblemList _problems = new();
    private readonly PluginWidgetCatalog _catalog;
    private readonly PluginWidgetEventHub _events;
    private readonly VariableStore _variables = new();
    private readonly SessionRegistry _sessions = new();
    private readonly FakeHost _host = new();
    private readonly ActionDispatcher _dispatcher = new([], NullLogger<ActionDispatcher>.Instance);
    private readonly PluginPermissionStore _permissions;
    private readonly ProfileStore _profiles;
    private readonly RecordingSocket _socket = new();
    private readonly ClientSession _session;
    private readonly List<ActionContext> _ran = [];
    private DateTimeOffset _now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private PluginWidgetRouter _router = null!;

    public PluginWidgetRouterTests()
    {
        Directory.CreateDirectory(_root);
        _catalog = new PluginWidgetCatalog(_assets);
        _events = new PluginWidgetEventHub(_problems);
        _permissions = new PluginPermissionStore(_root);
        _profiles = new ProfileStore(_root);
        _session = new ClientSession(_socket)
        {
            Capabilities = [ClientCapabilities.Assets, ClientCapabilities.PluginWidgets],
            DeviceId = "phone",
            DeviceName = "Phone",
        };
        _sessions.Add(_session);
        _session.ProfileId = "prof";
        _session.PageId = "page";
        _dispatcher.Register(new FakeAction("gp.act", _ran.Add));
        _dispatcher.Register(new FakeAction("other.act", _ran.Add));
    }

    public async Task InitializeAsync()
    {
        var script = Path.Combine(_root, "g.js");
        File.WriteAllText(script, "// code");
        var widget = new ValidatedWidget(
            new PluginWidgetManifest
            {
                Id = "gauge", Name = "Gauge", Entry = "g.js", Interactive = true,
                Settings = [new SettingField("source", "Value", SettingFieldKind.Variable)],
            },
            script, [], 15, new PluginWidgetSize(2, 2), []);
        _catalog.Set("gp", "Gauge plugin", PluginWidgetAvailability.Available, true, [widget], [], PluginKind.Csharp);
        SaveProfile(PlaceWidget("w1", "gp", "gauge", new JsonObject { ["source"] = "sys.cpu" }));
        _router = NewRouter();
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _router.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return Task.CompletedTask;
    }

    private PluginWidgetRouter NewRouter() => new(_catalog, _host, _permissions, _profiles, _sessions, _dispatcher, _variables, _events, _problems,
        NullLogger<PluginWidgetRouter>.Instance, () => _now);

    private static Widget PlaceWidget(string id, string plugin, string widget, JsonObject? settings = null) => new()
    {
        Id = id, Type = WidgetTypes.PluginWidget, W = 2, H = 2,
        Props = new JsonObject { ["plugin"] = plugin, ["widget"] = widget, ["settings"] = settings ?? [] },
    };

    private void SaveProfile(params Widget[] widgets)
    {
        var page = new Page { Id = "page", Cols = 8, Rows = 6 };
        // Distinct cells so the validator is happy.
        for (var i = 0; i < widgets.Length; i++) { widgets[i].X = (i * 2) % 8; widgets[i].Y = (i * 2) / 8 * 2; page.Widgets.Add(widgets[i]); }
        _profiles.Save(new Profile { Id = "prof", Name = "Prof", Pages = [page] });
    }

    private static PluginWidgetRequestMessage Msg(string kind, JsonNode? data = null, int? id = 1, string widgetId = "w1", bool gesture = false) =>
        new("page", widgetId, kind, id, data, gesture);

    private Task Send(PluginWidgetRequestMessage message) => _router.HandleRequestAsync(_session, message, null!, CancellationToken.None);

    private PluginWidgetReplyMessage LastReply() => _socket.Of(MessageTypes.PluginWidgetReply).Last().DataAs<PluginWidgetReplyMessage>()!;

    private static FakeHandler Echo() => new((m, _) => Task.FromResult<JsonNode?>(new JsonObject { ["echo"] = m.Data?.DeepClone(), ["settings"] = m.Settings.DeepClone(), ["widget"] = m.Widget }));

    // ---- request ----

    [Fact]
    public async Task A_request_reaches_the_plugin_with_the_placed_widgets_settings_and_the_answer_comes_back_with_the_same_id()
    {
        var handler = Echo();
        _host.Handler = handler;

        await Send(Msg(PluginWidgetKinds.Request, new JsonObject { ["op"] = "x" }, id: 7));

        var reply = LastReply();
        Assert.True(reply.Ok);
        Assert.Equal(7, reply.Id);
        Assert.Equal("w1", reply.WidgetId);
        Assert.Equal("x", reply.Data!["echo"]!["op"]!.GetValue<string>());
        Assert.Equal("sys.cpu", reply.Data["settings"]!["source"]!.GetValue<string>());
        var seen = Assert.Single(handler.Seen);
        Assert.Equal("gauge", seen.Widget);
        Assert.Equal("phone", seen.DeviceId);
        Assert.Equal("w1", seen.WidgetId);
    }

    [Fact]
    public async Task A_widget_that_is_not_in_the_profile_this_device_has_open_is_refused_and_nothing_runs()
    {
        var handler = Echo();
        _host.Handler = handler;

        await Send(Msg(PluginWidgetKinds.Request, id: 1, widgetId: "not-there"));

        Assert.Equal("not_allowed", LastReply().Error);
        Assert.Empty(handler.Seen);
    }

    [Fact]
    public async Task A_widget_of_another_type_cannot_be_used_as_a_plugin_widget()
    {
        var handler = Echo();
        _host.Handler = handler;
        SaveProfile(new Widget { Id = "w1", Type = WidgetTypes.Button });

        await Send(Msg(PluginWidgetKinds.Request));

        Assert.Equal("not_allowed", LastReply().Error);
        Assert.Empty(handler.Seen);
    }

    [Fact]
    public async Task A_widget_whose_plugin_is_not_loaded_gets_plugin_unavailable()
    {
        SaveProfile(PlaceWidget("w1", "gone", "gauge"));

        await Send(Msg(PluginWidgetKinds.Request));

        Assert.Equal("plugin_unavailable", LastReply().Error);
    }

    [Fact]
    public async Task A_plugin_without_a_handler_answers_not_allowed()
    {
        await Send(Msg(PluginWidgetKinds.Request));

        Assert.Equal("not_allowed", LastReply().Error);
    }

    [Fact]
    public async Task A_throwing_handler_is_a_failed_reply_and_an_error_list_entry_naming_the_plugin()
    {
        _host.Handler = new FakeHandler((_, _) => throw new InvalidOperationException("nope"));

        await Send(Msg(PluginWidgetKinds.Request));

        Assert.Equal("failed", LastReply().Error);
        var problem = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.WidgetCallFailed);
        Assert.Equal("gp", problem.Source);
        Assert.Contains("nope", problem.Message);
    }

    [Fact]
    public async Task A_too_large_request_and_a_too_large_reply_are_refused()
    {
        _host.Handler = new FakeHandler((_, _) => Task.FromResult<JsonNode?>(JsonValue.Create(new string('x', PluginWidgetRouter.MaxReplyBytes + 10))));

        await Send(Msg(PluginWidgetKinds.Request, JsonValue.Create(new string('y', PluginWidgetRouter.MaxRequestBytes + 10)), id: 1));
        Assert.Equal("too_large", LastReply().Error);

        await Send(Msg(PluginWidgetKinds.Request, JsonValue.Create("small"), id: 2));
        Assert.Equal("too_large", LastReply().Error);
    }

    [Fact]
    public async Task Requests_are_rate_limited_per_widget()
    {
        _host.Handler = Echo();

        for (var i = 1; i <= PluginWidgetRouter.MaxRequestsPerSecondPerWidget; i++)
        {
            await Send(Msg(PluginWidgetKinds.Request, id: i));
            Assert.True(LastReply().Ok);
        }
        await Send(Msg(PluginWidgetKinds.Request, id: 99));
        Assert.Equal("rate_limited", LastReply().Error);

        _now += TimeSpan.FromSeconds(1);
        await Send(Msg(PluginWidgetKinds.Request, id: 100));
        Assert.True(LastReply().Ok);
    }

    [Fact]
    public async Task At_most_four_requests_of_one_widget_are_in_flight()
    {
        var gate = new TaskCompletionSource();
        _host.Handler = new FakeHandler(async (_, _) => { await gate.Task; return null; });

        var pending = Enumerable.Range(1, PluginWidgetRouter.MaxInFlightPerWidget).Select(i => Send(Msg(PluginWidgetKinds.Request, id: i))).ToList();
        await Send(Msg(PluginWidgetKinds.Request, id: 50));

        Assert.Equal("rate_limited", LastReply().Error);
        gate.SetResult();
        await Task.WhenAll(pending);
        Assert.Equal(PluginWidgetRouter.MaxInFlightPerWidget, _socket.Of(MessageTypes.PluginWidgetReply).Count(e => e.DataAs<PluginWidgetReplyMessage>()!.Ok));
    }

    [Fact]
    public async Task A_message_to_a_client_without_the_capability_is_ignored()
    {
        _host.Handler = Echo();
        _session.Capabilities = [ClientCapabilities.Assets];

        await Send(Msg(PluginWidgetKinds.Request));

        Assert.Empty(_socket.Of(MessageTypes.PluginWidgetReply));
    }

    // ---- run ----

    [Fact]
    public async Task A_widget_runs_only_its_own_plugins_actions_and_the_gesture_flag_reaches_the_action()
    {
        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "gp.act", ["settings"] = new JsonObject() }, id: 1, gesture: true));
        Assert.True(LastReply().Ok);
        Assert.True(Assert.Single(_ran).UserGesture);
        Assert.Equal("w1", _ran[0].WidgetId);

        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "gp.act" }, id: 2, gesture: false));
        Assert.False(_ran[1].UserGesture);

        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "other.act" }, id: 3));
        Assert.Equal("not_allowed", LastReply().Error);
        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "core.page" }, id: 4));
        Assert.Equal("not_allowed", LastReply().Error);
        Assert.Equal(2, _ran.Count);
    }

    [Fact]
    public async Task A_failing_action_is_a_failed_reply_and_reported()
    {
        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "gp.act", ["settings"] = new JsonObject { ["fail"] = true } }));

        Assert.Equal("failed", LastReply().Error);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.WidgetCallFailed && p.Source == "gp");
    }

    [Fact]
    public async Task A_javascript_plugin_needs_the_actions_permission_to_run_actions()
    {
        _catalog.Set("gp", "Gauge plugin", PluginWidgetAvailability.Available, false, [_catalog.Resolve("gp", "gauge", out _)!.Widget], [], PluginKind.Js);
        // Not granted yet.
        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "gp.act" }, id: 1));
        Assert.Equal("not_allowed", LastReply().Error);

        _permissions.Grant("gp", ["actions"]);
        await Send(Msg(PluginWidgetKinds.Run, new JsonObject { ["action"] = "gp.act" }, id: 2));
        Assert.True(LastReply().Ok);
    }

    // ---- variables ----

    private static JsonObject Names(params string[] names) => new() { ["variables"] = new JsonArray(names.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray()) };

    [Fact]
    public async Task A_widget_may_subscribe_to_its_own_plugins_variables_and_the_ones_the_person_bound_and_nothing_else()
    {
        _variables.Set("gp.count", 3.0);
        _variables.Set("sys.cpu", 41.0);
        _variables.Set("sys.secret", "no");
        _variables.Set("other.value", 1.0);

        await Send(Msg(PluginWidgetKinds.Subscribe, Names("gp.count", "sys.cpu", "sys.secret", "other.value", "bad name")));

        var vars = Assert.Single(_socket.Of(MessageTypes.PluginWidgetVars)).DataAs<PluginWidgetVarsMessage>()!;
        Assert.Equal(["gp.count", "sys.cpu"], vars.Values.Keys.Order());
        Assert.Equal(41.0, vars.Values["sys.cpu"]!.GetValue<double>());
    }

    [Fact]
    public async Task A_javascript_plugin_needs_the_variables_permission_for_its_own_variables_but_not_for_bound_ones()
    {
        _catalog.Set("gp", "Gauge plugin", PluginWidgetAvailability.Available, false, [_catalog.Resolve("gp", "gauge", out _)!.Widget], [], PluginKind.Js);
        _variables.Set("gp.count", 3.0);
        _variables.Set("sys.cpu", 41.0);

        await Send(Msg(PluginWidgetKinds.Subscribe, Names("gp.count", "sys.cpu")));

        var vars = Assert.Single(_socket.Of(MessageTypes.PluginWidgetVars)).DataAs<PluginWidgetVarsMessage>()!;
        Assert.Equal(["sys.cpu"], vars.Values.Keys);
    }

    [Fact]
    public async Task At_most_32_variables_are_taken()
    {
        var names = Enumerable.Range(0, 40).Select(i => "gp.v" + i).ToArray();
        foreach (var n in names) _variables.Set(n, 1.0);

        await Send(Msg(PluginWidgetKinds.Subscribe, Names(names)));

        Assert.Equal(PluginWidgetRouter.MaxSubscribedVariables, _socket.Of(MessageTypes.PluginWidgetVars).Single().DataAs<PluginWidgetVarsMessage>()!.Values.Count);
    }

    [Fact]
    public async Task A_changed_subscribed_variable_is_pushed_batched_and_an_unchanged_one_is_not()
    {
        _variables.Set("gp.count", 1.0);
        await Send(Msg(PluginWidgetKinds.Subscribe, Names("gp.count")));
        await _router.StartAsync(CancellationToken.None);

        _variables.Set("gp.count", 2.0);
        _variables.Set("gp.other", 9.0); // not subscribed
        await WaitAsync(() => _socket.Of(MessageTypes.PluginWidgetVars).Count >= 2);

        var pushed = _socket.Of(MessageTypes.PluginWidgetVars).Last().DataAs<PluginWidgetVarsMessage>()!;
        Assert.Equal(["gp.count"], pushed.Values.Keys);
        Assert.Equal(2.0, pushed.Values["gp.count"]!.GetValue<double>());
        await _router.StopAsync(CancellationToken.None);
    }

    // ---- events ----

    [Fact]
    public async Task An_event_goes_to_the_matching_widget_on_the_shown_page_and_only_to_the_device_it_names()
    {
        SaveProfile(PlaceWidget("w1", "gp", "gauge"), PlaceWidget("w2", "gp", "gauge"), PlaceWidget("w3", "gp", "other-widget"));

        _events.Post("Gauge plugin", new PluginWidgetEvent("gp", "gauge", "hello", JsonValue.Create(1), WidgetId: "w2", DeviceId: null, Retain: false));
        _events.Post("Gauge plugin", new PluginWidgetEvent("gp", "gauge", "all", JsonValue.Create(2), null, null, false));
        _events.Post("Gauge plugin", new PluginWidgetEvent("gp", "gauge", "tablet", JsonValue.Create(3), null, "tablet", false));
        await WaitAsync(() => _socket.Of(MessageTypes.PluginWidgetEvent).Count >= 3);
        await Task.Delay(100);

        var events = _socket.Of(MessageTypes.PluginWidgetEvent).Select(e => e.DataAs<PluginWidgetEventMessage>()!).ToList();
        Assert.Contains(events, e => e.WidgetId == "w2" && e.Name == "hello");
        Assert.DoesNotContain(events, e => e.WidgetId == "w1" && e.Name == "hello");
        Assert.Equal(2, events.Count(e => e.Name == "all"));
        Assert.DoesNotContain(events, e => e.Name == "tablet");
        Assert.DoesNotContain(events, e => e.WidgetId == "w3");
    }

    [Fact]
    public async Task A_retained_event_is_sent_when_the_widget_says_it_is_ready()
    {
        _events.Post("Gauge plugin", new PluginWidgetEvent("gp", "gauge", "status", JsonValue.Create("ok"), null, null, Retain: true));
        await Task.Delay(50);
        _socket.Sent.Clear();

        await Send(Msg(PluginWidgetKinds.Ready, id: null));

        var sent = Assert.Single(_socket.Of(MessageTypes.PluginWidgetEvent)).DataAs<PluginWidgetEventMessage>()!;
        Assert.Equal("status", sent.Name);
        Assert.Equal("w1", sent.WidgetId);
    }

    // ---- errors ----

    [Fact]
    public void Widget_script_errors_go_to_the_error_list_at_most_five_per_ten_seconds()
    {
        for (var i = 0; i < 10; i++)
            _router.HandleError(_session, new PluginWidgetErrorMessage("page", "w1", "oops " + i));

        Assert.Equal(PluginWidgetRouter.MaxErrorsPer10Seconds, _problems.Snapshot().Count(p => p.Code == ProblemCodes.WidgetScriptError));

        _now += TimeSpan.FromSeconds(11);
        _router.HandleError(_session, new PluginWidgetErrorMessage("page", "w1", "later"));
        Assert.Equal(PluginWidgetRouter.MaxErrorsPer10Seconds + 1, _problems.Snapshot().Count(p => p.Code == ProblemCodes.WidgetScriptError));
    }

    // ---- layout ----

    [Fact]
    public async Task The_layout_of_a_capable_client_carries_the_widgets_runtime_and_a_placeholder_reason_when_it_cannot_run()
    {
        SaveProfile(PlaceWidget("w1", "gp", "gauge"), PlaceWidget("w2", "gone", "x"));
        var sender = new LayoutSender(_assets, _catalog);

        await sender.SendFullAsync(_session, _profiles.Get("prof")!, "page");

        var widgets = _socket.Of(MessageTypes.LayoutFull).Single().Data!["profile"]!["pages"]![0]!["widgets"]!.AsArray();
        var runtime = widgets[0]!["props"]!["runtime"]!;
        Assert.StartsWith("asset:", runtime["code"]!.GetValue<string>());
        Assert.Equal(15, runtime["fps"]!.GetValue<int>());
        Assert.Equal("missing", widgets[1]!["props"]!["runtime"]!["unavailable"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_layout_of_a_client_without_the_capability_has_no_runtime()
    {
        _session.Capabilities = [ClientCapabilities.Assets];
        var sender = new LayoutSender(_assets, _catalog);

        await sender.SendFullAsync(_session, _profiles.Get("prof")!, "page");

        var props = _socket.Of(MessageTypes.LayoutFull).Single().Data!["profile"]!["pages"]![0]!["widgets"]![0]!["props"]!;
        Assert.Null(props["runtime"]);
    }

    [Fact]
    public void The_server_made_runtime_is_stripped_from_a_profile_that_comes_in_and_a_widget_without_ids_is_invalid()
    {
        var widget = PlaceWidget("w1", "gp", "gauge");
        widget.Props!["runtime"] = new JsonObject { ["code"] = "asset:evil" };
        var profile = new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [widget] }] };

        PluginWidgetProps.Strip(profile);

        Assert.Null(widget.Props["runtime"]);
        Assert.True(ProfileValidator.Validate(profile, out _));
        var broken = new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "x", Type = WidgetTypes.PluginWidget, Props = [] }] }] };
        Assert.False(ProfileValidator.Validate(broken, out var error));
        Assert.Contains("plugin and widget", error);
    }

    // ---- JavaScript plugin side ----

    [Fact]
    public async Task A_javascript_plugin_answers_widget_requests_with_a_value_a_promise_or_an_error_and_can_post_events()
    {
        var dir = Path.Combine(_root, "js");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "index.js");
        File.WriteAllText(path, """
            host.widgets.onMessage((m) => {
              if (m.data.op === 'sync') return { got: m.data.n, widget: m.widget, setting: m.settings.source };
              if (m.data.op === 'async') return Promise.resolve({ later: true });
              if (m.data.op === 'throw') throw new Error('bad op');
              return undefined;
            });
            host.widgets.post('gauge', 'ready', { a: 1 }, { retain: true });
            """);
        var manifest = new PluginManifest { Id = "gp", Name = "G", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        using var plugin = new JsPlugin(manifest, path, new JsPermissions([]), _variables, null, NullLogger.Instance, _ => { });
        var host = new PluginHostCollector("1.4.0", dir, "gp", new PluginStatusRegistry(), NullLogger.Instance, null, _events, "G");
        plugin.Initialize(host);

        JsonNode? Ask(string op, int n = 0) => plugin.OnWidgetMessageAsync(
            new PluginWidgetMessage("gauge", "phone", "page", "w1", new JsonObject { ["source"] = "s" }, new JsonObject { ["op"] = op, ["n"] = n }), CancellationToken.None).GetAwaiter().GetResult();

        var sync = Ask("sync", 5)!;
        Assert.Equal(5, sync["got"]!.GetValue<int>());
        Assert.Equal("gauge", sync["widget"]!.GetValue<string>());
        Assert.Equal("s", sync["setting"]!.GetValue<string>());
        Assert.True(Ask("async")!["later"]!.GetValue<bool>());
        Assert.Null(Ask("none"));
        var ex = Assert.Throws<InvalidOperationException>(() => Ask("throw"));
        Assert.Contains("bad op", ex.Message);
        Assert.Single(_events.RetainedFor("gp", "gauge", "w1", "phone"));
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(20);
        Assert.True(condition(), "Condition was not met in time.");
    }
}
