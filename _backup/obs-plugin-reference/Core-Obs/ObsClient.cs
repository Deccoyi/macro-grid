using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace MacroStation.Core.Obs;

/// <summary>Thrown when OBS accepts the connection but rejects a request (wrong scene name, no such input, ...).</summary>
public sealed class ObsRequestException(string requestType, int statusCode, string? comment)
    : Exception($"obs-websocket request '{requestType}' failed ({statusCode}){(comment is null ? "" : $": {comment}")}")
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Thrown when the initial Hello/Identify handshake itself fails (bad password, protocol mismatch).</summary>
public sealed class ObsAuthException(string message) : Exception(message);

/// <summary>
/// A single obs-websocket v5 connection: raw <see cref="ClientWebSocket"/> plumbing, the Hello/Identify
/// handshake (see <see cref="ObsAuth"/>), request/response correlation by requestId, and an event feed.
/// One instance = one live connection; <see cref="ObsConnection"/> owns reconnecting a new instance when
/// this one dies. Not thread-safe beyond what ClientWebSocket itself guarantees (one concurrent send, one
/// concurrent receive) — <see cref="RequestAsync"/> serializes sends behind <see cref="_sendLock"/>.
/// </summary>
public sealed class ObsClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private Task? _receiveLoop;

    /// <summary>Fires on the thread pool for every `Event` message once identified. eventType, eventData.</summary>
    public event Action<string, JsonObject>? EventReceived;

    /// <summary>Fires once the receive loop exits (server closed, network error, ...) so the owner can reconnect.</summary>
    public event Action<Exception?>? Disconnected;

    public static async Task<ObsClient> ConnectAsync(string host, int port, string password, CancellationToken cancellationToken)
    {
        var client = new ObsClient();
        try
        {
            await client._socket.ConnectAsync(new Uri($"ws://{host}:{port}"), cancellationToken);
            await client.HandshakeAsync(password, cancellationToken);
            client._receiveLoop = client.ReceiveLoopAsync();
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    private async Task HandshakeAsync(string password, CancellationToken cancellationToken)
    {
        var hello = await ReceiveOneAsync(cancellationToken) ?? throw new ObsAuthException("Sunucu bağlantıyı Hello mesajı göndermeden kapattı.");
        if (hello["op"]?.GetValue<int>() != 0)
            throw new ObsAuthException("Beklenmeyen ilk mesaj (Hello değil).");

        var data = hello["d"] as JsonObject ?? [];
        var identify = new JsonObject { ["rpcVersion"] = 1, ["eventSubscriptions"] = (int)(ObsEventSubscription.General | ObsEventSubscription.Outputs | ObsEventSubscription.Scenes | ObsEventSubscription.Inputs) };

        if (data["authentication"] is JsonObject auth)
        {
            var salt = auth["salt"]?.GetValue<string>() ?? "";
            var challenge = auth["challenge"]?.GetValue<string>() ?? "";
            identify["authentication"] = ObsAuth.ComputeAuthenticationString(password, salt, challenge);
        }

        await SendAsync(1, identify, cancellationToken);

        var identified = await ReceiveOneAsync(cancellationToken) ?? throw new ObsAuthException("Identify sonrası bağlantı kapandı (muhtemelen yanlış şifre).");
        if (identified["op"]?.GetValue<int>() != 2)
            throw new ObsAuthException("Kimlik doğrulama başarısız (yanlış şifre olabilir).");
    }

    /// <summary>Sends a Request and awaits its matching RequestResponse. Throws <see cref="ObsRequestException"/> if OBS reports failure.</summary>
    public async Task<JsonObject> RequestAsync(string requestType, JsonObject? requestData, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = tcs;

        try
        {
            var payload = new JsonObject { ["requestType"] = requestType, ["requestId"] = requestId };
            if (requestData is not null) payload["requestData"] = requestData;
            await SendAsync(6, payload, cancellationToken);

            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            var response = await tcs.Task;

            var status = response["requestStatus"] as JsonObject;
            if (status?["result"]?.GetValue<bool>() != true)
            {
                var code = status?["code"]?.GetValue<int>() ?? -1;
                var comment = status?["comment"]?.GetValue<string>();
                throw new ObsRequestException(requestType, code, comment);
            }

            return response["responseData"] as JsonObject ?? [];
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private async Task SendAsync(int op, JsonObject data, CancellationToken cancellationToken)
    {
        var frame = new JsonObject { ["op"] = op, ["d"] = data };
        var bytes = Encoding.UTF8.GetBytes(frame.ToJsonString());
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>Reads exactly one JSON text frame. Used only during the handshake, before the receive loop starts.</summary>
    private async Task<JsonObject?> ReceiveOneAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return JsonNode.Parse(stream) as JsonObject;
    }

    private async Task ReceiveLoopAsync()
    {
        Exception? failure = null;
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var message = await ReceiveOneAsync(CancellationToken.None);
                if (message is null) break;
                Dispatch(message);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            // Any request still awaiting a response when the socket dies would otherwise hang forever.
            foreach (var (_, tcs) in _pending)
                tcs.TrySetException(failure ?? new IOException("OBS bağlantısı koptu."));
            Disconnected?.Invoke(failure);
        }
    }

    private void Dispatch(JsonObject message)
    {
        var op = message["op"]?.GetValue<int>();
        var data = message["d"] as JsonObject ?? [];
        switch (op)
        {
            case 7 when data["requestId"]?.GetValue<string>() is { } requestId && _pending.TryGetValue(requestId, out var tcs):
                tcs.TrySetResult(data);
                break;
            case 5 when data["eventType"]?.GetValue<string>() is { } eventType:
                EventReceived?.Invoke(eventType, data["eventData"] as JsonObject ?? []);
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch { /* best-effort close */ }

        if (_receiveLoop is not null)
        {
            try { await _receiveLoop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* loop already exiting */ }
        }

        _socket.Dispose();
    }
}

/// <summary>Subset of obs-websocket's EventSubscription bitmask — only what <see cref="ObsConnection"/> actually listens to.</summary>
[Flags]
public enum ObsEventSubscription
{
    General = 1 << 0,
    Scenes = 1 << 2,
    Inputs = 1 << 4,
    Outputs = 1 << 6,
}
