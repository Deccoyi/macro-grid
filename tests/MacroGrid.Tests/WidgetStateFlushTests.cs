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
}
