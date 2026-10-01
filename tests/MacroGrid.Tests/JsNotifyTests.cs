using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsNotifyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-notify-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];

    public JsNotifyTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Start(string script, PluginNotifications? notifications, params string[] permissions)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "My Plugin", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(permissions), _variables, null, NullLogger.Instance, _ => { }, problems: _problems, notifications: notifications);
        _plugins.Add(plugin);
        plugin.Initialize(new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance));
    }

    [Fact]
    public void Notify_is_a_known_permission() => Assert.True(JsPermissions.IsKnown("notify"));

    [Fact]
    public void A_notice_is_shown_with_the_plugins_own_name_as_title()
    {
        var notifications = new PluginNotifications();
        var shown = new List<(string Title, string Text)>();
        notifications.Posted += (title, text) => shown.Add((title, text));

        Start("host.notify('Build finished');", notifications, "notify");

        Assert.Equal([("My Plugin", "Build finished")], shown);
    }

    [Fact]
    public void Without_the_permission_nothing_is_shown()
    {
        var notifications = new PluginNotifications();
        var shown = 0;
        notifications.Posted += (_, _) => shown++;

        Start("try { host.notify('x'); } catch (e) { host.variables.set('t.err', e.message); }", notifications, "variables");

        Assert.Equal(0, shown);
        Assert.Contains("'notify' permission", (string)_variables.Get("t.err")!);
    }

    [Fact]
    public void Without_a_display_the_call_does_nothing()
    {
        Start("host.notify('x'); host.variables.set('t.ok', true);", new PluginNotifications(), "notify", "variables");
        Assert.Equal(true, _variables.Get("t.ok"));
        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void Extra_notices_are_dropped_and_listed_once()
    {
        var notifications = new PluginNotifications();
        var shown = 0;
        notifications.Posted += (_, _) => shown++;

        Start("for (let i = 0; i < 6; i++) host.notify('n' + i);", notifications, "notify");

        Assert.Equal(3, shown);
        var line = Assert.Single(_problems.Snapshot(), p => p.Code == ProblemCodes.LimitReached);
        Assert.Equal(3, line.Count);
    }

    [Fact]
    public void The_limit_refills_over_time()
    {
        var now = DateTimeOffset.UtcNow;
        var notifications = new PluginNotifications(() => now);
        notifications.Posted += (_, _) => { };

        for (var i = 0; i < 3; i++) Assert.Equal(NoticeResult.Shown, notifications.Post("a", "A", "x"));
        Assert.Equal(NoticeResult.Dropped, notifications.Post("a", "A", "x"));
        Assert.Equal(NoticeResult.Shown, notifications.Post("b", "B", "x")); // another plugin has its own allowance

        now += TimeSpan.FromSeconds(31);
        Assert.Equal(NoticeResult.Shown, notifications.Post("a", "A", "x"));
        Assert.Equal(NoticeResult.Dropped, notifications.Post("a", "A", "x"));
    }

    [Fact]
    public void Text_is_cleaned_and_cut()
    {
        var notifications = new PluginNotifications();
        string? text = null;
        notifications.Posted += (_, t) => text = t;

        notifications.Post("a", "A", "line\r\none‮two" + new string('x', 300));

        Assert.Equal(PluginNotifications.MaxTextLength, text!.Length);
        Assert.DoesNotContain('\n', text);
        Assert.DoesNotContain('‮', text);
    }

    [Fact]
    public void An_empty_notice_is_dropped()
    {
        var notifications = new PluginNotifications();
        notifications.Posted += (_, _) => Assert.Fail("shown");
        Assert.Equal(NoticeResult.Dropped, notifications.Post("a", "A", "  \r\n "));
    }
}
