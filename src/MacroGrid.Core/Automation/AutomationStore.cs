using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Automation;

/// <summary>The automation rules and the master pause, kept in <c>automation.json</c> in the data folder.</summary>
public sealed partial class AutomationStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };

    private sealed record RulesFile(int FormatVersion, bool Paused, List<AutomationRule> Rules);

    private readonly string _path;
    private readonly Lock _lock = new();
    private List<AutomationRule> _rules = [];
    private bool _paused;

    public AutomationStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "automation.json");
        Load();
    }

    /// <summary>Raised after the list or the pause changed.</summary>
    public event Action? Changed;

    public bool Paused { get { lock (_lock) return _paused; } }

    /// <summary>A copy of the rules in the order the person arranged them.</summary>
    public IReadOnlyList<AutomationRule> List()
    {
        lock (_lock) return Clone(_rules);
    }

    /// <summary>Replaces the whole list. Nothing changes when any part is invalid.</summary>
    public bool TryReplace(IReadOnlyList<AutomationRule> rules, bool paused, out string error)
    {
        if (!TryNormalize(rules, out var normalized, out error)) return false;
        var file = new RulesFile(1, paused, normalized);
        if (JsonSerializer.SerializeToUtf8Bytes(file, FileJson).Length > AutomationLimits.MaxFileBytes)
        {
            error = "The rules are too large.";
            return false;
        }
        lock (_lock)
        {
            _rules = normalized;
            _paused = paused;
            Write(file);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Switches one rule off (the loop guard); false when the rule is unknown or already off.</summary>
    public bool Disable(string id)
    {
        lock (_lock)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == id);
            if (rule is null || !rule.Enabled) return false;
            rule.Enabled = false;
            Write(new RulesFile(1, _paused, _rules));
        }
        Changed?.Invoke();
        return true;
    }

    private static bool TryNormalize(IReadOnlyList<AutomationRule> rules, out List<AutomationRule> normalized, out string error)
    {
        normalized = [];
        if (rules.Count > AutomationLimits.MaxRules) { error = $"At most {AutomationLimits.MaxRules} rules."; return false; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            var label = string.IsNullOrWhiteSpace(rule.Name) ? rule.Id : rule.Name;
            if (!IdPattern().IsMatch(rule.Id ?? "")) { error = "A rule has an invalid id."; return false; }
            if (!ids.Add(rule.Id!)) { error = $"The id '{rule.Id}' is used twice."; return false; }
            if (rule.Actions is null || rule.Actions.Count > AutomationLimits.MaxSteps) { error = $"'{label}' can have at most {AutomationLimits.MaxSteps} steps."; return false; }
            if (rule.Actions.Any(a => a is null || string.IsNullOrWhiteSpace(a.Type) || a.Settings is null)) { error = $"'{label}' has a step that is not valid."; return false; }
            if (rule.CooldownSeconds is < 0 or > AutomationLimits.MaxCooldownSeconds) { error = $"The cooldown of '{label}' must be between 0 and {AutomationLimits.MaxCooldownSeconds} seconds."; return false; }
            if (!TryTrigger(rule.Trigger, label, out var trigger, out error)) return false;
            normalized.Add(new AutomationRule
            {
                Id = rule.Id,
                Name = PlainText.Clean(rule.Name, AutomationLimits.MaxNameLength),
                Enabled = rule.Enabled,
                Trigger = trigger,
                Actions = [.. rule.Actions],
                CooldownSeconds = rule.CooldownSeconds,
            });
        }
        error = "";
        return true;
    }

    private static bool TryTrigger(AutomationTrigger? t, string label, out AutomationTrigger trigger, out string error)
    {
        trigger = new AutomationTrigger();
        error = "";
        switch (t?.Kind)
        {
            case AutomationTriggerKinds.Variable:
                if (t.Condition is null || !TryCondition(t.Condition, out var comparisons) || comparisons == 0 || comparisons > AutomationLimits.MaxComparisons)
                {
                    error = $"The condition of '{label}' is not valid (at most {AutomationLimits.MaxComparisons} comparisons).";
                    return false;
                }
                trigger = new AutomationTrigger { Kind = t.Kind, Condition = t.Condition };
                return true;
            case AutomationTriggerKinds.Time:
                if (!TimeOnly.TryParseExact(t.Time ?? "", "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    error = $"The time of '{label}' must look like 07:30.";
                    return false;
                }
                if (t.Days is null || t.Days.Any(d => d is < 0 or > 6))
                {
                    error = $"The days of '{label}' must be between 0 and 6.";
                    return false;
                }
                trigger = new AutomationTrigger { Kind = t.Kind, Time = t.Time, Days = [.. t.Days.Distinct().Order()] };
                return true;
            case AutomationTriggerKinds.DeviceConnect:
                trigger = new AutomationTrigger { Kind = t.Kind, DeviceId = string.IsNullOrWhiteSpace(t.DeviceId) ? null : t.DeviceId };
                return true;
            default:
                error = $"'{label}' has no known trigger.";
                return false;
        }
    }

    /// <summary>Checks the tree shape and counts its comparisons.</summary>
    private static bool TryCondition(ConditionNode node, out int comparisons, int depth = 0)
    {
        comparisons = 0;
        if (depth > 8) return false;
        switch (node.Kind)
        {
            case ConditionKinds.Compare:
                comparisons = 1;
                return !string.IsNullOrEmpty(node.Variable) && !string.IsNullOrEmpty(node.Operator);
            case ConditionKinds.Not when node.Children.Count != 1:
            case ConditionKinds.And or ConditionKinds.Or or ConditionKinds.Xor when node.Children.Count == 0:
                return false;
            case ConditionKinds.Not or ConditionKinds.And or ConditionKinds.Or or ConditionKinds.Xor:
                foreach (var child in node.Children)
                {
                    if (!TryCondition(child, out var n, depth + 1)) return false;
                    comparisons += n;
                }
                return true;
            default:
                return false;
        }
    }

    private static List<AutomationRule> Clone(List<AutomationRule> rules) =>
        JsonSerializer.Deserialize<List<AutomationRule>>(JsonSerializer.Serialize(rules, FileJson), FileJson) ?? [];

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var file = JsonSerializer.Deserialize<RulesFile>(File.ReadAllText(_path), FileJson);
            if (file?.Rules is null) return;
            _paused = file.Paused;
            _rules = file.Rules.Take(AutomationLimits.MaxRules).ToList();
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }

    private void Write(RulesFile file)
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(file, FileJson));
        File.Move(tmp, _path, overwrite: true);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();
}
