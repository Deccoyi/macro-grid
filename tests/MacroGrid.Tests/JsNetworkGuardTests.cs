using System.Net;
using System.Text.Json;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsNetworkGuardTests : IDisposable
{
    public sealed record ScopeCase(string Host, string Scope);

    public static IEnumerable<object[]> ScopeCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "network-scope-cases.json");
        var cases = JsonSerializer.Deserialize<List<ScopeCase>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(ScopeCases))]
    public void The_scope_of_a_name_follows_the_shared_table(ScopeCase c) =>
        Assert.Equal(c.Scope, JsNetworkGuard.ScopeOf(c.Host).ToString().ToLowerInvariant());

    [Theory]
    [InlineData("127.0.0.1", AddressKind.Loopback)]
    [InlineData("127.9.9.9", AddressKind.Loopback)]
    [InlineData("::1", AddressKind.Loopback)]
    [InlineData("10.1.2.3", AddressKind.Private)]
    [InlineData("172.16.0.1", AddressKind.Private)]
    [InlineData("172.15.0.1", AddressKind.Public)]
    [InlineData("192.168.0.1", AddressKind.Private)]
    [InlineData("169.254.0.9", AddressKind.Private)]
    [InlineData("100.64.1.1", AddressKind.Private)]
    [InlineData("fc00::1", AddressKind.Private)]
    [InlineData("fd12:3456::1", AddressKind.Private)]
    [InlineData("fe80::1", AddressKind.Private)]
    [InlineData("8.8.8.8", AddressKind.Public)]
    [InlineData("2606:4700::1111", AddressKind.Public)]
    [InlineData("0.0.0.0", AddressKind.Never)]
    [InlineData("255.255.255.255", AddressKind.Never)]
    [InlineData("224.0.0.1", AddressKind.Never)]
    [InlineData("ff02::1", AddressKind.Never)]
    [InlineData("::", AddressKind.Never)]
    [InlineData("::ffff:127.0.0.1", AddressKind.Loopback)]
    [InlineData("::ffff:192.168.1.1", AddressKind.Private)]
    [InlineData("::ffff:8.8.8.8", AddressKind.Public)]
    public void An_address_is_classified(string address, AddressKind expected) =>
        Assert.Equal(expected, JsNetworkGuard.KindOf(IPAddress.Parse(address)));

    [Fact]
    public void Only_addresses_of_the_approved_kind_are_allowed()
    {
        IPAddress[] mixed = [IPAddress.Parse("127.0.0.1"), IPAddress.Parse("192.168.1.5"), IPAddress.Parse("8.8.8.8")];
        Assert.Equal(["8.8.8.8"], JsNetworkGuard.Allowed("example.com", 443, mixed, [], _ => false).Select(a => a.ToString()));
        Assert.Equal(["192.168.1.5"], JsNetworkGuard.Allowed("nas", 80, mixed, [], _ => false).Select(a => a.ToString()));
        Assert.Equal(["127.0.0.1"], JsNetworkGuard.Allowed("localhost", 80, mixed, [], _ => false).Select(a => a.ToString()));
    }

    [Fact]
    public void Macro_Grids_own_ports_are_never_allowed_on_this_computer()
    {
        IPAddress[] loop = [IPAddress.Loopback];
        Assert.Empty(JsNetworkGuard.Allowed("localhost", 9820, loop, [9820, 9821], _ => false));
        Assert.NotEmpty(JsNetworkGuard.Allowed("localhost", 9000, loop, [9820, 9821], _ => false));
        // This computer's own LAN address on the server's port.
        IPAddress[] own = [IPAddress.Parse("192.168.1.5")];
        Assert.Empty(JsNetworkGuard.Allowed("nas", 9820, own, [9820], a => a.ToString() == "192.168.1.5"));
        // Another computer's address on the same port number is fine.
        Assert.NotEmpty(JsNetworkGuard.Allowed("nas", 9820, own, [9820], _ => false));
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("host")]
    [InlineData("Origin")]
    [InlineData("Connection")]
    [InlineData("Upgrade")]
    [InlineData("Content-Length")]
    [InlineData("Transfer-Encoding")]
    [InlineData("TE")]
    [InlineData("Sec-WebSocket-Key")]
    [InlineData("sec-fetch-site")]
    [InlineData("Proxy-Authorization")]
    public void Some_headers_cannot_be_set(string name) => Assert.True(JsNetworkGuard.IsRefusedHeader(name));

    [Theory]
    [InlineData("Authorization")]
    [InlineData("X-Api-Key")]
    [InlineData("Accept")]
    [InlineData("User-Agent")]
    public void Ordinary_headers_can_be_set(string name)
    {
        Assert.False(JsNetworkGuard.IsRefusedHeader(name));
        Assert.False(JsNetworkGuard.IsMalformedHeader(name, "value"));
    }

    [Theory]
    [InlineData("X-A", "one\r\nHost: localhost")]
    [InlineData("X-A", "one\ntwo")]
    [InlineData("X A", "v")]
    [InlineData("X-A:", "v")]
    [InlineData("", "v")]
    public void A_header_that_cannot_be_sent_as_it_is_is_malformed(string name, string value) =>
        Assert.True(JsNetworkGuard.IsMalformedHeader(name, value));

    // ---- through a plugin ----

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-net-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];

    public JsNetworkGuardTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Start(string script, string[] permissions, JsNetworkPolicy network)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(permissions), _variables, null, NullLogger.Instance, _ => { },
            JsPluginLimits.Default with { CallTimeout = TimeSpan.FromMilliseconds(3000) }, problems: _problems, network: network);
        _plugins.Add(plugin);
        plugin.Initialize(new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance));
    }

    private static int FreePort()
    {
        var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private static HttpListener Serve(int port, Action<HttpListenerRequest>? onRequest = null)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await listener.GetContextAsync(); } catch (Exception) { return; }
                onRequest?.Invoke(context.Request);
                await context.Response.OutputStream.WriteAsync("hello"u8.ToArray());
                context.Response.Close();
            }
        });
        return listener;
    }

    [Fact]
    public void A_request_to_an_approved_port_that_is_Macro_Grids_own_is_refused_and_reported_once()
    {
        var port = FreePort();
        var hits = 0;
        using var listener = Serve(port, _ => Interlocked.Increment(ref hits));

        Start($"for (let i = 0; i < 2; i++) {{ try {{ host.http.get('http://localhost:{port}/x'); }} catch (e) {{ host.variables.set('t.err', e.message); }} }}",
            ["variables", $"http:localhost:{port}"], new JsNetworkPolicy([port]));

        Assert.Contains("Macro Grid itself", (string)_variables.Get("t.err")!);
        Assert.Equal(0, hits);
        var line = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.NetworkRefused);
        Assert.Equal(2, line.Count);
    }

    [Fact]
    public void The_same_port_is_fine_when_it_is_not_one_of_the_servers()
    {
        var port = FreePort();
        using var listener = Serve(port);

        Start($"host.variables.set('t.body', host.http.get('http://localhost:{port}/x').body);", ["variables", $"http:localhost:{port}"], new JsNetworkPolicy([port + 1]));

        Assert.Equal("hello", _variables.Get("t.body"));
        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void A_name_that_looks_like_the_internet_but_points_inside_is_refused()
    {
        var port = FreePort();
        var hits = 0;
        using var listener = Serve(port, _ => Interlocked.Increment(ref hits));
        var policy = new JsNetworkPolicy([], (_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

        Start($"try {{ host.http.get('http://example.test:{port}/x'); }} catch (e) {{ host.variables.set('t.err', e.message); }}",
            ["variables", $"http:example.test:{port}"], policy);

        Assert.StartsWith("The request failed", (string)_variables.Get("t.err")!);
        Assert.Equal(0, hits);
        Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.NetworkRefused);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Origin")]
    [InlineData("Connection")]
    [InlineData("Proxy-Authorization")]
    [InlineData("Sec-Fetch-Mode")]
    public void A_refused_header_stops_the_request(string header)
    {
        var port = FreePort();
        var hits = 0;
        using var listener = Serve(port, _ => Interlocked.Increment(ref hits));

        Start($"try {{ host.http.get('http://localhost:{port}/x', {{ headers: {{ '{header}': 'localhost:9820' }} }}); }} catch (e) {{ host.variables.set('t.err', e.message); }}",
            ["variables", $"http:localhost:{port}"], JsNetworkPolicy.None);

        Assert.Contains(header, (string)_variables.Get("t.err")!);
        Assert.Equal(0, hits);
        Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.NetworkRefused);
    }

    [Fact]
    public void A_header_value_with_a_line_break_stops_the_request()
    {
        var port = FreePort();
        using var listener = Serve(port);

        Start($"try {{ host.http.get('http://localhost:{port}/x', {{ headers: {{ 'X-A': 'one\\r\\nHost: x' }} }}); }} catch (e) {{ host.variables.set('t.err', e.message); }}",
            ["variables", $"http:localhost:{port}"], JsNetworkPolicy.None);

        Assert.Contains("not valid", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void An_ordinary_header_still_reaches_the_server()
    {
        var port = FreePort();
        string? seen = null;
        using var listener = Serve(port, r => seen = r.Headers["X-Api-Key"]);

        Start($"host.variables.set('t.body', host.http.get('http://localhost:{port}/x', {{ headers: {{ 'X-Api-Key': 'k1' }} }}).body);",
            ["variables", $"http:localhost:{port}"], JsNetworkPolicy.None);

        Assert.Equal("hello", _variables.Get("t.body"));
        Assert.Equal("k1", seen);
    }
}
