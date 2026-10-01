using System.Net.WebSockets;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class AutomationServiceTests : IDisposable
{
    /// <summary>A clock the test moves by hand (UTC is the local time here).</summary>
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class QuietSocket : WebSocket
    {
        public override WebSocketState State => WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    /// <summary>Records every run; a step with "fail" set throws, a step with "wait" waits for <see cref="Gate"/>.</summary>
    private sealed class Probe(string type) : IActionHandler
    {
        public string Type => type;
        public string DisplayName => type;
        public List<ActionContext> Runs { get; } = [];
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            lock (Runs) Runs.Add(context);
            if (settings["wait"] is not null) await Gate.Task;
            if (settings["fail"] is not null) throw new InvalidOperationException("boom");
        }
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-autosvc-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTime _clock = new(new DateTimeOffset(2026, 10, 7, 7, 29, 0, TimeSpan.Zero)); // a Wednesday
    private readonly VariableStore _variables = new();
    private readonly SessionRegistry _sessions = new();
    private readonly ProblemList _problems = new();
    private readonly AutomationStore _store;
    private readonly Probe _ok = new("t.ok");
    private readonly Probe _keys = new("core.hotkey");
    private readonly AutomationService _service;

    public AutomationServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new AutomationStore(_dir);
        var dispatcher = new ActionDispatcher([_ok, _keys], NullLogger<ActionDispatcher>.Instance, _variables);
        var profiles = new ProfileStore(_dir);
        var widgetState = new WidgetStateService(_variables, _sessions, profiles, new ToggleStateStore(), new LayoutSender(new AssetStore()), new WebViewState(),
            NullLogger<WidgetStateService>.Instance);
        _service = new AutomationService(_store, _variables, _sessions, dispatcher, _problems, profiles, widgetState, NullLogger<AutomationService>.Instance, _clock);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ActionBinding Step(string type = "t.ok", JsonObject? settings = null) => new(type, settings ?? new JsonObject());

    private static AutomationRule When(string id, string variable, string op, string value, params ActionBinding[] steps) => new()
    {
        Id = id,
        Name = "Rule " + id,
        Trigger = new AutomationTrigger { Kind = AutomationTriggerKinds.Variable, Condition = new ConditionNode { Variable = variable, Operator = op, Value = value } },
        Actions = steps.Length == 0 ? [Step()] : [.. steps],
    };

    private static AutomationRule At(string id, string time, params int[] days) => new()
    {
        Id = id,
        Name = "Rule " + id,
        Trigger = new AutomationTrigger { Kind = AutomationTriggerKinds.Time, Time = time, Days = [.. days] },
        Actions = [Step()],
    };

    private static AutomationRule OnDevice(string id, string? deviceId = null) => new()
    {
        Id = id,
        Name = "Rule " + id,
        Trigger = new AutomationTrigger { Kind = AutomationTriggerKinds.DeviceConnect, DeviceId = deviceId },
        Actions = [Step()],
    };

    private void Save(bool paused = false, params AutomationRule[] rules) => Assert.True(_store.TryReplace(rules, paused, out var error), error);

    /// <summary>One tick, then wait for the runs it started.</summary>
    private async Task TickAsync()
    {
        _service.Tick();
        await _service.IdleAsync();
    }

    private async Task NextSecondAsync(int seconds = 2)
    {
        _clock.Advance(TimeSpan.FromSeconds(seconds));
        await TickAsync();
    }

    [Fact]
    public async Task A_variable_rule_fires_only_when_its_condition_turns_true()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1"));
        await TickAsync();

        _variables.Set("user.x", 5);
        await NextSecondAsync();
        Assert.Single(_ok.Runs);

        _variables.Set("user.x", 6);
        await NextSecondAsync();
        Assert.Single(_ok.Runs);

        _variables.Set("user.x", 0);
        await NextSecondAsync();
        _variables.Set("user.x", 7);
        await NextSecondAsync();
        Assert.Equal(2, _ok.Runs.Count);
    }

    [Fact]
    public async Task A_rule_saved_while_its_condition_holds_does_not_run_for_that()
    {
        _variables.Set("user.x", 5);
        Save(false, When("a", "user.x", ">", "1"));
        await TickAsync();
        await NextSecondAsync();

        Assert.Empty(_ok.Runs);

        _variables.Set("user.x", 6);
        await NextSecondAsync();
        Assert.Empty(_ok.Runs);
    }

    [Fact]
    public async Task A_time_rule_fires_once_in_its_minute_on_its_days_only()
    {
        Save(false, At("a", "07:30", 3), At("b", "07:30", 1, 2));
        await TickAsync();

        await NextSecondAsync(30);
        Assert.Empty(_ok.Runs);

        await NextSecondAsync(31);
        Assert.Single(_ok.Runs);

        await NextSecondAsync(10);
        await NextSecondAsync(10);
        Assert.Single(_ok.Runs);
    }

    [Fact]
    public async Task A_device_rule_fires_when_a_matching_device_connects_and_the_run_has_that_device()
    {
        Save(false, OnDevice("any"), OnDevice("other", "tablet"));
        await TickAsync();

        _sessions.Add(new ClientSession(new QuietSocket()) { DeviceId = "phone" });
        await NextSecondAsync();

        var run = Assert.Single(_ok.Runs);
        Assert.Equal("phone", run.DeviceId);
        Assert.False(run.UserGesture);
        Assert.IsNotType<NoDeviceController>(run.Device);
    }

    [Fact]
    public async Task A_device_that_was_already_connected_when_the_rule_started_does_not_fire_it()
    {
        _sessions.Add(new ClientSession(new QuietSocket()) { DeviceId = "phone" });
        Save(false, OnDevice("any"));
        await TickAsync();
        await NextSecondAsync();

        Assert.Empty(_ok.Runs);
    }

    [Fact]
    public async Task A_rule_without_a_device_runs_with_no_gesture_and_a_controller_that_refuses()
    {
        Save(false, At("a", "07:30"));
        await TickAsync();
        await NextSecondAsync(61);

        var run = Assert.Single(_ok.Runs);
        Assert.False(run.UserGesture);
        Assert.Equal("", run.DeviceId);
        Assert.IsType<NoDeviceController>(run.Device);
    }

    [Fact]
    public async Task A_paused_list_and_a_switched_off_rule_do_not_fire()
    {
        _variables.Set("user.x", 0);
        var off = When("off", "user.x", ">", "1");
        off.Enabled = false;
        Save(true, When("on", "user.x", ">", "1"), off);
        await TickAsync();

        _variables.Set("user.x", 5);
        await NextSecondAsync();

        Assert.Empty(_ok.Runs);
    }

    [Fact]
    public async Task A_rule_does_not_start_again_while_it_runs()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1", Step(settings: new JsonObject { ["wait"] = true })));
        await TickAsync();

        _variables.Set("user.x", 5);
        _service.Tick();
        _variables.Set("user.x", 0);
        _clock.Advance(TimeSpan.FromSeconds(2));
        _service.Tick();
        _variables.Set("user.x", 5);
        _clock.Advance(TimeSpan.FromSeconds(2));
        _service.Tick();

        Assert.True(_service.Status()["a"].Running);
        Assert.Single(_ok.Runs);

        _ok.Gate.SetResult();
        await _service.IdleAsync();
        Assert.Equal("ok", _service.Status()["a"].LastResult);
    }

    [Fact]
    public async Task The_cooldown_holds_a_rule_back()
    {
        _variables.Set("user.x", 0);
        var rule = When("a", "user.x", ">", "1");
        rule.CooldownSeconds = 60;
        Save(false, rule);
        await TickAsync();

        _variables.Set("user.x", 5);
        await NextSecondAsync();
        _variables.Set("user.x", 0);
        await NextSecondAsync(10);
        _variables.Set("user.x", 5);
        await NextSecondAsync(10);
        Assert.Single(_ok.Runs);

        _variables.Set("user.x", 0);
        await NextSecondAsync(60);
        _variables.Set("user.x", 5);
        await NextSecondAsync(1);
        Assert.Equal(2, _ok.Runs.Count);
    }

    [Fact]
    public async Task A_rule_that_keeps_starting_is_switched_off_and_the_Error_List_says_so()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1"));
        await TickAsync();

        for (var i = 0; i < 40 && _store.List()[0].Enabled; i++)
        {
            _variables.Set("user.x", 5);
            await NextSecondAsync(1);
            _variables.Set("user.x", 0);
            await NextSecondAsync(0);
        }

        Assert.False(_store.List()[0].Enabled);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationSwitchedOff && p.Key == "P162:a");
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationDropped);
    }

    [Fact]
    public async Task A_failing_step_is_reported_and_a_later_good_run_takes_the_line_back()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1", Step(settings: new JsonObject { ["fail"] = true })));
        await TickAsync();
        _variables.Set("user.x", 5);
        await NextSecondAsync();

        Assert.Equal("failed", _service.Status()["a"].LastResult);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationStepFailed && p.Key == "P160:a");

        Save(false, When("a", "user.x", ">", "1"));
        _variables.Set("user.x", 0);
        await NextSecondAsync();
        _variables.Set("user.x", 5);
        await NextSecondAsync();

        Assert.Equal("ok", _service.Status()["a"].LastResult);
        Assert.DoesNotContain(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationStepFailed);
    }

    [Fact]
    public async Task A_step_that_presses_keys_is_refused_inside_a_rule()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1", Step(), Step("core.hotkey")));
        await TickAsync();
        _variables.Set("user.x", 5);
        await NextSecondAsync();

        Assert.Empty(_keys.Runs);
        Assert.Empty(_ok.Runs);
        Assert.Equal("failed", _service.Status()["a"].LastResult);
        Assert.Contains(_problems.Snapshot(), p => p.Code == ProblemCodes.AutomationStepFailed && p.Message.Contains("press keys"));
    }

    [Fact]
    public async Task Run_now_ignores_the_switch_and_the_pause_and_answers_unknown_and_no_device()
    {
        var off = At("a", "23:00");
        off.Enabled = false;
        Save(true, off, OnDevice("d"));

        Assert.Equal(AutomationRunResult.NotFound, _service.RunNow("missing", out _));
        Assert.Equal(AutomationRunResult.Refused, _service.RunNow("d", out var message));
        Assert.Contains("No device", message);

        Assert.Equal(AutomationRunResult.Started, _service.RunNow("a", out _));
        await _service.IdleAsync();
        Assert.Single(_ok.Runs);
    }

    [Fact]
    public async Task Changing_a_rule_while_its_condition_holds_does_not_fire_it()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1"));
        await TickAsync();
        _variables.Set("user.x", 5);
        await NextSecondAsync();
        Assert.Single(_ok.Runs);

        Save(false, When("a", "user.x", ">", "2"));
        await NextSecondAsync();
        _variables.Set("user.x", 6);
        await NextSecondAsync();

        Assert.Single(_ok.Runs);
    }

    [Fact]
    public async Task A_removed_rule_takes_its_problem_lines_with_it()
    {
        _variables.Set("user.x", 0);
        Save(false, When("a", "user.x", ">", "1", Step(settings: new JsonObject { ["fail"] = true })));
        await TickAsync();
        _variables.Set("user.x", 5);
        await NextSecondAsync();
        Assert.NotEmpty(_problems.Snapshot());

        Save(false);
        await NextSecondAsync();

        Assert.Empty(_problems.Snapshot());
        Assert.Empty(_service.Status());
    }
}
