using System.Net;
using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsPluginTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-js-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly List<JsPlugin> _plugins = [];
    private readonly List<string> _faults = [];

    public JsPluginTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly JsPluginLimits Tight = JsPluginLimits.Default with
    {
        CallTimeout = TimeSpan.FromMilliseconds(400),
        MemoryBytes = 16 * 1024 * 1024,
    };

    private static PluginManifest Manifest(string id = "t") => new()
    {
        Id = id, Name = "T", Version = "1.0.0", MacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js,
    };

    private (JsPlugin Plugin, PluginHostCollector Host) Start(string script, string[]? permissions = null, JsPluginLimits? limits = null)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var plugin = new JsPlugin(Manifest(), path, new JsPermissions(permissions ?? ["variables", "actions"]),
            _variables, null, NullLogger.Instance, _faults.Add, limits ?? Tight);
        _plugins.Add(plugin);
        var host = new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance);
        plugin.Initialize(host);
        return (plugin, host);
    }

    private static Task Run(PluginHostCollector host, string type, JsonObject? settings = null) =>
        host.Actions.Single(a => a.Type == type).ExecuteAsync(new ActionContext("d", "p", "w", null!, 2), settings ?? [], CancellationToken.None);

    [Fact]
    public async Task A_registered_action_runs_with_its_settings()
    {
        var (_, host) = Start("host.registerAction({ type: 't.double', name: 'Double', run: (ctx, s) => host.variables.set('t.result', s.x * 2 + ctx.value) });");

        await Run(host, "t.double", new JsonObject { ["x"] = 20 });

        Assert.Equal(42.0, _variables.Get("t.result"));
        Assert.Equal("Double", host.Actions.Single().DisplayName);
    }

    [Fact]
    public void An_action_describes_its_fields_for_the_editor_form()
    {
        var (_, host) = Start("host.registerAction({ type: 't.a', name: 'A', category: 'Test', fields: [{ key: 'scene', label: 'Scene', kind: 'Text' }], run() {} });");

        var descriptor = Assert.IsAssignableFrom<IActionDescriptor>(host.Actions.Single());
        Assert.Equal("Test", descriptor.Category);
        Assert.Equal("scene", Assert.Single(descriptor.Fields).Key);
    }

    [Fact]
    public void Registering_an_action_needs_the_actions_permission()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Start("host.registerAction({ type: 't.a', run() {} });", ["variables"]));

        Assert.Contains("'actions'", ex.Message);
    }

    [Fact]
    public void A_plugin_can_only_register_action_types_under_its_own_id()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Start("host.registerAction({ type: 'core.hotkey', run() {} });"));

        Assert.Contains("must start with 't.'", ex.Message);
    }

    [Fact]
    public void A_plugin_cannot_overwrite_variables_it_does_not_own()
    {
        Start("try { host.variables.set('system.cpu', 1); } catch (e) { host.variables.set('t.error', e.message); }");

        Assert.Null(_variables.Get("system.cpu"));
        Assert.Contains("must start with 't.'", (string)_variables.Get("t.error")!);
    }

    [Fact]
    public void The_script_has_no_way_into_dotnet_or_the_native_functions()
    {
        Start("host.variables.set('t.probe', [typeof System, typeof importNamespace, typeof __log, typeof __varSet, typeof Packages].join(','));");

        Assert.Equal("undefined,undefined,undefined,undefined,undefined", _variables.Get("t.probe"));
    }

    [Fact]
    public void The_host_object_cannot_be_replaced_or_extended()
    {
        Start("try { host.variables = null; } catch (e) {} try { host.evil = 1; } catch (e) {} host.variables.set('t.frozen', typeof host.evil + ':' + typeof host.variables.set);");

        Assert.Equal("undefined:function", _variables.Get("t.frozen"));
    }

    [Fact]
    public void An_endless_loop_at_start_is_cut_off()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Start("while (true) {}"));

        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public async Task An_endless_loop_in_an_action_fails_that_call_and_the_plugin_keeps_working()
    {
        var (_, host) = Start("host.registerAction({ type: 't.spin', run() { while (true) {} } }); host.registerAction({ type: 't.ok', run() { host.variables.set('t.ok', 1); } });");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(host, "t.spin"));
        await Run(host, "t.ok");

        Assert.Equal(1.0, _variables.Get("t.ok"));
    }

    [Fact]
    public void Runaway_memory_use_is_cut_off()
    {
        Assert.Throws<InvalidOperationException>(() => Start("const hog = []; while (true) { hog.push(new Array(200000).fill(1)); }"));
    }

    [Fact]
    public void Runaway_recursion_is_cut_off()
    {
        Assert.Throws<InvalidOperationException>(() => Start("function f() { return f() + 1; } f();"));
    }

    [Fact]
    public async Task A_plugin_that_keeps_failing_is_switched_off()
    {
        var (_, host) = Start("host.registerAction({ type: 't.boom', run() { throw new Error('nope'); } });");

        for (var i = 0; i < JsPluginLimits.Default.MaxConsecutiveErrors; i++)
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(host, "t.boom"));

        Assert.Contains("Switched off", Assert.Single(_faults));
    }

    [Fact]
    public async Task A_timer_runs_repeatedly_and_can_be_cancelled()
    {
        Start("let n = 0; const id = host.every(100, () => { n++; host.variables.set('t.ticks', n); if (n === 3) host.cancel(id); });");

        await WaitAsync(() => _variables.Get("t.ticks") is 3.0);
        await Task.Delay(400);

        Assert.Equal(3.0, _variables.Get("t.ticks"));
    }

    [Fact]
    public void The_shortest_timer_is_limited()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Start("host.every(10, () => {});"));

        Assert.Contains("100 ms", ex.Message);
    }

    [Fact]
    public void Http_is_refused_without_a_matching_permission()
    {
        Start("try { host.http.get('http://localhost:9/x'); } catch (e) { host.variables.set('t.http', e.message); }");

        Assert.Contains("http:localhost:9", (string)_variables.Get("t.http")!);
    }

    [Fact]
    public void Http_goes_only_to_the_approved_host_and_port()
    {
        using var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var bytes = "hello"u8.ToArray();
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        });

        Start($"const r = host.http.get('http://localhost:{port}/x'); host.variables.set('t.body', r.status + ':' + r.body);",
            ["variables", $"http:localhost:{port}"]);

        Assert.Equal("200:hello", _variables.Get("t.body"));
    }

    [Fact]
    public void Input_needs_its_permission()
    {
        Start("try { host.input.hotkey('ctrl+c'); } catch (e) { host.variables.set('t.input', e.message); }");

        Assert.Contains("'input'", (string)_variables.Get("t.input")!);
    }

    [Fact]
    public void A_settings_page_stores_only_declared_fields()
    {
        var (_, host) = Start("host.settings.page([{ key: 'name', label: 'Name', kind: 'Text', default: 'x' }]);");
        var page = host.SettingsPage!;

        Assert.Equal("x", page.Load()["name"]!.GetValue<string>());
        page.Save(new JsonObject { ["name"] = "y", ["stolen"] = "z" });

        Assert.Equal("y", page.Load()["name"]!.GetValue<string>());
        Assert.Null(page.Load()["stolen"]);
    }

    private static int FreePort()
    {
        var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(50);
        Assert.True(condition(), "Condition was not met in time.");
    }
}
