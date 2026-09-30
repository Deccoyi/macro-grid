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

    private PluginHostCollector Host(string id = "t", string name = "Test", double rate = 10000) =>
        new("1.4.0", _dir, id, new PluginStatusRegistry(), NullLogger.Instance, pluginName: name, problems: _problems, diagnosticsCallsPerSecond: rate);

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

    [Fact]
    public void A_report_with_a_key_replaces_the_line_of_that_key_instead_of_adding_another()
    {
        var diagnostics = Host().Diagnostics;
        diagnostics.Report(PluginDiagnosticLevel.Warning, "Retrying in 5 s", "link");
        diagnostics.Report(PluginDiagnosticLevel.Error, "Gave up", "link");

        var problem = Assert.Single(_problems.Snapshot());
        Assert.Equal(("Gave up", ProblemSeverity.Error), (problem.Message, problem.Severity));
    }

    [Fact]
    public void Updating_a_key_does_not_count_against_the_cap()
    {
        var diagnostics = Host().Diagnostics;
        for (var i = 0; i < 20; i++) diagnostics.Report(PluginDiagnosticLevel.Info, "m", "k" + i);
        diagnostics.Report(PluginDiagnosticLevel.Info, "changed", "k3");

        Assert.Equal(20, _problems.Snapshot().Count);
        Assert.Contains(_problems.Snapshot(), p => p.Message == "changed");
    }

    [Fact]
    public void Clear_removes_every_line_the_plugin_reported_and_no_other_plugin_s()
    {
        Host("b", "B").Diagnostics.Report(PluginDiagnosticLevel.Info, "other");
        var mine = Host().Diagnostics;
        mine.Report(PluginDiagnosticLevel.Info, "one");
        mine.Report(PluginDiagnosticLevel.Info, "two", "k");

        mine.Clear();

        Assert.Equal("b", Assert.Single(_problems.Snapshot()).Source);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("new\nline")]
    [InlineData("")]
    public void A_bad_key_is_dropped_without_an_exception(string key)
    {
        Host().Diagnostics.Report(PluginDiagnosticLevel.Info, "x", key);

        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void An_unknown_level_is_dropped_without_an_exception()
    {
        Host().Diagnostics.Report((PluginDiagnosticLevel)99, "x");

        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void Calls_faster_than_ten_a_second_are_dropped()
    {
        var diagnostics = Host(rate: 10).Diagnostics;
        for (var i = 0; i < 100; i++) diagnostics.Report(PluginDiagnosticLevel.Info, "line " + i, "k" + i);

        Assert.InRange(_problems.Snapshot().Count, 10, 12);
    }

    [Fact]
    public void After_the_host_is_retired_calls_do_nothing()
    {
        var host = Host();
        host.Diagnostics.Report(PluginDiagnosticLevel.Info, "before");
        host.Retire();
        _problems.Clear("t");

        host.Diagnostics.Report(PluginDiagnosticLevel.Error, "late");

        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void Direction_overrides_and_zero_width_characters_are_removed()
    {
        Host().Diagnostics.Report(PluginDiagnosticLevel.Info, "a‮b​c⁦d﻿e");

        Assert.Equal("abcde", Assert.Single(_problems.Snapshot()).Message);
    }

    [Fact]
    public void A_host_that_does_not_override_diagnostics_hands_out_a_silent_default()
    {
        IPluginHost host = new MinimalHost();

        host.Diagnostics.Report(PluginDiagnosticLevel.Error, "x");
        host.Diagnostics.Clear();
    }

    private sealed class MinimalHost : IPluginHost
    {
        public string ServerVersion => "";
        public string SdkVersion => "";
        public string DataDirectory => "";
        public IPluginSecrets Secrets => throw new NotSupportedException();
        public IPluginWidgets Widgets => throw new NotSupportedException();
        public void Log(string message) { }
        public void RegisterAction(IActionHandler handler) { }
        public void RegisterVariableProvider(IVariableProvider provider) { }
        public void RegisterSettingsPage(IPluginSettingsPage page) { }
        public IPluginStatusItem CreateStatusItem(string id) => throw new NotSupportedException();
        public void RegisterIconPack(IIconPackSource iconPack) { }
    }
}
