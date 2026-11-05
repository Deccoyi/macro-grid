using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Automation;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>The "variable changes" trigger (docs/design/automation-rules.md).</summary>
public sealed class AutomationChangeTriggerTests : IDisposable
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>Records the resolved "text" setting of every run; a step with "wait" waits for <see cref="Gate"/>; a step with "write" sets that variable to a new number.</summary>
    private sealed class TextProbe(VariableStore store) : IActionHandler, IActionDescriptor
    {
        private int _counter;
        public string Type => "t.text";
        public string DisplayName => "t.text";
        public string Category => "Test";
        public string? Description => null;
        public string? Icon => null;
        public IReadOnlyList<SettingField> Fields { get; } = [new SettingField("text", "Text", SettingFieldKind.Text) { AllowVariables = true }];
        public List<string> Texts { get; } = [];
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            lock (Texts) Texts.Add(settings["text"]?.GetValue<string>() ?? "");
            if (settings["write"]?.GetValue<string>() is { } name) store.Set(name, ++_counter + 1000);
            if (settings["wait"] is not null) await Gate.Task;
        }
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-autochg-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTime _clock = new(new DateTimeOffset(2026, 10, 7, 7, 29, 0, TimeSpan.Zero));
    private readonly VariableStore _variables = new();
    private readonly ProblemList _problems = new();
    private readonly AutomationStore _store;
    private readonly TextProbe _probe;
    private readonly AutomationService _service;

    public AutomationChangeTriggerTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new AutomationStore(_dir);
        _probe = new TextProbe(_variables);
        var dispatcher = new ActionDispatcher([_probe, new IfAction(), new ElseAction(), new EndIfAction(), new StopAction()], NullLogger<ActionDispatcher>.Instance, _variables);
        var sessions = new SessionRegistry();
        var profiles = new ProfileStore(_dir);
        var widgetState = new WidgetStateService(_variables, sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), NullLogger<WidgetStateService>.Instance);
        _service = new AutomationService(_store, _variables, sessions, dispatcher, _problems, profiles, widgetState, NullLogger<AutomationService>.Instance, _clock);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AutomationRule Change(string variable, Action<AutomationTrigger>? shape = null, params ActionBinding[] steps)
    {
        var trigger = new AutomationTrigger { Kind = AutomationTriggerKinds.VariableChange, Variable = variable };
        shape?.Invoke(trigger);
        return new AutomationRule
        {
            Id = "r1", Name = "Rule", Trigger = trigger,
            Actions = steps.Length == 0 ? [Text("{trigger.old}>{trigger.new}")] : [.. steps],
        };
    }

    private static ActionBinding Text(string text, params (string Key, JsonNode Value)[] more)
    {
        var settings = new JsonObject { ["text"] = text };
        foreach (var (key, value) in more) settings[key] = value;
        return new ActionBinding("t.text", settings);
    }

    private void Save(params AutomationRule[] rules) => Assert.True(_store.TryReplace(rules, false, out var error), error);

    private async Task TickAsync(int seconds = 0)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        _service.Tick();
        await _service.IdleAsync();
    }

    /// <summary>Saves the rule, lets the value settle (the first tick reads it, then the settle window passes).</summary>
    private async Task ArmAsync(AutomationRule rule, string variable, object? value)
    {
        _variables.Set(variable, value);
        Save(rule);
        await TickAsync();
        await TickAsync(5);
    }

    [Fact]
    public async Task Saving_the_rule_or_writing_the_same_value_does_not_start_it()
    {
        await ArmAsync(Change("t.v"), "t.v", 1);
        _variables.Set("t.v", 1);
        await TickAsync(5);
        Assert.Empty(_probe.Texts);

        _variables.Set("t.v", 2);
        await TickAsync(5);
        Assert.Equal(["1>2"], _probe.Texts);
    }

    [Fact]
    public async Task A_boolean_flip_honours_the_from_and_to_filters()
    {
        await ArmAsync(Change("t.on", t => { t.From = "false"; t.To = "true"; }), "t.on", false);
        _variables.Set("t.on", true);
        await TickAsync(5);
        _variables.Set("t.on", false);
        await TickAsync(5);
        Assert.Equal(["false>true"], _probe.Texts);
    }

    [Fact]
    public async Task A_number_can_change_or_cross_a_threshold_in_both_directions()
    {
        await ArmAsync(Change("t.n", t => { t.Mode = AutomationChangeModes.Crosses; t.Threshold = 50; }), "t.n", 10);
        _variables.Set("t.n", 20);
        await TickAsync(5);
        Assert.Empty(_probe.Texts);
        _variables.Set("t.n", 60);
        await TickAsync(5);
        _variables.Set("t.n", 70);
        await TickAsync(5);
        _variables.Set("t.n", 40.5);
        await TickAsync(5);
        Assert.Equal(["20>60", "70>40.5"], _probe.Texts);
    }

    [Fact]
    public async Task A_text_can_change_to_a_value_or_contain_a_word()
    {
        await ArmAsync(Change("t.s", t => t.To = "live"), "t.s", "off");
        _variables.Set("t.s", "paused");
        await TickAsync(5);
        _variables.Set("t.s", "LIVE");
        await TickAsync(5);
        Assert.Equal(["paused>LIVE"], _probe.Texts);

        _probe.Texts.Clear();
        await ArmAsync(Change("t.s2", t => { t.Mode = AutomationChangeModes.Contains; t.To = "error"; }), "t.s2", "fine");
        _variables.Set("t.s2", "an Error happened");
        await TickAsync(5);
        _variables.Set("t.s2", "ok again");
        await TickAsync(5);
        Assert.Equal(["fine>an Error happened"], _probe.Texts);
    }

    [Fact]
    public async Task A_value_that_is_back_within_one_tick_is_no_change_and_the_old_value_is_the_one_last_judged()
    {
        await ArmAsync(Change("t.v"), "t.v", true);
        _variables.Set("t.v", false);
        _variables.Set("t.v", true);
        await TickAsync(5);
        Assert.Empty(_probe.Texts);

        _variables.Set("t.v", false);
        _variables.Set("t.v", 3);
        await TickAsync(5);
        Assert.Equal(["true>3"], _probe.Texts);
    }

    [Fact]
    public async Task A_variable_that_appears_or_disappears_does_not_start_the_rule_and_a_new_one_settles_first()
    {
        Save(Change("t.p"));
        await TickAsync();
        _variables.Set("t.p", "placeholder");
        await TickAsync(1);
        _variables.Set("t.p", "real value");
        await TickAsync(1);
        Assert.Empty(_probe.Texts); // inside the three seconds after it appeared

        await TickAsync(5);
        _variables.Set("t.p", "later");
        await TickAsync(1);
        Assert.Equal(["real value>later"], _probe.Texts);

        _variables.Remove("t.p");
        await TickAsync(5);
        _variables.Set("t.p", "back"); // the plugin reloaded
        await TickAsync(10);
        Assert.Single(_probe.Texts);
    }

    [Fact]
    public async Task The_minimum_interval_holds_back_starts_and_still_follows_the_value()
    {
        await ArmAsync(Change("t.v", t => t.MinIntervalSeconds = 30), "t.v", 1);
        _variables.Set("t.v", 2);
        await TickAsync(2);
        _variables.Set("t.v", 3);
        await TickAsync(5);
        Assert.Equal(["1>2"], _probe.Texts);

        await TickAsync(40);
        _variables.Set("t.v", 4);
        await TickAsync(2);
        Assert.Equal(["1>2", "3>4"], _probe.Texts);
    }

    [Fact]
    public async Task A_rule_that_changes_its_own_variable_does_not_start_itself_and_is_reported()
    {
        await ArmAsync(Change("t.v", null, Text("x", ("write", "t.v"))), "t.v", 1);
        _variables.Set("t.v", 2);
        await TickAsync(2);
        await TickAsync(5);
        Assert.Single(_probe.Texts);
        Assert.DoesNotContain(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationSelfChange);

        // A long run: its own writes arrive while it is still running.
        await ArmAsync(Change("t.w", null, Text("x", ("wait", true)), Text("y")), "t.w", 1);
        _probe.Texts.Clear();
        _variables.Set("t.w", 2);
        _service.Tick();
        for (var i = 0; i < 6; i++)
        {
            _variables.Set("t.w", 100 + i);
            await Task.Delay(1);
            _service.Tick();
        }
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationSelfChange);
        _probe.Gate.SetResult();
        await _service.IdleAsync();
        await TickAsync(5);
        Assert.Single(_probe.Texts, t => t == "x");
    }

    [Fact]
    public async Task The_trigger_tokens_are_cleaned_capped_and_usable_in_if_steps()
    {
        var condition = new ConditionNode { Variable = "trigger.new", Operator = "==", Value = "go" };
        var ifStep = new ActionBinding("core.if", new JsonObject
        {
            ["when"] = "condition",
            ["condition"] = JsonSerializer.SerializeToNode(condition, ProtocolJson.Options),
        });
        await ArmAsync(Change("t.v", null, ifStep, Text("yes"), new ActionBinding("core.else", new JsonObject()), Text("no"), new ActionBinding("core.endIf", new JsonObject()),
            Text("{trigger.new}")), "t.v", "a");
        _variables.Set("t.v", "go");
        await TickAsync(2);
        Assert.Equal(["yes", "go"], _probe.Texts);

        _probe.Texts.Clear();
        var long_ = "x\u0007y" + new string('z', 400);
        _variables.Set("t.v", long_);
        await TickAsync(5);
        Assert.Equal(["no", "xy" + new string('z', ChangeTrigger.MaxTokenLength - 2)], _probe.Texts);
    }

    [Fact]
    public void Rules_that_cannot_work_are_refused_with_a_clear_message()
    {
        bool Refused(Action<AutomationTrigger> shape, string expected)
        {
            var ok = _store.TryReplace([Change("t.v", shape)], false, out var error);
            return !ok && error.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(Refused(t => t.Variable = "self.toggled", "one button"));
        Assert.True(Refused(t => t.Variable = " ", "variable"));
        Assert.True(Refused(t => t.Mode = AutomationChangeModes.Crosses, "number"));
        Assert.True(Refused(t => t.Mode = AutomationChangeModes.Contains, "text"));
        Assert.True(Refused(t => t.Mode = "nonsense", "mode"));
        Assert.True(Refused(t => t.MinIntervalSeconds = 99999, "interval"));
    }

    [Fact]
    public void An_older_file_loads_and_fields_of_a_newer_build_survive_a_round_trip()
    {
        File.WriteAllText(Path.Combine(_dir, "automation.json"),
            """{"formatVersion":1,"paused":false,"rules":[{"id":"old","name":"Old","enabled":true,"trigger":{"kind":"time","time":"08:00","days":[],"future":{"a":1}},"actions":[],"cooldownSeconds":0,"later":true}]}""");
        var store = new AutomationStore(_dir);
        var rule = Assert.Single(store.List());
        Assert.Equal(AutomationTriggerKinds.Time, rule.Trigger.Kind);

        Assert.True(store.TryReplace(store.List(), false, out var error), error);
        var text = File.ReadAllText(Path.Combine(_dir, "automation.json"));
        Assert.Contains("\"later\"", text);
        Assert.Contains("\"future\"", text);
    }
}
