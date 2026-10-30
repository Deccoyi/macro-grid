using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-store-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];

    public JsStorageTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private JsPlugin Start(string script, params string[] permissions)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(permissions), _variables, null, NullLogger.Instance, _ => { }, problems: _problems);
        _plugins.Add(plugin);
        plugin.Initialize(new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance));
        return plugin;
    }

    [Fact]
    public void Storage_is_a_known_permission() => Assert.True(JsPermissions.IsKnown("storage"));

    [Fact]
    public void A_stored_value_comes_back_as_it_was_and_keys_are_listed()
    {
        Start("""
            host.storage.set('a', { n: 1, list: [1, 2] });
            host.storage.set('b', 'text');
            host.variables.set('t.a', host.storage.get('a').list[1]);
            host.variables.set('t.b', host.storage.get('b'));
            host.variables.set('t.keys', host.storage.keys().join(','));
            host.variables.set('t.missing', host.storage.get('nope') === null ? 'null' : 'other');
            host.storage.remove('b');
            host.variables.set('t.after', host.storage.keys().join(','));
            """, "variables", "storage");

        Assert.Equal(2d, _variables.Get("t.a"));
        Assert.Equal("text", _variables.Get("t.b"));
        Assert.Equal("a,b", _variables.Get("t.keys"));
        Assert.Equal("null", _variables.Get("t.missing"));
        Assert.Equal("a", _variables.Get("t.after"));
    }

    [Fact]
    public void Without_the_permission_the_call_is_refused()
    {
        Start("try { host.storage.get('a'); } catch (e) { host.variables.set('t.err', e.message); }", "variables");
        Assert.Contains("'storage' permission", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void Values_survive_a_restart_of_the_plugin()
    {
        var first = Start("host.storage.set('count', 41);", "storage");
        first.Dispose(); // writes what is waiting

        Assert.True(File.Exists(Path.Combine(_dir, "storage.json")));
        Start("host.variables.set('t.count', host.storage.get('count') + 1);", "variables", "storage");
        Assert.Equal(42d, _variables.Get("t.count"));
    }

    [Fact]
    public void An_unreadable_file_starts_empty()
    {
        File.WriteAllText(Path.Combine(_dir, "storage.json"), "{ not json");
        Start("host.variables.set('t.n', host.storage.keys().length);", "variables", "storage");
        Assert.Equal(0d, _variables.Get("t.n"));
    }

    [Theory]
    [InlineData("''")]
    [InlineData("'has space'")]
    [InlineData("'a'.repeat(65)")]
    [InlineData("'../x'")]
    public void A_bad_key_is_refused_and_reported(string key)
    {
        Start($"try {{ host.storage.set({key}, 1); }} catch (e) {{ host.variables.set('t.err', e.message); }}", "variables", "storage");
        Assert.Contains("storage key", (string)_variables.Get("t.err")!);
        Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.LimitReached);
    }

    [Fact]
    public void A_value_over_the_limit_is_refused()
    {
        Start("try { host.storage.set('big', 'x'.repeat(17000)); } catch (e) { host.variables.set('t.err', e.message); }", "variables", "storage");
        Assert.Contains("16 KB", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void More_than_64_keys_are_refused_but_an_existing_key_can_change()
    {
        Start("""
            for (let i = 0; i < 64; i++) host.storage.set('k' + i, i);
            host.storage.set('k0', 'changed');
            try { host.storage.set('one-more', 1); } catch (e) { host.variables.set('t.err', e.message); }
            """, "variables", "storage");
        Assert.Contains("64 keys", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void The_total_is_limited()
    {
        Start("""
            for (let i = 0; i < 16; i++) host.storage.set('k' + i, 'x'.repeat(16000));
            try { host.storage.set('k16', 'x'.repeat(16000)); } catch (e) { host.variables.set('t.err', e.message); }
            """, "variables", "storage");
        Assert.Contains("in total", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void A_reached_limit_is_one_line_with_a_count()
    {
        Start("for (let i = 0; i < 3; i++) { try { host.storage.set('big', 'x'.repeat(17000)); } catch (e) { } }", "storage");
        var line = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.LimitReached);
        Assert.Equal(3, line.Count);
    }
}
