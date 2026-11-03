using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Images;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsPluginImageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-jsimg-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly ManualTime _time = new();
    private readonly PluginImageService _images;
    private readonly List<JsPlugin> _plugins = [];

    public JsPluginImageTests()
    {
        Directory.CreateDirectory(_dir);
        _images = new PluginImageService(NullLogger<PluginImageService>.Instance, _problems, _time);
    }

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        _images.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Start(string script)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(["variables"]), _variables, null, NullLogger.Instance, _ => { }, problems: _problems);
        _plugins.Add(plugin);
        _images.Attach("t", "T", _dir, null); // the manager makes the folder known before the plugin starts
        plugin.Initialize(new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance, problems: _problems, images: _images));
        _images.Attach("t", "T", _dir, plugin);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }

    [Fact]
    public async Task The_script_gets_the_requested_ids_and_its_put_is_accepted()
    {
        Start($"host.images.onRequest(ids => {{ host.variables.set('t.ids', ids.join(',')); host.variables.set('t.ok', host.images.put(ids[0], '{TestPictures.PngUrl()}')); }});");

        _images.Resolve("plugin-image:t/v1");
        _images.Resolve("plugin-image:t/v2");
        _time.Advance(TimeSpan.Zero);

        await WaitFor(() => _variables.Get("t.ok") is true);
        Assert.Equal("v1,v2", _variables.Get("t.ids"));
        Assert.Equal(TestPictures.PngUrl(), _images.TryGetDataUrl("t", "v1"));
        Assert.Null(_images.TryGetDataUrl("t", "v2"));
    }

    [Fact]
    public async Task A_put_nobody_asked_for_or_a_bad_one_returns_false_and_lists_one_problem()
    {
        Start("host.variables.set('t.a', host.images.put('x', 'data:image/svg+xml;base64,PHN2Zy8+')); host.variables.set('t.b', host.images.put('y', 'data:image/png;base64,iVBORw0KGgo='));");

        await WaitFor(() => _variables.Get("t.b") is not null);
        Assert.Equal(false, _variables.Get("t.a"));
        Assert.Equal(false, _variables.Get("t.b"));
        Assert.Equal(2, Assert.Single(_problems.Snapshot()).Count);
    }

    [Fact]
    public async Task A_script_without_a_handler_ignores_requests_and_a_throwing_handler_does_not_break_the_host()
    {
        Start("host.variables.set('t.up', true);");
        _images.Resolve("plugin-image:t/v1");
        _time.Advance(TimeSpan.Zero);
        await Task.Delay(100);
        Assert.Equal(true, _variables.Get("t.up"));

        Start("host.images.onRequest(() => { throw new Error('nope'); });");
        _images.Resolve("plugin-image:t/v2");
        _time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(100);
    }
}
