using System.Net.WebSockets;
using System.Text;
using MacroGrid.Core.Model;
using MacroGrid.Core.Sessions;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

public class LayoutSenderTests
{
    /// <summary>Collects every text frame the session sends.</summary>
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

    private static readonly string Icon = "data:image/svg+xml;base64," + new string('Q', 400);

    private static Profile ProfileWithIcon(string text = "hi") => new()
    {
        Id = "p",
        Name = "P",
        Pages =
        [
            new Page
            {
                Id = "a",
                Widgets =
                [
                    new Widget { Id = "w1", Text = text, Style = new WidgetStyle { Icon = Icon } },
                    new Widget { Id = "w2", Text = "two", Style = new WidgetStyle { Icon = Icon } },
                ],
            },
        ],
    };

    private static (ClientSession Session, RecordingSocket Socket) NewSession(params string[] capabilities)
    {
        var socket = new RecordingSocket();
        return (new ClientSession(socket) { Capabilities = [.. capabilities] }, socket);
    }

    [Fact]
    public async Task A_client_without_capabilities_gets_the_plain_layout_with_icons_inline()
    {
        var (session, socket) = NewSession();

        await new LayoutSender(new AssetStore()).SendFullAsync(session, ProfileWithIcon(), "a");

        var message = Assert.Single(socket.Sent);
        Assert.Equal(MessageTypes.LayoutFull, message.Type);
        Assert.Equal(Icon, message.Data!["profile"]!["pages"]![0]!["widgets"]![0]!["style"]!["icon"]!.GetValue<string>());
        Assert.Null(session.SentLayout);
    }

    [Fact]
    public async Task An_assets_client_gets_references_and_can_fetch_the_data_once()
    {
        var (session, socket) = NewSession(ClientCapabilities.Assets);
        var sender = new LayoutSender(new AssetStore());

        await sender.SendFullAsync(session, ProfileWithIcon(), "a");

        var widgets = socket.Sent[0].Data!["profile"]!["pages"]![0]!["widgets"]!.AsArray();
        var reference = widgets[0]!["style"]!["icon"]!.GetValue<string>();
        Assert.StartsWith(AssetStore.RefPrefix, reference);
        Assert.Equal(reference, widgets[1]!["style"]!["icon"]!.GetValue<string>());

        var hash = reference[AssetStore.RefPrefix.Length..];
        await sender.SendAssetsAsync(session, [hash, hash, "ffffffffffffffffffffffff"]);

        var assets = socket.Sent.Skip(1).ToList();
        Assert.Equal(2, assets.Count);
        Assert.Equal(Icon, assets[0].DataAs<AssetMessage>()!.Data);
        Assert.Null(assets[1].DataAs<AssetMessage>()!.Data);
    }

    [Fact]
    public async Task A_patch_client_gets_only_the_edited_widget_after_the_first_full_layout()
    {
        var (session, socket) = NewSession(ClientCapabilities.Assets, ClientCapabilities.LayoutPatch);
        var sender = new LayoutSender(new AssetStore());
        await sender.SendFullAsync(session, ProfileWithIcon(), "a");

        var result = await sender.SendUpdateAsync(session, ProfileWithIcon(text: "edited"), "a");

        Assert.Equal(LayoutSendKind.Patch, result.Kind);
        Assert.Equal(["w1"], result.ChangedWidgetIds);
        var patch = socket.Sent[^1];
        Assert.Equal(MessageTypes.LayoutPatch, patch.Type);
        var upsert = Assert.Single(patch.Data!["pages"]![0]!["widgets"]!.AsArray());
        Assert.Equal("edited", upsert!["text"]!.GetValue<string>());
        Assert.StartsWith(AssetStore.RefPrefix, upsert["style"]!["icon"]!.GetValue<string>());
    }

    [Fact]
    public async Task Saving_an_unchanged_profile_sends_nothing()
    {
        var (session, socket) = NewSession(ClientCapabilities.Assets, ClientCapabilities.LayoutPatch);
        var sender = new LayoutSender(new AssetStore());
        await sender.SendFullAsync(session, ProfileWithIcon(), "a");
        var before = socket.Sent.Count;

        var result = await sender.SendUpdateAsync(session, ProfileWithIcon(), "a");

        Assert.Equal(LayoutSendKind.Unchanged, result.Kind);
        Assert.Equal(before, socket.Sent.Count);
    }

    [Fact]
    public async Task A_client_without_patch_support_gets_a_full_layout_on_every_update()
    {
        var (session, socket) = NewSession(ClientCapabilities.Assets);
        var sender = new LayoutSender(new AssetStore());
        await sender.SendFullAsync(session, ProfileWithIcon(), "a");

        var result = await sender.SendUpdateAsync(session, ProfileWithIcon(text: "edited"), "a");

        Assert.Equal(LayoutSendKind.Full, result.Kind);
        Assert.Equal(MessageTypes.LayoutFull, socket.Sent[^1].Type);
    }

    [Fact]
    public async Task A_patch_client_with_no_baseline_yet_falls_back_to_a_full_layout()
    {
        var (session, socket) = NewSession(ClientCapabilities.LayoutPatch);

        var result = await new LayoutSender(new AssetStore()).SendUpdateAsync(session, ProfileWithIcon(), "a");

        Assert.Equal(LayoutSendKind.Full, result.Kind);
        Assert.Equal(MessageTypes.LayoutFull, Assert.Single(socket.Sent).Type);
    }
}
