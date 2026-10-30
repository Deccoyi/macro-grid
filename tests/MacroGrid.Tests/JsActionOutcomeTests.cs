using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>What a script action can report: a coded failure, "accepted", or a promise the host waits for.</summary>
public sealed class JsActionOutcomeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-jsout-" + Guid.NewGuid().ToString("N"));
    private readonly List<JsPlugin> _plugins = [];
    private readonly List<string> _faults = [];

    public JsActionOutcomeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly JsPluginLimits Tight = JsPluginLimits.Default with { CallTimeout = TimeSpan.FromMilliseconds(1500), MemoryBytes = 16 * 1024 * 1024 };

    private (JsPlugin Plugin, PluginHostCollector Host, ProblemList Problems) Start(string script, JsPluginLimits? limits = null)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var problems = new ProblemList();
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(["variables", "actions"]), new VariableStore(), null, NullLogger.Instance, _faults.Add,
            limits ?? Tight, problems: problems);
        _plugins.Add(plugin);
        var host = new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), NullLogger.Instance);
        plugin.Initialize(host);
        return (plugin, host, problems);
    }

    private static Task<ActionOutcome> Outcome(PluginHostCollector host) =>
        ((IActionOutcomeHandler)host.Actions.Single(a => a.Type == "t.a")).ExecuteWithOutcomeAsync(new ActionContext("d", "p", "w", null!), [], CancellationToken.None);

    private (JsPlugin Plugin, PluginHostCollector Host, ProblemList Problems) StartAction(string definition, JsPluginLimits? limits = null) =>
        Start($"host.registerAction({{ type: 't.a', {definition} }});", limits);

    [Fact]
    public async Task A_synchronous_action_can_report_a_coded_failure()
    {
        var (_, host, _) = StartAction("run() { return { ok: false, code: 'NotConnected', message: 'Nothing is open.' }; }");

        var outcome = await Outcome(host);

        Assert.Equal(ActionOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(ActionFailureCode.NotConnected, outcome.Code);
        Assert.Equal("Nothing is open.", outcome.Message);
    }

    [Theory]
    [InlineData("{ ok: false, code: 'Nope', message: 'x' }", ActionFailureCode.ProviderError)]
    [InlineData("{ ok: false, message: 'x' }", ActionFailureCode.ProviderError)]
    [InlineData("{ ok: false, code: 'notfound' }", ActionFailureCode.NotFound)]
    [InlineData("{ ok: false, code: 'TIMEOUT' }", ActionFailureCode.Timeout)]
    [InlineData("{ ok: false, code: 7 }", ActionFailureCode.ProviderError)]
    public async Task The_failure_code_is_matched_by_name_ignoring_case(string returned, ActionFailureCode expected)
    {
        var (_, host, _) = StartAction($"run() {{ return {returned}; }}");

        Assert.Equal(expected, (await Outcome(host)).Code);
    }

    [Fact]
    public async Task A_message_that_is_not_a_string_is_dropped()
    {
        var (_, host, _) = StartAction("run() { return { ok: false, code: 'NotFound', message: 5 }; }");

        Assert.Null((await Outcome(host)).Message);
    }

    [Fact]
    public async Task Accepted_is_read_from_the_return_value()
    {
        var (_, host, _) = StartAction("run() { return { ok: 'accepted', message: 'sent' }; }");

        var outcome = await Outcome(host);

        Assert.Equal(ActionOutcomeKind.Accepted, outcome.Kind);
        Assert.Equal("sent", outcome.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("return true;")]
    [InlineData("return { ok: true };")]
    [InlineData("return 5;")]
    [InlineData("return { done: 1 };")]
    [InlineData("return 'ok';")]
    public async Task Any_other_return_value_is_success(string body)
    {
        var (_, host, _) = StartAction($"run() {{ {body} }}");

        Assert.Equal(ActionOutcomeKind.Success, (await Outcome(host)).Kind);
    }

    [Fact]
    public async Task A_reported_failure_is_no_script_fault()
    {
        var (_, host, problems) = StartAction("run() { return { ok: false, code: 'NotFound' }; }");

        for (var i = 0; i < 6; i++) await Outcome(host);

        Assert.Empty(problems.Snapshot());
        Assert.Empty(_faults);
    }

    [Fact]
    public async Task An_outcome_action_waits_for_its_promise()
    {
        var (_, host, _) = StartAction("outcome: true, run: async () => { await Promise.resolve(); return { ok: false, code: 'Timeout', message: 'slow' }; }");

        var outcome = await Outcome(host);

        Assert.Equal(ActionFailureCode.Timeout, outcome.Code);
        Assert.Equal("slow", outcome.Message);
    }

    [Fact]
    public async Task An_outcome_action_that_resolves_without_a_shape_is_success()
    {
        var (_, host, _) = StartAction("outcome: true, run: async () => 1");

        Assert.Equal(ActionOutcomeKind.Success, (await Outcome(host)).Kind);
    }

    [Fact]
    public async Task An_outcome_action_that_rejects_is_a_provider_error_and_no_script_fault()
    {
        var (_, host, problems) = StartAction("outcome: true, run: async () => { throw new Error('closed'); }");

        for (var i = 0; i < 6; i++)
        {
            var outcome = await Outcome(host);
            Assert.Equal(ActionFailureCode.ProviderError, outcome.Code);
            Assert.Equal("closed", outcome.Message);
        }

        Assert.Empty(problems.Snapshot());
        Assert.Empty(_faults);
    }

    [Fact]
    public async Task A_promise_that_never_settles_times_out_and_leaves_no_wait_behind()
    {
        var limits = Tight with { ActionOutcomeTimeout = TimeSpan.FromMilliseconds(200) };
        var (plugin, host, _) = StartAction("outcome: true, run: () => new Promise(() => {})", limits);

        Assert.Equal(ActionFailureCode.Timeout, (await Outcome(host)).Code);

        Assert.Equal(0, plugin.PendingOutcomeWaits);
    }

    [Fact]
    public async Task Without_the_outcome_flag_a_rejected_async_action_stays_silent()
    {
        var (_, host, problems) = StartAction("run: async () => { throw new Error('closed'); }");

        Assert.Equal(ActionOutcomeKind.Success, (await Outcome(host)).Kind);
        Assert.Empty(problems.Snapshot());
    }

    [Fact]
    public async Task More_than_sixteen_waiting_runs_are_refused_and_dispose_does_not_hang()
    {
        var limits = Tight with { ActionOutcomeTimeout = TimeSpan.FromSeconds(30) };
        var (plugin, host, _) = StartAction("outcome: true, run: () => new Promise(() => {})", limits);

        var waiting = Enumerable.Range(0, 16).Select(_ => Outcome(host)).ToList();
        for (var i = 0; i < 100 && plugin.PendingOutcomeWaits < 16; i++) await Task.Delay(50);
        Assert.Equal(16, plugin.PendingOutcomeWaits);

        Assert.Equal(ActionFailureCode.Unavailable, (await Outcome(host)).Code);

        plugin.Dispose();
        var results = await Task.WhenAll(waiting).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(results, r => Assert.Equal(ActionFailureCode.Unavailable, r.Code));
    }
}
