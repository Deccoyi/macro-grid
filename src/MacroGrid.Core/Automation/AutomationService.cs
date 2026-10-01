using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Automation;

/// <summary>What the editor shows about one rule. Kept in memory only.</summary>
public sealed record AutomationRuleStatus(bool Running, DateTimeOffset? LastStart, string LastResult, string? LastMessage, int Runs);

/// <summary>Why a manual run did not start.</summary>
public enum AutomationRunResult { Started, NotFound, Refused }

/// <summary>
/// Runs the automation rules (docs/design/automation-rules.md). Event handlers only record what happened; every decision is made on a
/// 250 ms tick, which a test can also call by hand. A rule never runs without these limits: no overlap with itself, a cooldown, a bucket
/// of its own and one for all rules, and at most four lists at once.
/// </summary>
public sealed class AutomationService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MinCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GuardWindow = TimeSpan.FromSeconds(60);
    private const int GuardRefusals = 10;
    private const int MaxParallel = 4;
    private const string Source = "automation";

    /// <summary>Steps that press keys by themselves. A rule runs without a touch, so these are refused there until the owner decides otherwise.</summary>
    private static readonly HashSet<string> KeyboardSteps = new(StringComparer.OrdinalIgnoreCase) { HotkeyAction.TypeId, TypeTextAction.TypeId };

    private sealed class RuleState(string signature)
    {
        public string Signature { get; set; } = signature;
        public bool WasTrue;
        public DateTime LastMinute;
        public bool Running;
        public DateTimeOffset? LastStart;
        public string LastResult = "none";
        public string? LastMessage;
        public int Runs;
        public readonly TokenBucket Bucket = new(5, 0.5);
        public readonly Queue<DateTimeOffset> Refusals = new();
        public HashSet<string> Variables = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly AutomationStore _store;
    private readonly VariableStore _variables;
    private readonly SessionRegistry _sessions;
    private readonly ActionDispatcher _dispatcher;
    private readonly ProblemList _problems;
    private readonly ProfileStore _profiles;
    private readonly WidgetStateService _widgetState;
    private readonly ILogger<AutomationService> _logger;
    private readonly TimeProvider _clock;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, RuleState> _state = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly TokenBucket _global = new(20, 5);
    private readonly SemaphoreSlim _slots = new(MaxParallel);
    private readonly ConcurrentDictionary<Task, byte> _active = new();
    private HashSet<string> _devices = [];
    private bool _devicesKnown;
    private bool _rulesChanged = true;
    private bool _sessionsChanged = true;
    private CancellationToken _stopping;

    public AutomationService(
        AutomationStore store, VariableStore variables, SessionRegistry sessions, ActionDispatcher dispatcher, ProblemList problems,
        ProfileStore profiles, WidgetStateService widgetState, ILogger<AutomationService> logger, TimeProvider? clock = null)
    {
        _store = store;
        _variables = variables;
        _sessions = sessions;
        _dispatcher = dispatcher;
        _problems = problems;
        _profiles = profiles;
        _widgetState = widgetState;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        // Only noted here; the tick decides. Hooked up in the constructor so a test that calls Tick by hand sees the same events.
        _store.Changed += OnRulesChanged;
        _variables.Changed += OnVariableChanged;
        _sessions.Changed += OnSessionsChanged;
    }

    /// <summary>The state of every rule, by id.</summary>
    public IReadOnlyDictionary<string, AutomationRuleStatus> Status()
    {
        lock (_lock)
            return _state.ToDictionary(p => p.Key, p => new AutomationRuleStatus(p.Value.Running, p.Value.LastStart, p.Value.LastResult, p.Value.LastMessage, p.Value.Runs));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping = stoppingToken;
        try
        {
            using var timer = new PeriodicTimer(TickInterval, _clock);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { Tick(); }
                catch (Exception ex) { _logger.LogError(ex, "Automation tick failed"); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override void Dispose()
    {
        _store.Changed -= OnRulesChanged;
        _variables.Changed -= OnVariableChanged;
        _sessions.Changed -= OnSessionsChanged;
        base.Dispose();
    }

    private void OnRulesChanged() { lock (_lock) _rulesChanged = true; }
    private void OnSessionsChanged() { lock (_lock) _sessionsChanged = true; }
    private void OnVariableChanged(string name) { lock (_lock) _dirtyVariables.Add(name); }

    /// <summary>One decision pass. Called every 250 ms by the service; a test calls it by hand with a clock it moves.</summary>
    public void Tick()
    {
        var now = _clock.GetUtcNow();
        var rules = _store.List();
        var paused = _store.Paused;
        List<(AutomationRule Rule, string? DeviceId)> due = [];

        lock (_lock)
        {
            if (_rulesChanged)
            {
                _rulesChanged = false;
                Rebuild(rules);
            }
            var dirty = _dirtyVariables.ToList();
            _dirtyVariables.Clear();
            var sessionsChanged = _sessionsChanged;
            _sessionsChanged = false;

            var local = _clock.GetLocalNow();
            var minute = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0);
            HashSet<string>? arrived = null;
            if (sessionsChanged)
            {
                var current = CurrentDevices();
                arrived = [.. current.Except(_devices)];
                _devices = current;
            }

            foreach (var rule in rules)
            {
                if (!_state.TryGetValue(rule.Id, out var state)) continue;
                switch (rule.Trigger.Kind)
                {
                    case AutomationTriggerKinds.Variable when rule.Trigger.Condition is { } condition && dirty.Any(state.Variables.Contains):
                        var matches = DynamicRuleEvaluator.Matches(condition, _variables);
                        if (matches && !state.WasTrue) due.Add((rule, null));
                        state.WasTrue = matches;
                        break;
                    case AutomationTriggerKinds.Time when state.LastMinute != minute && TimeMatches(rule.Trigger, local):
                        state.LastMinute = minute;
                        due.Add((rule, null));
                        break;
                    case AutomationTriggerKinds.DeviceConnect when arrived is { Count: > 0 }:
                        foreach (var id in arrived.Where(id => rule.Trigger.DeviceId is null || rule.Trigger.DeviceId == id))
                            due.Add((rule, id));
                        break;
                }
            }

            foreach (var (rule, deviceId) in due)
                TryStart(rule, _state[rule.Id], deviceId, paused, manual: false, now);
        }
    }

    /// <summary>Runs a rule now, as the "Run now" button of the editor does. It ignores the switch, the pause and the cooldown, not the overlap or the buckets.</summary>
    public AutomationRunResult RunNow(string id, out string message)
    {
        message = "";
        var rule = _store.List().FirstOrDefault(r => r.Id == id);
        if (rule is null) return AutomationRunResult.NotFound;
        string? deviceId = null;
        if (rule.Trigger.Kind == AutomationTriggerKinds.DeviceConnect)
        {
            deviceId = CurrentDevices().FirstOrDefault(d => rule.Trigger.DeviceId is null || rule.Trigger.DeviceId == d);
            if (deviceId is null) { message = "No device is connected."; return AutomationRunResult.Refused; }
        }
        lock (_lock)
        {
            if (_rulesChanged) { _rulesChanged = false; Rebuild(_store.List()); }
            if (!_state.TryGetValue(id, out var state)) return AutomationRunResult.NotFound;
            if (!TryStart(rule, state, deviceId, paused: false, manual: true, _clock.GetUtcNow())) { message = "The rule is still running or was started too often."; return AutomationRunResult.Refused; }
        }
        return AutomationRunResult.Started;
    }

    /// <summary>Completes when no rule is running or waiting. For tests and for stopping.</summary>
    public async Task IdleAsync()
    {
        while (true)
        {
            var tasks = _active.Keys.ToArray();
            if (tasks.Length == 0) return;
            await Task.WhenAll(tasks);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try { await IdleAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) { }
    }

    /// <summary>Called under the lock. Gives every rule its state again when its content changed, and forgets the rules that are gone.</summary>
    private void Rebuild(IReadOnlyList<AutomationRule> rules)
    {
        var current = _clock.GetLocalNow();
        var minute = new DateTime(current.Year, current.Month, current.Day, current.Hour, current.Minute, 0);
        var ids = rules.Select(r => r.Id).ToHashSet();
        foreach (var gone in _state.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _state.Remove(gone);
            ResolveAll(gone);
        }
        foreach (var rule in rules)
        {
            var signature = JsonSerializer.Serialize(rule, ProtocolJson.Options);
            if (_state.TryGetValue(rule.Id, out var existing) && existing.Signature == signature) continue;
            // The state object stays (a run in progress holds it); only what the trigger needs is read again.
            var state = existing ?? new RuleState(signature);
            state.Signature = signature;
            state.LastMinute = minute;
            state.Variables = [];
            state.WasTrue = false;
            if (rule.Trigger.Condition is { } condition)
            {
                // The condition is read now without firing: a rule does not run because it was saved while its condition holds.
                state.Variables = [.. DynamicRuleEvaluator.CollectVariables(condition)];
                state.WasTrue = DynamicRuleEvaluator.Matches(condition, _variables);
            }
            _state[rule.Id] = state;
        }
        if (!_devicesKnown) { _devices = CurrentDevices(); _devicesKnown = true; }
    }

    private HashSet<string> CurrentDevices() => [.. _sessions.All.Select(s => s.DeviceId).OfType<string>()];

    private static bool TimeMatches(AutomationTrigger trigger, DateTimeOffset local) =>
        trigger.Time == $"{local.Hour:00}:{local.Minute:00}" && (trigger.Days.Count == 0 || trigger.Days.Contains((int)local.DayOfWeek));

    /// <summary>Called under the lock. The order of the checks is the order in the design document.</summary>
    private bool TryStart(AutomationRule rule, RuleState state, string? deviceId, bool paused, bool manual, DateTimeOffset now)
    {
        if (!manual && (paused || !rule.Enabled)) return false;
        if (state.Running) return false;
        if (!manual && state.LastStart is { } last && now - last < Max(MinCooldown, TimeSpan.FromSeconds(rule.CooldownSeconds))) return false;

        if (!state.Bucket.Take(now))
        {
            Refuse(rule, state, now, guard: true);
            return false;
        }
        if (!_global.Take(now))
        {
            Refuse(rule, state, now, guard: false);
            return false;
        }

        state.Running = true;
        state.LastStart = now;
        state.Runs++;
        var session = deviceId is null ? null : _sessions.All.FirstOrDefault(s => s.DeviceId == deviceId);
        var run = Task.Run(() => RunAsync(rule, state, deviceId, session));
        _active[run] = 0;
        _ = run.ContinueWith(t => _active.TryRemove(t, out _), TaskScheduler.Default);
        return true;
    }

    private void Refuse(AutomationRule rule, RuleState state, DateTimeOffset now, bool guard)
    {
        state.LastResult = "refused";
        _problems.Report(Source, rule.Name, ProblemSeverity.Warning, ProblemCodes.AutomationDropped,
            "This rule was started too often, so some starts were dropped.", $"{ProblemCodes.AutomationDropped}:{rule.Id}", int.MaxValue);
        if (!guard) return;
        state.Refusals.Enqueue(now);
        while (state.Refusals.Count > 0 && now - state.Refusals.Peek() > GuardWindow) state.Refusals.Dequeue();
        if (state.Refusals.Count < GuardRefusals) return;

        state.Refusals.Clear();
        if (_store.Disable(rule.Id))
            _problems.Report(Source, rule.Name, ProblemSeverity.Error, ProblemCodes.AutomationSwitchedOff,
                "This rule kept starting too often and was switched off. Check what starts it, then switch it on again.", $"{ProblemCodes.AutomationSwitchedOff}:{rule.Id}", int.MaxValue);
    }

    private async Task RunAsync(AutomationRule rule, RuleState state, string? deviceId, ClientSession? session)
    {
        var watch = Stopwatch.StartNew();
        var failures = new List<ActionFailure>();
        try
        {
            await _slots.WaitAsync(_stopping);
            try
            {
                if (rule.Actions.FirstOrDefault(a => KeyboardSteps.Contains(a.Type)) is { } keyStep)
                {
                    failures.Add(new ActionFailure(keyStep.Type, ActionFailureCode.ProviderError,
                        "A rule cannot press keys or type text by itself. Remove this step from the rule."));
                }
                else
                {
                    IDeviceController device = session is null ? new NoDeviceController() : new SessionDeviceController(session, _profiles, _widgetState);
                    var context = new ActionContext(deviceId ?? "", "", "", device) { UserGesture = false };
                    failures.AddRange(await _dispatcher.RunListAsync(rule.Actions, context, "rule:" + rule.Id, _stopping));
                }
            }
            finally { _slots.Release(); }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            failures.Add(new ActionFailure("", ActionFailureCode.ProviderError, "The server was stopping."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation rule {Rule} failed", rule.Id);
            failures.Add(new ActionFailure("", ActionFailureCode.ProviderError, ex.Message));
        }

        lock (_lock)
        {
            state.Running = false;
            state.LastResult = failures.Count == 0 ? "ok" : "failed";
            state.LastMessage = failures.Count == 0 ? null : PlainText.Clean(failures[0].Message, 200);
            if (failures.Count == 0)
                _problems.ResolveKey(Source, $"{ProblemCodes.AutomationStepFailed}:{rule.Id}");
            else
                _problems.Report(Source, rule.Name, ProblemSeverity.Warning, ProblemCodes.AutomationStepFailed,
                    failures[0].Message, $"{ProblemCodes.AutomationStepFailed}:{rule.Id}", int.MaxValue);
        }
        _logger.LogInformation("Automation rule {Rule} ({Kind}) ran in {Ms} ms with {Failures} failures", rule.Id, rule.Trigger.Kind, watch.ElapsedMilliseconds, failures.Count);
    }

    private void ResolveAll(string id)
    {
        foreach (var code in new[] { ProblemCodes.AutomationStepFailed, ProblemCodes.AutomationDropped, ProblemCodes.AutomationSwitchedOff })
            _problems.ResolveKey(Source, $"{code}:{id}");
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
