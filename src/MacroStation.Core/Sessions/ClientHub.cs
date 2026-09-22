using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MacroStation.Core.Actions;
using MacroStation.Core.Devices;
using MacroStation.Core.Model;
using MacroStation.Core.Profiles;
using MacroStation.Plugin.Abstractions;
using MacroStation.Protocol;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Sessions;

public sealed record LayoutFullPayload(Profile Profile, string PageId);

/// <summary>Runs the receive loop for every client WebSocket and routes protocol messages.</summary>
public sealed class ClientHub(
    SessionRegistry sessions,
    ProfileStore profiles,
    ActionDispatcher dispatcher,
    ToggleStateStore toggles,
    WidgetStateService widgetState,
    DeviceStore devices,
    PairingService pairing,
    ILogger<ClientHub> logger)
{
    public const string ServerVersion = "0.1.0";
    private const int MaxMessageBytes = 64 * 1024;

    public IReadOnlyCollection<ClientSession> Sessions => sessions.All;

    public event Action? SessionsChanged
    {
        add => sessions.Changed += value;
        remove => sessions.Changed -= value;
    }

    public async Task HandleAsync(WebSocket socket, string remoteAddress, CancellationToken cancellationToken)
    {
        var session = new ClientSession(socket, remoteAddress);
        sessions.Add(session);
        logger.LogInformation("Client {Session} connected from {Remote}", session.Id, remoteAddress);

        // Actions of one client run sequentially and off the receive loop,
        // so a slow action never delays reading the next message.
        var queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
        var worker = Task.Run(async () =>
        {
            await foreach (var work in queue.Reader.ReadAllAsync())
                await work();
        });

        try
        {
            await ReceiveLoopAsync(session, queue.Writer, cancellationToken);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // Client vanished (Wi-Fi drop, app killed) or server is shutting down.
        }
        finally
        {
            queue.Writer.TryComplete();
            await worker;
            sessions.Remove(session);
            logger.LogInformation("Client {Session} ({Device}) disconnected", session.Id, session.DeviceName ?? "?");
        }
    }

    private async Task ReceiveLoopAsync(ClientSession session, ChannelWriter<Func<Task>> queue, CancellationToken ct)
    {
        var socket = session.Socket;
        var buffer = new byte[8 * 1024];
        using var message = new MemoryStream();

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct);
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxMessageBytes)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "message too big", ct);
                return;
            }
            if (!result.EndOfMessage) continue;

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);

            Envelope? envelope;
            try
            {
                envelope = Envelope.Parse(json);
            }
            catch (JsonException)
            {
                envelope = null;
            }

            if (envelope is null || string.IsNullOrEmpty(envelope.Type))
            {
                await session.SendAsync(MessageTypes.Error, new ErrorMessage("bad_message", "Geçersiz JSON zarfı."), ct);
                continue;
            }

            await HandleMessageAsync(session, envelope, queue, ct);
        }
    }

    private async Task HandleMessageAsync(ClientSession session, Envelope envelope, ChannelWriter<Func<Task>> queue, CancellationToken ct)
    {
        if (envelope.Type == MessageTypes.Hello)
        {
            await OnHelloAsync(session, envelope.DataAs<HelloMessage>(), ct);
            return;
        }

        if (!session.IsIdentified)
        {
            await session.SendAsync(MessageTypes.Error, new ErrorMessage("hello_required", "Önce hello gönderilmeli."), ct);
            return;
        }

        switch (envelope.Type)
        {
            case MessageTypes.WidgetDown:
                Enqueue(session, envelope, WidgetEvents.Press, queue);
                break;
            case MessageTypes.WidgetUp:
                Enqueue(session, envelope, WidgetEvents.Release, queue);
                break;
            case MessageTypes.WidgetLongPress:
                Enqueue(session, envelope, WidgetEvents.LongPress, queue);
                break;
            case MessageTypes.WidgetDoubleTap:
                Enqueue(session, envelope, WidgetEvents.DoubleTap, queue);
                break;
            case MessageTypes.PageChange:
                var page = envelope.DataAs<PageChangeMessage>();
                if (page is not null) session.PageId = page.PageId;
                break;
            case MessageTypes.ProfileChange:
                var change = envelope.DataAs<ProfileChangeMessage>();
                if (change is not null)
                    await new SessionDeviceController(session, profiles, widgetState).SwitchProfileAsync(change.ProfileId);
                break;
            default:
                logger.LogDebug("Ignoring message type {Type}", envelope.Type);
                break;
        }
    }

    private async Task OnHelloAsync(ClientSession session, HelloMessage? hello, CancellationToken ct)
    {
        if (hello is null || string.IsNullOrWhiteSpace(hello.DeviceId))
        {
            await session.SendAsync(MessageTypes.Error, new ErrorMessage("bad_hello", "deviceId gerekli."), ct);
            return;
        }

        var deviceName = string.IsNullOrWhiteSpace(hello.DeviceName) ? "Cihaz" : hello.DeviceName;
        var device = devices.FindByToken(hello.Token);
        string? issuedToken = null;

        if (device is null)
        {
            if (!pairing.Verify(hello.Pin))
            {
                await session.SendAsync(MessageTypes.Error, new ErrorMessage("pairing_required", "Eşleştirme gerekli. Bilgisayardaki Macro Station düzenleyicisinde gösterilen PIN'i girin."), ct);
                return;
            }
            device = devices.Pair(hello.DeviceId, deviceName);
            issuedToken = device.Token;
        }
        else
        {
            devices.Touch(device.Id, deviceName);
        }

        session.DeviceId = device.Id;
        session.DeviceName = deviceName;

        var profile = profiles.First();
        session.ProfileId = profile.Id;
        var page = profile.Pages.FirstOrDefault();
        session.PageId = page?.Id;
        sessions.NotifyChanged();

        await session.SendAsync(MessageTypes.Welcome, new WelcomeMessage(Environment.MachineName, ServerVersion, issuedToken), ct);
        await session.SendAsync(MessageTypes.LayoutFull, new LayoutFullPayload(profile, session.PageId ?? ""), ct);
        // Every profile, not just the assigned one, so the client can offer a profile-switcher drawer.
        await session.SendAsync(MessageTypes.ProfilesList, new ProfilesListPayload(profiles.All.Select(p => new ProfileSummary(p.Id, p.Name)).ToList()), ct);
        if (page is not null)
            await widgetState.SendInitialAsync(session, page, ct);
    }

    private void Enqueue(ClientSession session, Envelope envelope, string eventName, ChannelWriter<Func<Task>> queue)
    {
        var msg = envelope.DataAs<WidgetEventMessage>();
        if (msg is null || session.ProfileId is null) return;

        var page = profiles.Get(session.ProfileId)?.FindPage(msg.PageId);
        var widget = page?.FindWidget(msg.WidgetId);
        if (widget is null || page is null)
        {
            logger.LogDebug("Widget {Widget} not found on page {Page}", msg.WidgetId, msg.PageId);
            return;
        }

        var device = new SessionDeviceController(session, profiles, widgetState);
        var context = new ActionContext(session.DeviceId!, msg.PageId, msg.WidgetId, device);

        // A toggle flips on every press instead of firing "press": the two states get their own action bindings.
        if (widget.Type == WidgetTypes.Toggle && eventName == WidgetEvents.Press)
        {
            queue.TryWrite(async () =>
            {
                var active = toggles.Toggle(widget.Id);
                await BroadcastToggleAsync(session.ProfileId, msg.PageId, widget.Id, active);
                await dispatcher.DispatchAsync(widget, active ? WidgetEvents.ToggleOn : WidgetEvents.ToggleOff, context, CancellationToken.None);
            });
            return;
        }

        queue.TryWrite(() => dispatcher.DispatchAsync(widget, eventName, context, CancellationToken.None));
    }

    private async Task BroadcastToggleAsync(string profileId, string pageId, string widgetId, bool active)
    {
        foreach (var s in sessions.All.Where(s => s.ProfileId == profileId && s.PageId == pageId))
            await s.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widgetId, Active: active));
    }
}
