using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class SelfVariableTests : IDisposable
{
    private sealed class TestSocket : WebSocket
    {
        public List<Envelope> Sent { get; } = [];
        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            SendAsync((ReadOnlyMemory<byte>)buffer, messageType, endOfMessage, cancellationToken).AsTask();
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
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-self-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ClientSession Identified(TestSocket socket) =>
        new(socket) { DeviceId = "d-" + Guid.NewGuid().ToString("N"), ProfileId = "p", PageId = "a" };

    private static Widget RedWhen(string id, string variable, string value) => new()
    {
        Id = id,
        Dynamic = new Dictionary<string, DynamicBinding>
        {
            ["style.background"] = new DynamicBinding { Cases = [new DynamicCase(new ConditionNode { Variable = variable, Operator = "==", Value = value }, "#ff0000")] },
        },
    };

    private (WidgetStateService Service, VariableStore Variables, ToggleStateStore Toggles, LastResultStore Results, SessionRegistry Sessions) Make(params Widget[] widgets)
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [.. widgets] }] });
        var variables = new VariableStore();
        var toggles = new ToggleStateStore();
        var results = new LastResultStore();
        var sessions = new SessionRegistry();
        var service = new WidgetStateService(variables, sessions, profiles, toggles, new LayoutSender(new AssetStore()), new WebViewState(),
            NullLogger<WidgetStateService>.Instance, results: results);
        return (service, variables, toggles, results, sessions);
    }

    [Fact]
    public void The_scope_answers_the_self_names_and_forwards_the_rest()
    {
        var variables = new VariableStore();
        variables.Set("test.value", 7);
        var toggles = new ToggleStateStore();
        var results = new LastResultStore();
        var session = Identified(new TestSocket());
        var widget = new Widget { Id = "w" };
        var scope = new SelfVariableScope(variables, session, widget, toggles, results);

        Assert.Equal(false, scope.Get("self.toggled"));
        Assert.Equal(false, scope.Get("self.busy"));
        Assert.Equal(false, scope.Get("self.pressed"));
        Assert.Equal("", scope.Get("self.lastResult"));
        Assert.Equal("", scope.Get("self.lastError"));
        Assert.Equal(7, scope.Get("test.value"));

        toggles.Toggle("w");
        session.Busy["w"] = true;
        session.Pressed["w"] = DateTime.UtcNow;
        results.Set("w", [new ActionFailure("core.web", ActionFailureCode.NotFound, "x"), new ActionFailure("core.web", ActionFailureCode.Timeout, "y")]);
        Assert.Equal(true, scope.Get("self.toggled"));
        Assert.Equal(true, scope.Get("self.busy"));
        Assert.Equal(true, scope.Get("self.pressed"));
        Assert.Equal("Failed", scope.Get("self.lastResult"));
        Assert.Equal("NotFound", scope.Get("self.lastError"));

        results.Set("w", []);
        Assert.Equal("Success", scope.Get("self.lastResult"));
        Assert.Equal("", scope.Get("self.lastError"));
    }

    [Fact]
    public void An_unknown_self_name_is_unavailable_and_a_write_never_reaches_the_store()
    {
        var variables = new VariableStore();
        var scope = new SelfVariableScope(variables, Identified(new TestSocket()), new Widget { Id = "w" }, new ToggleStateStore(), null);
        Assert.Null(scope.Get("self.typo"));
        scope.Set("self.busy", true);
        Assert.Null(variables.Get("self.busy"));
        scope.Set("test.a", 1);
        Assert.Equal(1, variables.Get("test.a"));
    }

    [Fact]
    public void A_plugin_store_ignores_self_names()
    {
        var variables = new VariableStore();
        var store = new TrackingVariableStore(variables);
        store.Set("self.busy", true);
        Assert.Null(variables.Get("self.busy"));
    }

    [Fact]
    public void The_catalog_lists_the_five_names_with_types()
    {
        var all = new SelfVariableCatalog().Describe().ToList();
        Assert.Equal(5, all.Count);
        Assert.All(all, v => Assert.Equal(SelfVariables.Category, v.Category));
        Assert.Equal(VariableType.Boolean, all.Single(v => v.Name == "self.busy").Type);
        Assert.Contains("Failed", all.Single(v => v.Name == "self.lastResult").Values!);
        Assert.Contains("NotFound", all.Single(v => v.Name == "self.lastError").Values!);
    }

    [Fact]
    public async Task Refresh_sends_the_new_look_once_and_nothing_when_it_did_not_change()
    {
        var (service, _, toggles, _, sessions) = Make(RedWhen("w", "self.toggled", "true"));
        using var _ = service;
        var socket = new TestSocket();
        sessions.Add(Identified(socket));

        toggles.Toggle("w");
        service.Refresh("w");
        await service.FlushAsync();
        Assert.Equal("#ff0000", (string?)Assert.Single(socket.Sent).Data!["style"]!["background"]);

        service.Refresh("w");
        await service.FlushAsync();
        Assert.Single(socket.Sent);
    }

    [Fact]
    public async Task A_refresh_for_one_device_sends_nothing_to_the_other()
    {
        var (service, _, _, _, sessions) = Make(RedWhen("w", "self.busy", "true"));
        using var _ = service;
        var a = new TestSocket();
        var b = new TestSocket();
        var sessionA = Identified(a);
        sessions.Add(sessionA);
        sessions.Add(Identified(b));

        sessionA.Busy["w"] = true;
        service.Refresh("w", sessionA);
        await service.FlushAsync();

        Assert.Single(a.Sent);
        Assert.Empty(b.Sent);
    }

    [Fact]
    public async Task A_refresh_without_a_session_updates_every_device()
    {
        var (service, _, toggles, _, sessions) = Make(RedWhen("w", "self.toggled", "true"));
        using var _ = service;
        var a = new TestSocket();
        var b = new TestSocket();
        sessions.Add(Identified(a));
        sessions.Add(Identified(b));

        toggles.Toggle("w");
        service.Refresh("w");
        await service.FlushAsync();

        Assert.Single(a.Sent);
        Assert.Single(b.Sent);
    }

    [Fact]
    public async Task A_widget_with_a_self_rule_is_resolved_when_a_page_is_shown()
    {
        var (service, _, toggles, _, _) = Make();
        using var _ = service;
        var socket = new TestSocket();
        var page = new Page { Id = "a", Widgets = [RedWhen("w", "self.toggled", "true")] };
        toggles.Toggle("w");

        await service.SendInitialAsync(Identified(socket), page, default);

        Assert.Equal("#ff0000", (string?)Assert.Single(socket.Sent).Data!["style"]!["background"]);
    }

    [Fact]
    public async Task A_fast_action_never_becomes_busy()
    {
        var (service, _, _, _, sessions) = Make(RedWhen("w", "self.busy", "true"));
        using var _ = service;
        service.BusyDelay = TimeSpan.FromMilliseconds(300);
        var socket = new TestSocket();
        var session = Identified(socket);
        sessions.Add(session);

        var result = await service.RunBusyAsync(session, "w", () => Task.FromResult(5));
        await service.FlushAsync();

        Assert.Equal(5, result);
        Assert.Empty(socket.Sent);
    }

    [Fact]
    public async Task A_slow_action_shows_busy_once_and_the_resting_look_when_it_ends()
    {
        var (service, _, _, _, sessions) = Make(RedWhen("w", "self.busy", "true"));
        using var _ = service;
        service.BusyDelay = TimeSpan.FromMilliseconds(30);
        var socket = new TestSocket();
        var session = Identified(socket);
        sessions.Add(session);
        var release = new TaskCompletionSource();

        var run = service.RunBusyAsync(session, "w", async () => { await release.Task; return 1; });
        await Task.Delay(150);
        Assert.True(session.Busy.ContainsKey("w"));
        await service.FlushAsync();
        Assert.Equal("#ff0000", (string?)Assert.Single(socket.Sent).Data!["style"]!["background"]);

        release.SetResult();
        await run;
        Assert.False(session.Busy.ContainsKey("w"));
        await service.FlushAsync();
        Assert.Equal(2, socket.Sent.Count);
        Assert.Empty(Assert.IsType<System.Text.Json.Nodes.JsonObject>(socket.Sent[1].Data!["style"]));
    }

    [Fact]
    public async Task Busy_is_cleared_when_the_work_throws()
    {
        var (service, _, _, _, sessions) = Make(RedWhen("w", "self.busy", "true"));
        using var _ = service;
        service.BusyDelay = TimeSpan.FromMilliseconds(20);
        var session = Identified(new TestSocket());
        sessions.Add(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunBusyAsync<int>(session, "w", async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException();
        }));

        Assert.False(session.Busy.ContainsKey("w"));
    }

    [Fact]
    public async Task Busy_is_not_shown_on_another_device()
    {
        var (service, _, _, _, sessions) = Make(RedWhen("w", "self.busy", "true"));
        using var _ = service;
        service.BusyDelay = TimeSpan.FromMilliseconds(20);
        var other = new TestSocket();
        var session = Identified(new TestSocket());
        sessions.Add(session);
        sessions.Add(Identified(other));

        await service.RunBusyAsync(session, "w", async () => { await Task.Delay(100); return 1; });
        await service.FlushAsync();

        Assert.Empty(other.Sent);
    }

    [Fact]
    public void The_last_result_is_empty_before_a_run_then_follows_each_run()
    {
        var results = new LastResultStore();
        Assert.Equal(("", ""), results.Get("w"));

        results.Set("w", [new ActionFailure("core.web", ActionFailureCode.NotFound, "x")]);
        Assert.Equal(("Failed", "NotFound"), results.Get("w"));
        Assert.Equal(("", ""), results.Get("other"));

        results.Set("w", []);
        Assert.Equal(("Success", ""), results.Get("w"));
    }

    [Fact]
    public async Task A_quick_tap_keeps_pressed_on_until_the_minimum_has_passed()
    {
        var (service, _, _, _, sessions) = Make();
        using var _ = service;
        service.PressedMinimum = TimeSpan.FromMilliseconds(150);
        var session = Identified(new TestSocket());
        sessions.Add(session);

        service.PressBegan(session, "w");
        service.PressEnded(session, "w");
        Assert.True(session.Pressed.ContainsKey("w"));

        await Task.Delay(400);
        Assert.False(session.Pressed.ContainsKey("w"));
    }

    [Fact]
    public async Task A_second_press_is_not_cleared_by_the_first_release()
    {
        var (service, _, _, _, sessions) = Make();
        using var _ = service;
        service.PressedMinimum = TimeSpan.FromMilliseconds(150);
        var session = Identified(new TestSocket());
        sessions.Add(session);

        service.PressBegan(session, "w");
        service.PressEnded(session, "w");
        await Task.Delay(30);
        service.PressBegan(session, "w");
        await Task.Delay(300);

        Assert.True(session.Pressed.ContainsKey("w"));
    }

    [Fact]
    public async Task A_long_hold_is_cleared_at_once_on_release()
    {
        var (service, _, _, _, sessions) = Make();
        using var _ = service;
        service.PressedMinimum = TimeSpan.FromMilliseconds(30);
        var session = Identified(new TestSocket());
        sessions.Add(session);

        service.PressBegan(session, "w");
        await Task.Delay(100);
        service.PressEnded(session, "w");

        Assert.False(session.Pressed.ContainsKey("w"));
    }

    [Fact]
    public async Task A_page_change_clears_a_held_button_and_the_look_is_resting_when_the_page_is_shown_again()
    {
        var button = new Widget
        {
            Id = "w",
            Dynamic = new Dictionary<string, DynamicBinding>
            {
                ["style.background"] = new DynamicBinding { Cases = [new DynamicCase(new ConditionNode { Variable = "self.pressed", Operator = "==", Value = "true" }, "#ff0000")] },
            },
        };
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [button] }, new Page { Id = "b", Widgets = [] }] });
        using var service = new WidgetStateService(new VariableStore(), new SessionRegistry(), profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), new WebViewState(),
            NullLogger<WidgetStateService>.Instance, results: new LastResultStore());
        var socket = new TestSocket();
        var session = Identified(socket);
        var controller = new SessionDeviceController(session, profiles, service);

        service.PressBegan(session, "w");
        await controller.ShowPageAsync("b");

        Assert.Empty(session.Pressed);
    }

    [Fact]
    public async Task Removing_a_variable_a_text_uses_shows_n_a_on_every_device_and_does_not_mark_the_widget()
    {
        var (service, variables, _, _, sessions) = Make(new Widget { Id = "w", Text = "{plug.value}" });
        using var _ = service;
        var phone = new TestSocket();
        var browser = new TestSocket();
        sessions.Add(Identified(phone));
        sessions.Add(Identified(browser));
        variables.Set("plug.value", "x");
        await service.FlushAsync();
        phone.Sent.Clear();
        browser.Sent.Clear();

        variables.Remove("plug.value"); // what switching a plugin off does to its variables
        await service.FlushAsync();

        foreach (var socket in new[] { phone, browser })
        {
            var message = Assert.Single(socket.Sent).Data!;
            Assert.Equal("n/a", (string?)message["text"]);
            Assert.Null(message["unavailable"]);
        }
    }

    [Fact]
    public async Task An_asset_request_for_an_unknown_hash_is_answered_with_an_empty_asset()
    {
        // The phone uses exactly this request as its liveness probe, so the reply is part of the contract.
        var socket = new TestSocket();
        var session = new ClientSession(socket);

        await new LayoutSender(new AssetStore()).SendAssetsAsync(session, ["0000000000000000"]);

        var reply = Assert.Single(socket.Sent);
        Assert.Equal(MessageTypes.Asset, reply.Type);
        var asset = reply.DataAs<AssetMessage>()!;
        Assert.Equal("0000000000000000", asset.Hash);
        Assert.Null(asset.Data);
    }

    [Fact]
    public async Task A_press_still_held_when_the_device_goes_is_released_once()
    {
        var handler = new ReleaseAwareHandler();
        var button = new Widget { Id = "w", Actions = { [WidgetEvents.Press] = [new ActionBinding(handler.Type, [])] } };
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [button] }] });
        using var service = new WidgetStateService(new VariableStore(), new SessionRegistry(), profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), new WebViewState(),
            NullLogger<WidgetStateService>.Instance, results: new LastResultStore());
        var hub = new ClientHub(new SessionRegistry(), profiles, new ActionDispatcher([handler], NullLogger<ActionDispatcher>.Instance), new ToggleStateStore(), service,
            new LastResultStore(), null!, null!, null!, null!, NullLogger<ClientHub>.Instance, null!, null!, new ProblemList());
        var session = Identified(new TestSocket());
        session.Held["w"] = "a"; // the device pressed the button and vanished before it reported the release
        var queue = Channel.CreateUnbounded<Func<Task>>();

        hub.ReleaseHeld(session, queue.Writer);
        hub.ReleaseHeld(session, queue.Writer); // a second call finds nothing left to release
        queue.Writer.Complete();
        await foreach (var work in queue.Reader.ReadAllAsync()) await work();

        Assert.Equal(["release"], handler.Calls);
        Assert.Empty(session.Held);
    }

    private sealed class FailingHandler(bool fail) : IActionHandler
    {
        public bool Fail { get; set; } = fail;
        public string Type => "test.failing";
        public string DisplayName => "Failing";
        public Task ExecuteAsync(ActionContext context, System.Text.Json.Nodes.JsonObject settings, CancellationToken cancellationToken) =>
            Fail ? throw new InvalidOperationException("boom") : Task.CompletedTask;
    }

    [Fact]
    public async Task A_failed_action_is_one_counted_Error_List_line_that_the_next_success_removes()
    {
        var handler = new FailingHandler(true);
        var button = new Widget { Id = "w", Name = "Btn", Actions = { [WidgetEvents.Release] = [new ActionBinding(handler.Type, [])] } };
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Name = "Main", Widgets = [button] }] });
        using var service = new WidgetStateService(new VariableStore(), new SessionRegistry(), profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), new WebViewState(),
            NullLogger<WidgetStateService>.Instance, results: new LastResultStore());
        var problems = new ProblemList();
        var hub = new ClientHub(new SessionRegistry(), profiles, new ActionDispatcher([handler], NullLogger<ActionDispatcher>.Instance), new ToggleStateStore(), service,
            new LastResultStore(), null!, null!, null!, null!, NullLogger<ClientHub>.Instance, new PluginLocalizer(() => "en"), null!, problems);

        async Task ReleaseOnce()
        {
            var session = Identified(new TestSocket());
            session.Held["w"] = "a";
            var queue = Channel.CreateUnbounded<Func<Task>>();
            hub.ReleaseHeld(session, queue.Writer);
            queue.Writer.Complete();
            await foreach (var work in queue.Reader.ReadAllAsync()) await work();
        }

        await ReleaseOnce();
        await ReleaseOnce();
        var line = Assert.Single(problems.Snapshot());
        Assert.Equal(ProblemCodes.ActionFailed, line.Code);
        Assert.Equal(2, line.Count);
        Assert.Equal("w", line.Target?.WidgetId);

        handler.Fail = false;
        await ReleaseOnce();
        Assert.Empty(problems.Snapshot());
    }

    [Fact]
    public async Task A_connected_device_gets_the_profile_list_again_when_a_profile_is_added_renamed_or_deleted()
    {
        var profiles = new ProfileStore(_dir);
        var devices = new DeviceStore(_dir);
        var paired = devices.Pair("phone-1", "Phone");
        var socket = new TestSocket();
        var sessions = new SessionRegistry();
        sessions.Add(new ClientSession(socket) { DeviceId = paired.Id, ProfileId = profiles.First().Id });
        var hub = new ClientHub(sessions, profiles, new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance), new ToggleStateStore(), null!,
            new LastResultStore(), devices, null!, null!, null!, NullLogger<ClientHub>.Instance, null!, null!, new ProblemList());
        hub.ListenForProfileChanges(); // the hub starts listening on the first connection; here it is started directly

        async Task<ProfilesListPayload> NextList(Action change)
        {
            socket.Sent.Clear();
            change();
            for (var i = 0; i < 100 && socket.Sent.Count == 0; i++) await Task.Delay(20);
            var message = Assert.Single(socket.Sent);
            Assert.Equal(MessageTypes.ProfilesList, message.Type);
            return message.DataAs<ProfilesListPayload>()!;
        }

        var added = await NextList(() => profiles.Save(new Profile { Id = "new", Name = "Streaming", Pages = [new Page { Id = "a", Name = "Main" }] }));
        Assert.Contains(added.Profiles, p => p.Id == "new" && p.Name == "Streaming");

        var renamed = await NextList(() => profiles.Save(new Profile { Id = "new", Name = "Gaming", Pages = [new Page { Id = "a", Name = "Main" }] }));
        Assert.Contains(renamed.Profiles, p => p.Id == "new" && p.Name == "Gaming");

        var deleted = await NextList(() => profiles.Delete("new"));
        Assert.DoesNotContain(deleted.Profiles, p => p.Id == "new");

        socket.Sent.Clear();
        profiles.Save(new Profile { Id = profiles.First().Id, Name = profiles.First().Name, Pages = [new Page { Id = "b", Name = "Other" }] }); // content only
        await Task.Delay(100);
        Assert.Empty(socket.Sent);
    }
}
