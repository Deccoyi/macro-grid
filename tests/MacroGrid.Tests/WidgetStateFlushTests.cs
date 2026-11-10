using System.Net.WebSockets;
using System.Text;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class WidgetStateFlushTests : IDisposable
{
    /// <summary>A socket whose sends either complete at once or hang until cancelled, like a phone whose Wi-Fi went to sleep.</summary>
    private sealed class TestSocket(bool hangs) : WebSocket
    {
        public List<Envelope> Sent { get; } = [];
        public bool Aborted { get; private set; }

        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            SendAsync((ReadOnlyMemory<byte>)buffer, messageType, endOfMessage, cancellationToken).AsTask();

        public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (hangs) await Task.Delay(Timeout.Infinite, cancellationToken);
            Sent.Add(Envelope.Parse(Encoding.UTF8.GetString(buffer.Span))!);
        }

        public override void Abort() => Aborted = true;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-flush-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static ClientSession Identified(TestSocket socket) =>
        new(socket) { DeviceId = "d-" + Guid.NewGuid().ToString("N"), ProfileId = "p", PageId = "a" };

    private WidgetStateService Service(Widget widget, out ProfileStore profiles)
    {
        profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [widget] }] });
        return new WidgetStateService(new VariableStore(), new SessionRegistry(), profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()),
            NullLogger<WidgetStateService>.Instance);
    }

    [Fact]
    public async Task Initial_state_of_a_widget_is_one_frame()
    {
        using var service = Service(new Widget { Id = "w", Type = WidgetTypes.Toggle, Text = "v={test.value}" }, out var profiles);
        var socket = new TestSocket(hangs: false);
        var session = Identified(socket);

        await service.SendInitialAsync(session, profiles.Get("p")!.Pages[0], default);

        var data = Assert.Single(socket.Sent).DataAs<WidgetStateMessage>()!;
        Assert.Equal("w", data.WidgetId);
        Assert.Equal("v=", data.Text?[..2]);
        Assert.NotNull(data.Active);
        Assert.Null(data.Style);
    }

    [Fact]
    public async Task Initial_state_clears_a_stale_style_with_an_empty_style_not_null()
    {
        using var service = Service(new Widget { Id = "w", Text = "v={test.value}" }, out var profiles);
        var socket = new TestSocket(hangs: false);
        var session = Identified(socket);
        session.SentStyles["w"] = new Dictionary<string, string> { ["color"] = "red" };

        await service.SendInitialAsync(session, profiles.Get("p")!.Pages[0], default);

        var data = Assert.Single(socket.Sent).DataAs<WidgetStateMessage>()!;
        Assert.NotNull(data.Style);
        Assert.Empty(data.Style);
        Assert.Equal("v=", data.Text?[..2]);
    }

    [Fact]
    public async Task A_client_that_does_not_take_frames_is_dropped_and_does_not_hold_up_the_others()
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile
        {
            Id = "p",
            Name = "P",
            Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "w", Text = "v={test.value}" }] }],
        });

        var variables = new VariableStore();
        var sessions = new SessionRegistry();
        var slow = new TestSocket(hangs: true);
        var healthy = new TestSocket(hangs: false);
        sessions.Add(Identified(slow));
        sessions.Add(Identified(healthy));

        using var service = new WidgetStateService(variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()),
            NullLogger<WidgetStateService>.Instance) { SendTimeout = TimeSpan.FromMilliseconds(200) };

        variables.Set("test.value", 7);
        await service.FlushAsync();

        Assert.True(slow.Aborted);
        Assert.False(healthy.Aborted);
        var message = Assert.Single(healthy.Sent);
        Assert.Equal(MessageTypes.WidgetState, message.Type);
    }

    [Fact]
    public async Task A_removed_web_widget_gets_no_live_text_or_style()
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "w", Type = WidgetTypes.Web, Text = "v={test.value}" }] }] });
        var variables = new VariableStore();
        var sessions = new SessionRegistry();
        var socket = new TestSocket(hangs: false);
        var session = Identified(socket);
        session.ProfileId = "p";
        session.PageId = "a";
        sessions.Add(session);
        using var service = new WidgetStateService(variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()),
            NullLogger<WidgetStateService>.Instance);

        variables.Set("test.value", 7);
        await service.FlushAsync();
        await service.SendInitialAsync(session, profiles.Get("p")!.Pages[0], default);

        Assert.Empty(socket.Sent);
    }

    [Fact]
    public async Task A_dynamic_style_is_cleared_once_when_no_rule_matches_any_more_and_there_is_no_default()
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile
        {
            Id = "p",
            Name = "P",
            Pages = [new Page
            {
                Id = "a",
                Widgets =
                [
                    new Widget
                    {
                        Id = "w",
                        Dynamic = new Dictionary<string, DynamicBinding>
                        {
                            ["style.background"] = new DynamicBinding
                            {
                                Cases = [new DynamicCase(new ConditionNode { Variable = "test.on", Operator = "==", Value = "true" }, "#ff0000")],
                            },
                        },
                    },
                ],
            }],
        });

        var variables = new VariableStore();
        var sessions = new SessionRegistry();
        var socket = new TestSocket(hangs: false);
        sessions.Add(Identified(socket));
        using var service = new WidgetStateService(variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()),
            NullLogger<WidgetStateService>.Instance);

        variables.Set("test.on", true);
        await service.FlushAsync();
        var first = Assert.Single(socket.Sent);
        Assert.Equal("#ff0000", (string?)first.Data!["style"]!["background"]);

        variables.Set("test.on", false);
        await service.FlushAsync();
        Assert.Equal(2, socket.Sent.Count);
        var cleared = Assert.IsType<System.Text.Json.Nodes.JsonObject>(socket.Sent[1].Data!["style"]);
        Assert.Empty(cleared);

        variables.Set("test.on", false);
        variables.Set("test.on", null);
        await service.FlushAsync();
        Assert.Equal(2, socket.Sent.Count);
    }

    [Fact]
    public async Task Coming_back_to_a_page_clears_a_style_that_no_rule_matches_any_more()
    {
        var profiles = new ProfileStore(_dir);
        var page = new Page
        {
            Id = "a",
            Widgets =
            [
                new Widget
                {
                    Id = "w",
                    Dynamic = new Dictionary<string, DynamicBinding>
                    {
                        ["style.background"] = new DynamicBinding
                        {
                            Cases = [new DynamicCase(new ConditionNode { Variable = "test.on", Operator = "==", Value = "true" }, "#ff0000")],
                        },
                    },
                },
            ],
        };
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [page] });

        var variables = new VariableStore();
        var socket = new TestSocket(hangs: false);
        var session = Identified(socket);
        using var service = new WidgetStateService(variables, new SessionRegistry(), profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()),
            NullLogger<WidgetStateService>.Instance);

        variables.Set("test.on", true);
        await service.SendInitialAsync(session, page, default);
        Assert.Equal("#ff0000", (string?)Assert.Single(socket.Sent).Data!["style"]!["background"]);

        // The device is on another page while the variable changes, so no flush reaches this page; then it comes back.
        variables.Set("test.on", false);
        await service.SendInitialAsync(session, page, default);
        Assert.Empty(Assert.IsType<System.Text.Json.Nodes.JsonObject>(socket.Sent[1].Data!["style"]));

        await service.SendInitialAsync(session, page, default);
        Assert.Equal(2, socket.Sent.Count);
    }

    private (WidgetStateService Service, VariableStore Variables, TestSocket Socket, ProfileStore Profiles) FlushRig(params Widget[] widgets)
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [.. widgets] }] });
        var variables = new VariableStore();
        var sessions = new SessionRegistry();
        var socket = new TestSocket(hangs: false);
        sessions.Add(Identified(socket));
        var service = new WidgetStateService(variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), NullLogger<WidgetStateService>.Instance);
        return (service, variables, socket, profiles);
    }

    private static string[] SentIds(TestSocket socket) => [.. socket.Sent.Select(e => e.DataAs<WidgetStateMessage>()!.WidgetId)];

    [Fact]
    public async Task Flush_touches_only_widgets_that_read_the_changed_variables()
    {
        var styled = new Widget
        {
            Id = "styled",
            Dynamic = new Dictionary<string, DynamicBinding>
            {
                ["style.background"] = new DynamicBinding { Cases = [new DynamicCase(new ConditionNode { Variable = "t.cond", Operator = "==", Value = "1" }, "#ff0000")] },
            },
        };
        var ruleText = new Widget
        {
            Id = "ruleText",
            Dynamic = new Dictionary<string, DynamicBinding>
            {
                ["text"] = new DynamicBinding { Cases = [new DynamicCase(new ConditionNode { Variable = "t.cond2", Operator = "==", Value = "1" }, "r={t.result}")], Default = "d={t.default}" },
            },
        };
        var slider = new Widget { Id = "slider", Type = WidgetTypes.Slider, Props = new System.Text.Json.Nodes.JsonObject { ["valueVariable"] = "t.level" } };
        var (service, variables, socket, _) = FlushRig(new Widget { Id = "text", Text = "a={t.text}" }, styled, ruleText, slider, new Widget { Id = "other", Text = "x={t.other}" });
        using var _s = service;

        variables.Set("t.unrelated", 1);
        await service.FlushAsync();
        Assert.Empty(socket.Sent);

        variables.Set("t.text", 1);
        await service.FlushAsync();
        Assert.Equal(["text"], SentIds(socket));

        socket.Sent.Clear();
        variables.Set("t.cond", 1);
        variables.Set("t.level", 0.5);
        variables.Set("t.result", "q");
        variables.Set("t.default", "z");
        await service.FlushAsync();
        // Page order is kept; the rule-text widget reads its result and default templates too.
        Assert.Equal(["styled", "ruleText", "slider"], SentIds(socket));
    }

    [Fact]
    public async Task Refresh_forces_one_widget_and_a_replaced_page_is_indexed_again()
    {
        var (service, variables, socket, profiles) = FlushRig(new Widget { Id = "a1", Text = "a={t.one}" }, new Widget { Id = "a2", Text = "b={t.two}" });
        using var _s = service;

        service.Refresh("a2", null);
        await service.FlushAsync();
        Assert.Equal(["a2"], SentIds(socket));

        // An editor save replaces the profile: the next flush must follow the new widget list, not the old index.
        profiles.Save(new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "n1", Text = "n={t.two}" }] }] });
        socket.Sent.Clear();
        variables.Set("t.two", 5);
        await service.FlushAsync();
        Assert.Equal(["n1"], SentIds(socket));
    }

    [Fact]
    public async Task The_started_service_flushes_a_change_by_itself_and_a_change_during_a_flush_is_not_left_waiting()
    {
        var (service, variables, socket, _) = FlushRig(new Widget { Id = "w", Text = "v={t.v}" });
        using var _s = service;
        await service.StartAsync(default);

        variables.Set("t.v", 1);
        await WaitForAsync(() => socket.Sent.Count >= 1);

        variables.Set("t.v", 2);
        variables.Set("t.v", 3);
        await WaitForAsync(() => socket.Sent.Count >= 2 && socket.Sent[^1].DataAs<WidgetStateMessage>()!.Text == "v=3");

        // Quiet again: nothing more is sent.
        var count = socket.Sent.Count;
        await Task.Delay(400);
        Assert.Equal(count, socket.Sent.Count);
        await service.StopAsync(default);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "timed out");
            await Task.Delay(20);
        }
    }
}
