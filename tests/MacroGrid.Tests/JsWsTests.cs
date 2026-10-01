using System.Net;
using System.Net.WebSockets;
using System.Text;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsWsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-ws-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];
    private readonly List<HttpListener> _listeners = [];

    public JsWsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        foreach (var listener in _listeners) listener.Abort();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private JsPlugin Start(string script, string[] permissions, JsNetworkPolicy? network = null, JsPluginLimits? limits = null)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(permissions), _variables, null, NullLogger.Instance, _ => { },
            limits, problems: _problems, network: network ?? JsNetworkPolicy.None);
        _plugins.Add(plugin);
        plugin.Initialize(new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance));
        return plugin;
    }

    private static int FreePort()
    {
        var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    /// <summary>A web socket server on localhost; <paramref name="handler"/> runs for each accepted socket.</summary>
    private int Serve(Func<WebSocket, HttpListenerRequest, Task> handler, Action? onConnect = null)
    {
        var port = FreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        _listeners.Add(listener);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); } catch (Exception) { return; }
                onConnect?.Invoke();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var accepted = await context.AcceptWebSocketAsync(null);
                        await handler(accepted.WebSocket, context.Request);
                    }
                    catch (Exception) { /* a test closes sockets abruptly */ }
                });
            }
        });
        return port;
    }

    private static async Task Echo(WebSocket socket, HttpListenerRequest _)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                return;
            }
            await socket.SendAsync(buffer.AsMemory(0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
        }
    }

    private object? WaitFor(string name, int seconds = 5)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            if (_variables.Get(name) is { } value) return value;
            Thread.Sleep(20);
        }
        return null;
    }

    [Theory]
    [InlineData("ws:example.com:443")]
    [InlineData("ws:localhost:8080")]
    [InlineData("WS:nas:80")]
    public void A_ws_target_is_a_known_permission(string permission) => Assert.True(JsPermissions.IsKnown(permission));

    [Fact]
    public void An_http_permission_does_not_allow_a_socket_and_the_other_way_round()
    {
        var ws = new JsPermissions(["ws:localhost:9000"]);
        var http = new JsPermissions(["http:localhost:9000"]);
        Assert.True(ws.AllowsWs(new Uri("ws://localhost:9000/x")));
        Assert.False(ws.AllowsWs(new Uri("ws://localhost:9001/x")));
        Assert.False(ws.AllowsHttp(new Uri("http://localhost:9000/x")));
        Assert.False(http.AllowsWs(new Uri("ws://localhost:9000/x")));
        Assert.False(ws.AllowsWs(new Uri("http://localhost:9000/x")));
    }

    [Fact]
    public void A_message_goes_there_and_back_and_the_script_can_close()
    {
        var port = Serve(Echo);
        Start($$"""
            const s = host.ws.connect('ws://localhost:{{port}}/', {
              onOpen() { s.send('hello'); },
              onMessage(text) { host.variables.set('t.got', text); s.close(); },
              onClose(info) { host.variables.set('t.closed', info.error === null ? 'clean' : info.error); },
            });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Equal("hello", WaitFor("t.got"));
        Assert.Equal("clean", WaitFor("t.closed"));
    }

    [Fact]
    public void Without_the_permission_connect_throws()
    {
        var port = Serve(Echo);
        Start($"try {{ host.ws.connect('ws://localhost:{port}/', {{}}); }} catch (e) {{ host.variables.set('t.err', e.message); }}", ["variables", $"http:localhost:{port}"]);
        Assert.Contains("not been granted", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void A_plain_socket_to_an_internet_name_is_refused()
    {
        Start("try { host.ws.connect('ws://example.com:80/', {}); } catch (e) { host.variables.set('t.err', e.message); }", ["variables", "ws:example.com:80"]);
        Assert.Contains("wss://", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void Macro_Grids_own_port_is_refused_even_when_approved()
    {
        var hits = 0;
        var port = Serve(Echo, () => Interlocked.Increment(ref hits));
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onClose(info) { host.variables.set('t.err', info.error); } });
            """, ["variables", $"ws:localhost:{port}"], new JsNetworkPolicy([port]));

        Assert.Contains("Macro Grid itself", (string)WaitFor("t.err")!);
        Assert.Equal(0, hits);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.NetworkRefused);
    }

    [Fact]
    public void A_header_that_steers_the_request_is_refused()
    {
        var port = Serve(Echo);
        Start($"try {{ host.ws.connect('ws://localhost:{port}/', {{ headers: {{ 'Origin': 'http://localhost:9820' }} }}); }} catch (e) {{ host.variables.set('t.err', e.message); }}", ["variables", $"ws:localhost:{port}"]);
        Assert.Contains("Origin", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void Only_two_sockets_can_be_open_at_once()
    {
        var port = Serve(async (socket, _) => { await Task.Delay(5000); });
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', {});
            host.ws.connect('ws://localhost:{{port}}/', {});
            try { host.ws.connect('ws://localhost:{{port}}/', {}); } catch (e) { host.variables.set('t.err', e.message); }
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Contains("at most 2", (string)_variables.Get("t.err")!);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.LimitReached);
    }

    [Fact]
    public void Connects_are_limited_per_minute()
    {
        var port = Serve(Echo);
        Start($$"""
            let count = 0;
            for (let i = 0; i < 12; i++) {
              try { const s = host.ws.connect('ws://localhost:{{port}}/', {}); s.close(); count++; }
              catch (e) { host.variables.set('t.err', e.message); }
            }
            """, ["variables", $"ws:localhost:{port}"], limits: JsPluginLimits.Default with { MaxSockets = 50 });

        Assert.Contains("a minute", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void A_binary_message_closes_the_socket()
    {
        var port = Serve(async (socket, _) =>
        {
            await socket.SendAsync(new byte[] { 1, 2, 3 }, WebSocketMessageType.Binary, true, CancellationToken.None);
            await Task.Delay(3000);
        });
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onClose(info) { host.variables.set('t.err', info.error); } });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Contains("text", (string)WaitFor("t.err")!);
    }

    [Fact]
    public void A_message_that_is_too_large_closes_the_socket_and_is_listed()
    {
        var port = Serve(async (socket, _) =>
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(new string('x', 70_000)), WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(3000);
        });
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onClose(info) { host.variables.set('t.err', info.error); } });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Contains("too large", (string)WaitFor("t.err")!);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.LimitReached);
    }

    [Fact]
    public void Sending_too_fast_throws()
    {
        var port = Serve(Echo);
        Start($$"""
            const s = host.ws.connect('ws://localhost:{{port}}/', {});
            let n = 0;
            try { for (let i = 0; i < 40; i++) { s.send('x'); n++; } } catch (e) { host.variables.set('t.err', e.message); }
            host.variables.set('t.sent', n);
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Equal(20d, _variables.Get("t.sent"));
        Assert.Contains("a second", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void A_close_from_the_server_arrives_with_its_code()
    {
        var port = Serve(async (socket, _) => await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "no", CancellationToken.None));
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onClose(info) { host.variables.set('t.code', info.code); host.variables.set('t.reason', info.reason); } });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Equal(1008d, WaitFor("t.code"));
        Assert.Equal("no", WaitFor("t.reason"));
    }

    [Fact]
    public void A_connect_that_fails_ends_in_onClose_with_an_error()
    {
        var port = FreePort(); // nothing listens
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onClose(info) { host.variables.set('t.err', info.error); } });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.NotNull(WaitFor("t.err"));
    }

    [Fact]
    public void Stopping_the_plugin_closes_its_sockets()
    {
        var closed = new TaskCompletionSource();
        var port = Serve(async (socket, _) =>
        {
            var buffer = new byte[16];
            try { while ((await socket.ReceiveAsync(buffer, CancellationToken.None)).MessageType != WebSocketMessageType.Close) { } }
            catch (WebSocketException) { }
            closed.TrySetResult();
        });
        var plugin = Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onOpen() { host.variables.set('t.open', true); } });
            """, ["variables", $"ws:localhost:{port}"]);

        Assert.Equal(true, WaitFor("t.open"));
        plugin.Dispose();
        Assert.True(closed.Task.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_message_never_carries_a_press_window()
    {
        var port = Serve(async (socket, _) =>
        {
            await Task.Delay(300);
            await socket.SendAsync("go"u8.ToArray(), WebSocketMessageType.Text, true, CancellationToken.None);
            await Task.Delay(3000);
        });
        // The socket is opened by the script itself; a message arrives later and tries to type.
        Start($$"""
            host.ws.connect('ws://localhost:{{port}}/', { onMessage() {
              try { host.input.type('x'); } catch (e) { host.variables.set('t.err', e.message); }
            } });
            """, ["variables", "input", $"ws:localhost:{port}"]);

        Assert.Contains("button press", (string)WaitFor("t.err")!);
    }
}
