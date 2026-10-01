using System.Net.WebSockets;
using System.Text;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
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
}
