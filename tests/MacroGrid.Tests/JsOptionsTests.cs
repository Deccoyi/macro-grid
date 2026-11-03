using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Tests;

public sealed class JsOptionsTests : IDisposable
{
    private const string Canary = "CanaryLabelNeverLogged";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-jsopt-" + Guid.NewGuid().ToString("N"));
    private readonly ProblemList _problems = new();
    private readonly List<JsPlugin> _plugins = [];
    private readonly List<string> _faults = [];
    private readonly Capture _log = new();

    public JsOptionsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var plugin in _plugins) plugin.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Capture : ILogger
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + exception?.Message);
    }

    private PluginHostCollector Start(string script, JsPluginLimits? limits = null)
    {
        var path = Path.Combine(_dir, "index.js");
        File.WriteAllText(path, script);
        var manifest = new PluginManifest { Id = "t", Name = "T", Version = "1.0.0", MinMacroGrid = "1.0.0", Entry = "index.js", Kind = PluginKind.Js };
        var plugin = new JsPlugin(manifest, path, new JsPermissions(["actions"]), new VariableStore(), null, _log, _faults.Add,
            limits ?? JsPluginLimits.Default with { CallTimeout = TimeSpan.FromSeconds(1) }, problems: _problems);
        _plugins.Add(plugin);
        var host = new PluginHostCollector("0.1.0", _dir, "t", new PluginStatusRegistry(), _log);
        plugin.Initialize(host);
        return host;
    }

    private static IOptionsSearch Source(PluginHostCollector host) => (IOptionsSearch)host.Actions.Single();

    private const string Action = "host.registerAction({ type: 't.go', name: 'Go', run: () => {}, fields: [{ key: 'v', label: 'V', kind: 'Select', optionsSource: 'items', searchable: true, dependsOn: ['mode'] }] });\n";

    private static Task<OptionsResult> Ask(PluginHostCollector host, string text = "", int offset = 0, int limit = 20, string? selected = null, string id = "items") =>
        OptionsPaging.RunAsync(Source(host), id, new JsonObject { ["mode"] = "m" }, new OptionsQuery(text, offset, limit, selected), CancellationToken.None);

    [Fact]
    public async Task A_source_answers_with_options_paging_and_groups()
    {
        var host = Start(Action + """
            host.options.source('items', (q, values) => {
              const all = Array.from({ length: 45 }, (_, i) => ({ value: 'v' + i, label: 'Item ' + i, group: values.mode }));
              const hits = all.filter(o => o.label.toLowerCase().includes(q.text.toLowerCase()));
              return { options: hits.slice(q.offset, q.offset + q.limit), more: hits.length > q.offset + q.limit };
            });
            """);

        var field = ((IActionDescriptor)host.Actions.Single()).Fields.Single();
        Assert.True(field.Searchable);
        Assert.Equal("items", field.OptionsSource);
        Assert.Equal("mode", Assert.Single(field.DependsOn!));

        var first = await Ask(host);
        Assert.Equal(20, first.Options.Count);
        Assert.True(first.HasMore);
        Assert.Equal("m", first.Options[0].Group);
        var rest = await Ask(host, offset: 40);
        Assert.Equal(5, rest.Options.Count);
        Assert.False(rest.HasMore);
        Assert.Equal("v7", Assert.Single((await Ask(host, "item 7")).Options).Value);
    }

    [Fact]
    public async Task The_selected_value_and_its_absence_come_from_the_script()
    {
        var host = Start(Action + """
            host.options.source('items', (q) => {
              const options = [{ value: 'a', label: 'Alpha' }];
              if (q.selected === 'a') return { options, selectedMissing: false };
              return { options, selectedMissing: q.selected ? true : undefined };
            });
            """);
        Assert.False((await Ask(host, selected: "a")).SelectedMissing);
        Assert.True((await Ask(host, selected: "zzz")).SelectedMissing);
    }

    [Fact]
    public async Task An_unknown_source_a_throw_and_a_promise_give_an_error_not_an_exception()
    {
        var host = Start(Action + """
            host.options.source('items', () => { throw new Error('Voice app is not running'); });
            host.options.source('later', async () => ({ options: [] }));
            """);

        Assert.Equal("Voice app is not running", (await Ask(host)).Error);
        Assert.NotNull((await Ask(host, id: "nope")).Error);
        Assert.NotNull((await Ask(host, id: "later")).Error);
    }

    [Fact]
    public async Task A_source_that_runs_too_long_gives_an_error_and_does_not_fault_the_plugin()
    {
        var host = Start(Action + "host.options.source('items', () => { while (true) {} });",
            JsPluginLimits.Default with { CallTimeout = TimeSpan.FromMilliseconds(200) });

        for (var i = 0; i < 7; i++) Assert.NotNull((await Ask(host)).Error);
        Assert.Empty(_faults);
        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public async Task A_data_url_icon_is_dropped_and_references_are_kept_only_for_this_plugin()
    {
        var host = Start(Action + """
            host.options.source('items', () => ({ options: [
              { value: '1', label: 'a', icon: 'data:image/png;base64,AAAA' },
              { value: '2', label: 'b', icon: 'plugin-image:t/cat.1' },
              { value: '3', label: 'c', icon: 'plugin-image:other/cat' },
              { value: '4', label: 'd', icon: 'mic' },
              { value: '5', label: 'e', icon: 'http://x/y.png' },
            ] }));
            """);

        var icons = (await Ask(host)).Options.Select(o => o.Icon).ToArray();
        Assert.Equal(new string?[] { null, "plugin-image:t/cat.1", null, "mic", null }, icons);
    }

    [Fact]
    public void Static_field_options_lose_a_data_url_icon()
    {
        var host = Start("host.registerAction({ type: 't.go', name: 'Go', run: () => {}, fields: [{ key: 'v', label: 'V', kind: 'Select', options: [{ value: 'a', label: 'A', icon: 'data:image/png;base64,AA' }] }] });");
        Assert.Null(((IActionDescriptor)host.Actions.Single()).Fields.Single().Options![0].Icon);
    }

    [Fact]
    public async Task A_settings_page_field_uses_a_source_too()
    {
        var host = Start("""
            host.settings.page([{ key: 'v', label: 'V', kind: 'Select', optionsSource: 'items', searchable: true }]);
            host.options.source('items', (q) => ({ options: [{ value: 'a', label: 'Alpha' }], more: false }));
            """);
        var result = await OptionsPaging.RunAsync((IOptionsSearch)host.SettingsPage!, "items", [], new OptionsQuery("", 0, 10, null), CancellationToken.None);
        Assert.Equal("a", Assert.Single(result.Options).Value);
    }

    [Fact]
    public async Task Labels_and_error_texts_are_never_logged()
    {
        var host = Start(Action + $$"""
            host.options.source('items', (q) => {
              if (q.text === 'boom') throw new Error('{{Canary}} failed');
              return { options: [{ value: 'a', label: '{{Canary}}' }] };
            });
            """);

        Assert.Single((await Ask(host)).Options);
        Assert.Contains(Canary, (await Ask(host, "boom")).Error);
        // The endpoint itself has no logger, so the script path above is the only place that could have logged.
        Assert.DoesNotContain(_log.Messages, m => m.Contains(Canary));
        Assert.DoesNotContain(_problems.Snapshot(), p => p.Message.Contains(Canary));
    }
}
