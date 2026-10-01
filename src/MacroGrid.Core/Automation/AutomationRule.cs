using MacroGrid.Core.Model;

namespace MacroGrid.Core.Automation;

/// <summary>When a rule starts: a condition that turns true, a time of day, or a device that connects.</summary>
public static class AutomationTriggerKinds
{
    public const string Variable = "variable";
    public const string Time = "time";
    public const string DeviceConnect = "deviceConnect";
}

/// <summary>
/// One trigger. Only the fields of its <see cref="Kind"/> are used: <see cref="Condition"/> for a variable rule,
/// <see cref="Time"/> ("HH:mm") and <see cref="Days"/> (0 = Sunday ... 6 = Saturday, empty = every day) for a time rule,
/// <see cref="DeviceId"/> (null = any paired device) for a device rule.
/// </summary>
public sealed class AutomationTrigger
{
    public string Kind { get; set; } = AutomationTriggerKinds.Variable;
    public ConditionNode? Condition { get; set; }
    public string? Time { get; set; }
    public List<int> Days { get; set; } = [];
    public string? DeviceId { get; set; }
}

/// <summary>One rule: a trigger and an ordinary action list, run without anyone pressing a button.</summary>
public sealed class AutomationRule
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public AutomationTrigger Trigger { get; set; } = new();
    public List<ActionBinding> Actions { get; set; } = [];
    public int CooldownSeconds { get; set; }
}

/// <summary>The numbers that bound the rule list; the editor reads them from the API.</summary>
public static class AutomationLimits
{
    public const int MaxRules = 50;
    public const int MaxNameLength = 60;
    public const int MaxSteps = 20;
    public const int MaxCooldownSeconds = 86400;
    public const int MaxComparisons = 20;
    public const int MaxFileBytes = 256 * 1024;
}
