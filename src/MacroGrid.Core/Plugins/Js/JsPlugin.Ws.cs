using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin
{
    // ---- web sockets (permission ws:<host>:<port>) ----
    // The connection and the read loop run on the thread pool; every event is a job on the plugin's own thread. The read loop waits for that job
    // before it reads the next message, so a fast sender is slowed down by the network and nothing is dropped. A job that comes from a socket
    // never has a press window: a message from outside cannot press keys.

    private readonly ConcurrentDictionary<int, WsConnection> _sockets = [];
    private readonly Queue<long> _wsConnects = new(); // timestamps; plugin thread only
    private HttpMessageInvoker? _wsInvoker;

    private sealed class WsConnection(ClientWebSocket socket, CancellationTokenSource cts, int outgoingWaiting)
    {
        public ClientWebSocket Socket { get; } = socket;
        public CancellationTokenSource Cts { get; } = cts;
        public Channel<string> Outgoing { get; } = Channel.CreateBounded<string>(new BoundedChannelOptions(outgoingWaiting) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        public volatile bool ClosedByScript;
        // Plugin thread only: the start of the current second and how many messages were sent in it.
        public long SendSecond;
        public int SendsInSecond;
    }

    private void WsConnect(int id, string url, string protocolsJson, string headersJson)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeWs && uri.Scheme != Uri.UriSchemeWss))
            throw new JsHostException("Only ws:// and wss:// addresses can be connected.");
        if (!_permissions.AllowsWs(uri))
            throw new JsHostException($"This plugin has not been granted a web socket to {uri.Host}:{uri.Port}.");
        if (uri.Scheme == Uri.UriSchemeWs && JsNetworkGuard.ScopeOf(uri.Host) == NetworkScope.Internet)
            throw new JsHostException("A web socket to the internet must use wss://.");

        if (_sockets.Count >= _limits.MaxSockets)
            throw WsLimit($"A plugin can have at most {_limits.MaxSockets} web sockets open.");
        var now = Stopwatch.GetTimestamp();
        while (_wsConnects.Count > 0 && Stopwatch.GetElapsedTime(_wsConnects.Peek(), now) > TimeSpan.FromMinutes(1)) _wsConnects.Dequeue();
        if (_wsConnects.Count >= _limits.MaxWsConnectsPerMinute)
            throw WsLimit($"A plugin can open at most {_limits.MaxWsConnectsPerMinute} web sockets a minute.");

        var socket = new ClientWebSocket();
        try
        {
            socket.Options.KeepAliveInterval = _limits.WsPing;
            socket.Options.KeepAliveTimeout = _limits.WsPing;
            foreach (var protocol in JsonSerializer.Deserialize<string[]>(protocolsJson) ?? [])
                socket.Options.AddSubProtocol(protocol);
            foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson) ?? [])
            {
                if (JsNetworkGuard.IsRefusedHeader(key) || JsNetworkGuard.IsMalformedHeader(key, value))
                {
                    ReportNetworkRefused(uri.Host, uri.Port, "A request header is not allowed.");
                    throw new JsHostException(JsNetworkGuard.IsRefusedHeader(key) ? $"The header '{key}' cannot be set." : "A request header is not valid.");
                }
                socket.Options.SetRequestHeader(key, value);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            socket.Dispose();
            throw new JsHostException("The web socket options are not valid.");
        }
        catch { socket.Dispose(); throw; }

        _wsConnects.Enqueue(now);
        var connection = new WsConnection(socket, CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token), _limits.MaxWsOutgoingWaiting);
        _sockets[id] = connection;
        _wsInvoker ??= new HttpMessageInvoker(JsNetworkGuard.CreateHandler(_network, ReportNetworkRefused));
        _ = Task.Run(() => RunSocketAsync(id, uri, connection));
    }

    private JsHostException WsLimit(string message)
    {
        ReportLimit(message);
        return new JsHostException(message);
    }

    private async Task RunSocketAsync(int id, Uri uri, WsConnection connection)
    {
        var token = connection.Cts.Token;
        int? code = null;
        string reason = "";
        string? error = null;
        try
        {
            using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                connectTimeout.CancelAfter(_limits.WsConnectTimeout);
                try { await connection.Socket.ConnectAsync(uri, _wsInvoker!, connectTimeout.Token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("The connection timed out."); }
            }

            await WsEventAsync(id, "open", "");
            var writer = Task.Run(() => WriteSocketAsync(connection, token), CancellationToken.None);
            try { (code, reason, error) = await ReadSocketAsync(id, connection, token); }
            finally { connection.Outgoing.Writer.TryComplete(); await writer.ConfigureAwait(false); }
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or HttpRequestException or TimeoutException or OperationCanceledException or ObjectDisposedException)
        {
            error = connection.ClosedByScript ? null : DescribeFailure(ex);
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            connection.Socket.Dispose();
            connection.Cts.Dispose();
        }

        if (_disposed) return;
        await WsEventAsync(id, "close", JsonSerializer.Serialize(new { code = code, reason, error }));
    }

    private async Task<(int? Code, string Reason, string? Error)> ReadSocketAsync(int id, WsConnection connection, CancellationToken token)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        var second = Stopwatch.GetTimestamp();
        var inSecond = 0;

        while (!token.IsCancellationRequested)
        {
            var result = await connection.Socket.ReceiveAsync(buffer, token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (connection.Socket.State == WebSocketState.CloseReceived)
                    await connection.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                return ((int?)result.CloseStatus, result.CloseStatusDescription ?? "", null);
            }
            if (result.MessageType == WebSocketMessageType.Binary)
            {
                await AbortSocketAsync(connection, WebSocketCloseStatus.InvalidMessageType);
                return (null, "", "Only text messages are supported.");
            }

            if (message.Length + result.Count > _limits.MaxWsMessageBytes)
            {
                ReportLimit($"A web socket received a message of more than {_limits.MaxWsMessageBytes / 1024} KB and was closed.");
                await AbortSocketAsync(connection, WebSocketCloseStatus.MessageTooBig);
                return (null, "", "The message is too large.");
            }
            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);

            // At most N messages a second: wait for the next second instead of dropping.
            if (Stopwatch.GetElapsedTime(second) >= TimeSpan.FromSeconds(1)) { second = Stopwatch.GetTimestamp(); inSecond = 0; }
            if (++inSecond > _limits.MaxWsIncomingPerSecond)
            {
                var wait = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(second);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
                second = Stopwatch.GetTimestamp();
                inSecond = 1;
            }
            await WsEventAsync(id, "message", text);
        }
        return (null, "", null);
    }

    private async Task WriteSocketAsync(WsConnection connection, CancellationToken token)
    {
        try
        {
            await foreach (var text in connection.Outgoing.Reader.ReadAllAsync(token))
                await connection.Socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, token);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or OperationCanceledException or ObjectDisposedException) { /* the read loop reports the end */ }
    }

    private static async Task AbortSocketAsync(WsConnection connection, WebSocketCloseStatus status)
    {
        try { await connection.Socket.CloseOutputAsync(status, "", new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException) { connection.Socket.Abort(); }
    }

    /// <summary>Runs one socket event on the plugin thread and returns when the script has handled it (so the next message is read only then).</summary>
    private async Task WsEventAsync(int id, string kind, string payload)
    {
        try
        {
            await Post(() =>
            {
                _press = null;
                try { Invoke("__wsEvent", id, kind, payload); }
                catch (InvalidOperationException) { /* already logged and counted */ }
                return 0;
            });
        }
        catch (ObjectDisposedException) { /* the plugin was stopped */ }
    }

    private void WsSend(int id, string text)
    {
        if (!_sockets.TryGetValue(id, out var connection)) throw new JsHostException("The web socket is closed.");
        if (Encoding.UTF8.GetByteCount(text) > _limits.MaxWsMessageBytes)
            throw WsLimit($"A web socket message can be at most {_limits.MaxWsMessageBytes / 1024} KB.");

        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(connection.SendSecond, now) >= TimeSpan.FromSeconds(1)) { connection.SendSecond = now; connection.SendsInSecond = 0; }
        if (++connection.SendsInSecond > _limits.MaxWsOutgoingPerSecond)
            throw WsLimit($"A plugin can send at most {_limits.MaxWsOutgoingPerSecond} web socket messages a second.");
        if (!connection.Outgoing.Writer.TryWrite(text))
            throw WsLimit($"Too many web socket messages are waiting to be sent (at most {_limits.MaxWsOutgoingWaiting}).");
    }

    private void WsClose(int id)
    {
        if (!_sockets.TryGetValue(id, out var connection)) return;
        connection.ClosedByScript = true;
        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                if (connection.Socket.State == WebSocketState.Open)
                    await connection.Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", timeout.Token);
                else connection.Cts.Cancel();
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                try { connection.Cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        });
    }

    private void CloseAllSockets()
    {
        foreach (var connection in _sockets.Values)
        {
            try { connection.Socket.Abort(); } catch (ObjectDisposedException) { }
        }
        _wsInvoker?.Dispose();
    }
}
