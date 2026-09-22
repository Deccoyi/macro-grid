using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using MacroStation.Protocol;

namespace MacroStation.Core.Sessions;

/// <summary>One connected client (phone, tablet or browser).</summary>
public sealed class ClientSession(WebSocket socket, string remoteAddress)
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string RemoteAddress { get; } = remoteAddress;
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.Now;

    public bool IsIdentified => DeviceId is not null;
    public string? DeviceId { get; internal set; }
    public string? DeviceName { get; internal set; }
    public string? ProfileId { get; internal set; }
    public string? PageId { get; internal set; }

    /// <summary>Pages this client navigated away from, for <c>core.page</c> "back". Only touched by this session's own single-threaded action queue.</summary>
    internal Stack<string> PageHistory { get; } = new();

    /// <summary>Last text sent per widget id, so <see cref="WidgetStateService"/> only re-sends on an actual change.</summary>
    internal ConcurrentDictionary<string, string> SentTexts { get; } = new();

    /// <summary>Last resolved dynamic style per widget id, so <see cref="WidgetStateService"/> only re-sends on an actual change.</summary>
    internal ConcurrentDictionary<string, Dictionary<string, string>> SentStyles { get; } = new();

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
