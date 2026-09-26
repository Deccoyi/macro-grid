using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Core.Sessions;

/// <summary>Runs the receive loop for every client WebSocket and routes protocol messages.</summary>
public sealed class ClientHub(
    SessionRegistry sessions,
    ProfileStore profiles,
    ActionDispatcher dispatcher,
    ToggleStateStore toggles,
    WidgetStateService widgetState,
    DeviceStore devices,
    PairingService pairing,
    PluginStatusRegistry statusRegistry,
    PreferencesStore preferences,
    AutoProfileSwitcher autoSwitcher,
    ILogger<ClientHub> logger,
    PluginLocalizer localizer)
{
    public const string ServerVersion = "0.3.1";
    private const int MaxMessageBytes = 64 * 1024;
    private const int MaxAssetsPerRequest = 256;

    public IReadOnlyCollection<ClientSession> Sessions => sessions.All;

    public event Action? SessionsChanged
    {
        add => sessions.Changed += value;
        remove => sessions.Changed -= value;
    }

    public async Task HandleAsync(WebSocket socket, string remoteAddress, CancellationToken cancellationToken)
    {
        var session = new ClientSession(socket) { RemoteAddress = remoteAddress };
        sessions.Add(session);
        logger.LogInformation("Client {Session} connected from {Remote}", session.Id, remoteAddress);

        // Actions of one client run sequentially and off the receive loop,
        // so a slow action never delays reading the next message.
        var queue = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
        var worker = Task.Run(async () =>
        {
            await foreach (var work in queue.Reader.ReadAllAsync())
            {
                try
                {
                    await work();
                }
                catch (Exception ex)
                {
                    // A bug in dispatch/reporting must not silently kill this session's action queue.
                    logger.LogError(ex, "Unhandled exception in action queue for session {Session}", session.Id);
                }
            }
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
                await session.SendAsync(MessageTypes.Error, new ErrorMessage("bad_message", "Invalid JSON envelope."), ct);
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
            await session.SendAsync(MessageTypes.Error, new ErrorMessage("hello_required", "A hello message has to be sent first."), ct);
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
            case MessageTypes.WidgetValue:
                EnqueueValue(session, envelope, queue);
                break;
            case MessageTypes.AssetGet:
                if (envelope.DataAs<AssetGetMessage>() is { } request)
                    await widgetState.SendAssetsAsync(session, request.Hashes.Take(MaxAssetsPerRequest), ct);
                break;
            case MessageTypes.PageChange:
                var page = envelope.DataAs<PageChangeMessage>();
                if (page is not null) session.PageId = page.PageId;
                break;
            case MessageTypes.PageNext:
                await new SessionDeviceController(session, profiles, widgetState).NextPageAsync();
                break;
            case MessageTypes.PagePrev:
                await new SessionDeviceController(session, profiles, widgetState).PreviousPageAsync();
                break;
            case MessageTypes.ProfileChange:
                var change = envelope.DataAs<ProfileChangeMessage>();
                if (change is not null)
                    await new SessionDeviceController(session, profiles, widgetState).SwitchProfileAsync(change.ProfileId);
                break;
            case MessageTypes.ProfileLock:
                var lockMsg = envelope.DataAs<ProfileLockMessage>();
                if (lockMsg is not null && session.DeviceId is not null)
                {
                    devices.SetAutoSwitchLocked(session.DeviceId, lockMsg.Locked);
                    session.AutoSwitch.SetLocked(lockMsg.Locked);
                    if (!lockMsg.Locked) await autoSwitcher.ReevaluateAsync(session);
                    if (devices.All.FirstOrDefault(d => d.Id == session.DeviceId) is { } device)
                        await SendProfilesListAsync(session, device, ct);
                }
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
            await session.SendAsync(MessageTypes.Error, new ErrorMessage("bad_hello", "deviceId is required."), ct);
            return;
        }

        var deviceName = string.IsNullOrWhiteSpace(hello.DeviceName) ? "Device" : hello.DeviceName;
        var device = devices.FindByToken(hello.Token);
        string? issuedToken = null;

        if (device is null)
        {
            var result = pairing.TryPair(hello.Pin, session.RemoteAddress);
            if (result.Outcome != PairingOutcome.Accepted)
            {
                LogPairingFailure(result, session.RemoteAddress);
                await session.SendAsync(MessageTypes.Error, new ErrorMessage("pairing_required", PairingMessage(result)), ct);
                return;
            }
            device = devices.Pair(hello.DeviceId, deviceName);
            issuedToken = device.Token;
            logger.LogInformation(SecurityEvents.DevicePaired, "Security: device {Name} ({Device}) paired from {Remote}", deviceName, device.Id, session.RemoteAddress);
        }
        else
        {
            devices.Touch(device.Id, deviceName);
        }

        session.DeviceId = device.Id;
        session.DeviceName = deviceName;
        session.Capabilities = [.. hello.Capabilities ?? []];

        // A device with no assigned profile (or one assigned to a profile that's since been deleted)
        // falls back to the app-wide default, then just the first profile — ProfileResolver.ResolveDefault.
        var profile = ProfileResolver.ResolveDefault(device, profiles, preferences);
        session.ProfileId = profile.Id;
        session.AutoSwitch.OnManual(profile.Id); // the resolved-default profile is this session's manual base.
        session.AutoSwitch.SetLocked(device.AutoSwitchLocked);
        var page = profile.Pages.FirstOrDefault();
        session.PageId = page?.Id;
        sessions.NotifyChanged();

        await session.SendAsync(MessageTypes.Welcome, new WelcomeMessage(Environment.MachineName, ServerVersion, issuedToken), ct);
        await widgetState.SendLayoutAsync(session, profile, session.PageId ?? "", ct);
        await SendProfilesListAsync(session, device, ct);
        if (page is not null)
            await widgetState.SendInitialAsync(session, page, ct);
    }

    /// <summary>Every profile, not just the current one, so the client can offer a profile-switcher
    /// drawer — plus this device's auto-switch opt-in/lock state, re-sent whenever either changes.</summary>
    private void LogPairingFailure(PairingResult result, string remote)
    {
        switch (result.Outcome)
        {
            case PairingOutcome.WrongPin:
                logger.LogWarning(SecurityEvents.WrongPin, "Security: wrong pairing PIN from {Remote} ({Failures} in a row)", remote, result.Failures);
                break;
            case PairingOutcome.Closed:
                logger.LogWarning(SecurityEvents.PinWhileClosed, "Security: pairing PIN from {Remote} while pairing is closed ({Failures} in a row)", remote, result.Failures);
                break;
            case PairingOutcome.Blocked when result.NewlyBlocked:
                logger.LogWarning(SecurityEvents.PairingBlocked, "Security: {Remote} blocked from pairing for {Seconds} s after {Failures} wrong PINs", remote, (int)result.RetryAfter.TotalSeconds, result.Failures);
                break;
        }
        if (result.PinRenewed)
            logger.LogWarning(SecurityEvents.PinRenewed, "Security: pairing PIN replaced after {Count} wrong attempts", PairingService.FailuresBeforeNewPin);
    }

    private static string PairingMessage(PairingResult result) => result.Outcome switch
    {
        PairingOutcome.Blocked => $"Too many wrong PINs. Try again in {Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds))} seconds.",
        PairingOutcome.Closed => "Pairing is closed. Open the Pairing window in the Macro Grid editor on the computer, then enter the PIN shown there.",
        PairingOutcome.WrongPin => "Wrong PIN. Enter the PIN shown in the Pairing window of the Macro Grid editor on the computer.",
        _ => "Pairing is required. Open the Pairing window in the Macro Grid editor on the computer and enter the PIN shown there.",
    };

    private Task SendProfilesListAsync(ClientSession session, PairedDevice device, CancellationToken ct) =>
        session.SendAsync(MessageTypes.ProfilesList, new ProfilesListPayload(
            profiles.All.Select(p => new ProfileSummary(p.Id, p.Name)).ToList(),
            new AutoSwitchInfo(device.FollowActiveWindow, device.AutoSwitchLocked)), ct);

    private Widget? FindWidget(ClientSession session, string pageId, string widgetId, out Page? page)
    {
        page = session.ProfileId is null ? null : profiles.Get(session.ProfileId)?.FindPage(pageId);
        var widget = page?.FindWidget(widgetId);
        if (widget is null || page is null)
            logger.LogDebug("Widget {Widget} not found on page {Page}", widgetId, pageId);
        return widget;
    }

    private void Enqueue(ClientSession session, Envelope envelope, string eventName, ChannelWriter<Func<Task>> queue)
    {
        var msg = envelope.DataAs<WidgetEventMessage>();
        if (msg is null || session.ProfileId is null) return;

        var widget = FindWidget(session, msg.PageId, msg.WidgetId, out _);
        if (widget is null) return;

        var device = new SessionDeviceController(session, profiles, widgetState);
        var context = new ActionContext(session.DeviceId!, msg.PageId, msg.WidgetId, device);

        // A toggle flips on every press instead of firing "press": the two states get their own action bindings.
        if (widget.Type == WidgetTypes.Toggle && eventName == WidgetEvents.Press)
        {
            queue.TryWrite(async () =>
            {
                var active = toggles.Toggle(widget.Id);
                await BroadcastToggleAsync(session.ProfileId, msg.PageId, widget.Id, active);
                var errors = await dispatcher.DispatchAsync(widget, active ? WidgetEvents.ToggleOn : WidgetEvents.ToggleOff, context, CancellationToken.None);
                await ReportActionErrorsAsync(session, errors);
            });
            return;
        }

        queue.TryWrite(async () =>
        {
            var errors = await dispatcher.DispatchAsync(widget, eventName, context, CancellationToken.None);
            await ReportActionErrorsAsync(session, errors);
        });
    }

    /// <summary>How long a failed action stays in the status bar if nothing else clears it.</summary>
    private static readonly TimeSpan ActionErrorStatusLifetime = TimeSpan.FromSeconds(15);

    /// <summary>Surfaces a failed action both to the device that triggered it (toast, via the same "error"
    /// envelope pairing failures already use) and in the editor's status bar — a stale binding (e.g. a
    /// button pointed at a since-deleted scene of a plugin) must never fail silently.</summary>
    private async Task ReportActionErrorsAsync(ClientSession session, IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            statusRegistry.RemoveCore("actionError"); // the next action that works clears the old failure
            return;
        }

        var message = localizer.TranslateAny(errors[0]) ?? errors[0];
        statusRegistry.SetCore("actionError", message, StatusLevel.Warning, "triangle-alert", lifetime: ActionErrorStatusLifetime);
        await session.SendAsync(MessageTypes.Error, new ErrorMessage("action_failed", message));
    }

    /// <summary>A slider/knob drag commit — same dispatch as <see cref="Enqueue"/>, just carrying the live
    /// value instead of being a bare press/release, and with no toggle special-case (sliders aren't toggles).</summary>
    private void EnqueueValue(ClientSession session, Envelope envelope, ChannelWriter<Func<Task>> queue)
    {
        var msg = envelope.DataAs<WidgetValueMessage>();
        if (msg is null || session.ProfileId is null) return;

        var widget = FindWidget(session, msg.PageId, msg.WidgetId, out _);
        if (widget is null) return;

        var device = new SessionDeviceController(session, profiles, widgetState);
        var context = new ActionContext(session.DeviceId!, msg.PageId, msg.WidgetId, device, Value: msg.Value);
        queue.TryWrite(async () =>
        {
            var errors = await dispatcher.DispatchAsync(widget, WidgetEvents.ValueChange, context, CancellationToken.None);
            await ReportActionErrorsAsync(session, errors);
        });
    }

    private async Task BroadcastToggleAsync(string profileId, string pageId, string widgetId, bool active)
    {
        foreach (var s in sessions.All.Where(s => s.ProfileId == profileId && s.PageId == pageId))
            await s.SendAsync(MessageTypes.WidgetState, new WidgetStateMessage(widgetId, Active: active));
    }
}
