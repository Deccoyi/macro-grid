using System.Text.Json;
using System.Text.Json.Serialization;
using MacroGrid.Core.Model;

namespace MacroGrid.Core.Automation;

/// <summary>When a rule starts: a condition that turns true, a variable that changes, a time of day, or a device that connects.</summary>
public static class AutomationTriggerKinds
{
    public const string Variable = "variable";
    public const string Time = "time";
    public const string DeviceConnect = "deviceConnect";
    public const string VariableChange = "variableChange";
}

/// <summary>How a <c>variableChange</c> trigger judges a change: any change (narrowed by From/To), a number crossing a threshold, or a text that contains a word.</summary>
public static class AutomationChangeModes
{
    public const string Any = "any";
    public const string Crosses = "crosses";
    public const string Contains = "contains";
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
    /// <summary>variableChange: the full name of the watched variable (a global one; <c>self.*</c> is refused).</summary>
    public string? Variable { get; set; }
    /// <summary>variableChange: one of <see cref="AutomationChangeModes"/>.</summary>
    public string? Mode { get; set; }
    /// <summary>variableChange: the old value must equal this (null = any).</summary>
    public string? From { get; set; }
    /// <summary>variableChange: the new value must equal this (null = any); in mode "contains" the word the new text must contain.</summary>
    public string? To { get; set; }
    /// <summary>variableChange, mode "crosses": the number the value has to cross, in either direction.</summary>
    public double? Threshold { get; set; }
    /// <summary>variableChange: at least this long since the rule last started (on top of the cooldown).</summary>
    public int MinIntervalSeconds { get; set; }
    /// <summary>Fields of a newer build are kept and written back, so a round trip through this build loses nothing.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
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
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>The numbers that bound the rule list; the editor reads them from the API.</summary>
public static class AutomationLimits
{
    public const int MaxRules = 50;
    public const int MaxNameLength = 60;
    public const int MaxSteps = 20;
    public const int MaxCooldownSeconds = 86400;
    public const int MaxComparisons = 20;
    public const int MaxMinIntervalSeconds = 3600;
    public const int MaxChangeTextLength = 200;
    public const int MaxFileBytes = 256 * 1024;
}
