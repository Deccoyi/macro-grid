using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginDiagnosticsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-diag-" + Guid.NewGuid().ToString("N"));
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];

    public PluginDiagnosticsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private PluginHostCollector Host(string id = "t", string name = "Test") =>
        new("1.4.0", _dir, id, new PluginStatusRegistry(), NullLogger.Instance, pluginName: name, problems: _problems);

    [Fact]
    public void A_reported_problem_shows_up_under_the_plugin_with_its_severity()
    {
        Host().Diagnostics.Report(PluginDiagnosticLevel.Warning, "Wrong password");

        var problem = Assert.Single(_problems.Snapshot());
        Assert.Equal(("t", "Test", ProblemSeverity.Warning, ProblemCodes.PluginReported, "Wrong password"), (problem.Source, problem.SourceName, problem.Severity, problem.Code, problem.Message));
    }

    [Fact]
    public void Resolving_a_key_removes_only_the_lines_with_that_key()
    {
        var diagnostics = Host().Diagnostics;
        diagnostics.Report(PluginDiagnosticLevel.Error, "No connection", "link");
        diagnostics.Report(PluginDiagnosticLevel.Error, "Bad file", "file");

        diagnostics.Resolve("link");

        Assert.Equal("Bad file", Assert.Single(_problems.Snapshot()).Message);
    }

    [Fact]
    public void A_plugin_can_keep_only_twenty_different_lines_and_the_others_are_dropped()
    {
        var diagnostics = Host().Diagnostics;
        for (var i = 0; i < 50; i++) diagnostics.Report(PluginDiagnosticLevel.Info, "line " + i);
        diagnostics.Report(PluginDiagnosticLevel.Info, "line 0");

        var lines = _problems.Snapshot();
        Assert.Equal(20, lines.Count);
        Assert.Equal(2, lines.Single(p => p.Message == "line 0").Count);
    }

    [Fact]
    public void One_plugin_filling_its_lines_does_not_block_another()
    {
        var first = Host("a", "A").Diagnostics;
        for (var i = 0; i < 30; i++) first.Report(PluginDiagnosticLevel.Info, "x" + i);

        Host("b", "B").Diagnostics.Report(PluginDiagnosticLevel.Info, "mine");

        Assert.Contains(_problems.Snapshot(), p => p.Source == "b");
    }

    [Fact]
    public void Control_characters_and_long_text_are_cleaned()
    {
        Host().Diagnostics.Report(PluginDiagnosticLevel.Info, "a\nb\u0007" + new string('x', 500));

        var message = Assert.Single(_problems.Snapshot()).Message;
        Assert.Equal(300, message.Length);
        Assert.StartsWith("abxxx", message);
    }

    [Fact]
    public void A_javascript_plugin_reports_and_resolves_through_host_diagnostics()
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, "host.diagnostics.report('warning', 'Server unreachable', 'net'); host.diagnostics.report('error', 'Bad setting');");
        var plugin = new JsPlugin(new PluginManifest { Id = "t", Name = "Test", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js },
            path, new JsPermissions([]), new VariableStore(), null, NullLogger.Instance, _ => { }, JsPluginLimits.Default);
        _plugins.Add(plugin);
        var host = Host();
        plugin.Initialize(host);

        Assert.Equal(2, _problems.Snapshot().Count);
        host.Diagnostics.Resolve("net");
        Assert.Equal("Bad setting", Assert.Single(_problems.Snapshot()).Message);
    }

    [Fact]
    public void A_host_without_a_problem_list_ignores_reports()
    {
        var host = new PluginHostCollector("1.4.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance);

        host.Diagnostics.Report(PluginDiagnosticLevel.Error, "nobody listens");
        host.Diagnostics.Resolve("x");
    }
}
