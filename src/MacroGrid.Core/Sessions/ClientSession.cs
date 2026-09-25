using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using MacroGrid.Protocol;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Core.Sessions;

/// <summary>One connected client (phone, tablet or browser).</summary>
public sealed class ClientSession(WebSocket socket)
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    public bool IsIdentified => DeviceId is not null;
    public string? DeviceId { get; internal set; }
    public string? DeviceName { get; internal set; }
    public string? ProfileId { get; internal set; }
    public string? PageId { get; internal set; }

    /// <summary>Pages this client navigated away from, for <c>core.page</c> "back". Only touched by this session's own single-threaded action queue.</summary>
    internal Stack<string> PageHistory { get; } = new();

    /// <summary>This session's auto-profile-switch stack (docs/design/auto-profile-switch.md) — always present,
    /// but only ever driven by <c>AutoProfileSwitcher</c> for a device with <c>FollowActiveWindow</c> on.</summary>
    internal AutoSwitchState AutoSwitch { get; } = new();

    /// <summary>Last text sent per widget id, so <see cref="WidgetStateService"/> only re-sends on an actual change.</summary>
    internal ConcurrentDictionary<string, string> SentTexts { get; } = new();

    /// <summary>Last resolved dynamic style per widget id, so <see cref="WidgetStateService"/> only re-sends on an actual change.</summary>
    internal ConcurrentDictionary<string, Dictionary<string, string>> SentStyles { get; } = new();

    /// <summary>Last pushed slider/knob live value per widget id (from its bound variable), so <see cref="WidgetStateService"/> only re-sends on an actual change.</summary>
    internal ConcurrentDictionary<string, double> SentValues { get; } = new();

    /// <summary>Optional protocol features this client announced in its <c>hello</c> (see <see cref="ClientCapabilities"/>).</summary>
    internal HashSet<string> Capabilities { get; set; } = [];

    internal bool Supports(string capability) => Capabilities.Contains(capability);

    /// <summary>The layout exactly as the client last received it (asset references and all), the baseline the next
    /// <c>layout.patch</c> is computed against. Null until a full layout was sent, or for a client without patch support.</summary>
    internal JsonObject? SentLayout { get; set; }

    /// <summary>Layout sends for one client must not interleave, or a patch could be computed against a baseline
    /// the client never received.</summary>
    internal SemaphoreSlim LayoutLock { get; } = new(1, 1);

    internal WebSocket Socket => socket;

    public async Task SendAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        if (socket.State != WebSocketState.Open) return;
        var bytes = Encoding.UTF8.GetBytes(envelope.ToJson());

        // WebSocket allows only one concurrent send; broadcasts and replies may race.
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public Task SendAsync<T>(string type, T data, CancellationToken cancellationToken = default) =>
        SendAsync(Envelope.Create(type, data), cancellationToken);
}
