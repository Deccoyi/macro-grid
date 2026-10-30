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
            new WebViewState(), NullLogger<WidgetStateService>.Instance) { SendTimeout = TimeSpan.FromMilliseconds(200) };

        variables.Set("test.value", 7);
        await service.FlushAsync();

        Assert.True(slow.Aborted);
        Assert.False(healthy.Aborted);
        var message = Assert.Single(healthy.Sent);
        Assert.Equal(MessageTypes.WidgetState, message.Type);
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
            new WebViewState(), NullLogger<WidgetStateService>.Instance);

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
            new WebViewState(), NullLogger<WidgetStateService>.Instance);

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
}
