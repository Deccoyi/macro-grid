using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class WebActionTests : IDisposable
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
            Sent.Add(Envelope.Parse(Encoding.UTF8.GetString(buffer))!);
            return Task.CompletedTask;
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            Sent.Add(Envelope.Parse(Encoding.UTF8.GetString(buffer.Span))!);
            return ValueTask.CompletedTask;
        }

        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "macrogrid-webaction-" + Guid.NewGuid().ToString("N"));
    private readonly WebViewState _state = new();
    private readonly RecordingSocket _phone = new();
    private readonly RecordingSocket _otherPhone = new();
    private readonly WebAction _action;

    public WebActionTests()
    {
        var profiles = new ProfileStore(_dir);
        profiles.Save(new Profile
        {
            Id = "p",
            Name = "P",
            Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "chat", Type = WidgetTypes.Web, Name = "Chat" }, new Widget { Id = "b", X = 1 }] }],
        });
        var sessions = new SessionRegistry();
        sessions.Add(new ClientSession(_phone) { DeviceId = "d1", ProfileId = "p", PageId = "a" });
        sessions.Add(new ClientSession(_otherPhone) { DeviceId = "d2", ProfileId = "p", PageId = "a" });
        _action = new WebAction(sessions, profiles, _state, NullLogger<WebAction>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private static ActionContext From(string deviceId) => new(deviceId, "a", "b", new FakeDeviceController());

    private static WidgetStateMessage LastState(RecordingSocket socket) =>
        socket.Sent.Last(e => e.Type == MessageTypes.WidgetState).DataAs<WidgetStateMessage>()!;

    [Fact]
    public async Task Set_changes_only_the_device_that_pressed()
    {
        await _action.ExecuteAsync(From("d1"), WebAction.Set("chat", "https://example.org/chat"), default);

        Assert.Equal("https://example.org/chat", LastState(_phone).Url);
        Assert.Empty(_otherPhone.Sent);
        Assert.Equal("https://example.org/chat", _state.GetUrl("d1", "chat"));
        Assert.Null(_state.GetUrl("d2", "chat"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:pw@example.org/")]
    [InlineData("http://localhost:9820/")]
    [InlineData("")]
    public async Task A_refused_address_throws_and_changes_nothing(string url)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _action.ExecuteAsync(From("d1"), WebAction.Set("chat", url), default));

        Assert.Empty(_phone.Sent);
        Assert.Null(_state.GetUrl("d1", "chat"));
    }

    [Fact]
    public async Task Reset_sends_an_empty_address_and_forgets_it()
    {
        await _action.ExecuteAsync(From("d1"), WebAction.Set("chat", "https://example.org/a"), default);
        await _action.ExecuteAsync(From("d1"), WebAction.Reset("chat"), default);

        Assert.Equal("", LastState(_phone).Url);
        Assert.Null(_state.GetUrl("d1", "chat"));
    }

    [Fact]
    public async Task Reload_counts_up_each_time()
    {
        await _action.ExecuteAsync(From("d1"), WebAction.Reload("chat"), default);
        await _action.ExecuteAsync(From("d1"), WebAction.Reload("chat"), default);

        Assert.Equal(2, LastState(_phone).Reload);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("b")]
    [InlineData("")]
    public async Task A_missing_or_non_web_widget_is_an_error(string widgetId)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _action.ExecuteAsync(From("d1"), new JsonObject { ["widgetId"] = widgetId, ["mode"] = "reload" }, default));
    }

    [Fact]
    public void Clearing_a_device_returns_the_widgets_that_had_an_address()
    {
        _state.SetUrl("d1", "chat", "https://example.org/");

        Assert.Equal(["chat"], _state.Clear("d1"));
        Assert.Empty(_state.Clear("d1"));
    }
}
