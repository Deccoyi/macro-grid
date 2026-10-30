using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>The variables of one button (<c>self.*</c>): its own state, answered per widget and per device by <see cref="Widgets.SelfVariableScope"/>. They never enter the shared store.</summary>
public static class SelfVariables
{
    public const string Prefix = "self.";
    public const string Category = "This button";

    public const string Toggled = "self.toggled";
    public const string Busy = "self.busy";
    public const string Pressed = "self.pressed";
    public const string LastResult = "self.lastResult";
    public const string LastError = "self.lastError";

    public const string Success = "Success";
    public const string Failed = "Failed";

    public static bool IsSelfName(string? fullName) => fullName is not null && fullName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Lists the <c>self.*</c> names in the variable picker.</summary>
public sealed class SelfVariableCatalog : IVariableCatalogSource
{
    public IEnumerable<VariableInfo> Describe() =>
    [
        Info(SelfVariables.Toggled, "True while this toggle is on. The same on every device.", VariableType.Boolean),
        Info(SelfVariables.Busy, "True while this button's actions are running (after a short moment). Only on the device that pressed it.", VariableType.Boolean),
        Info(SelfVariables.Pressed, "True while this button is held down, and for a moment after. Only on the device that pressed it.", VariableType.Boolean),
        Info(SelfVariables.LastResult, "How the last run of this button's actions ended. Empty until it has run. The same on every device.", VariableType.Text,
            [SelfVariables.Success, SelfVariables.Failed]),
        Info(SelfVariables.LastError, "Why the last run failed (the failure code). Empty after a success. The same on every device.", VariableType.Text,
            [.. Enum.GetNames<ActionFailureCode>()]),
    ];

    private static VariableInfo Info(string name, string description, VariableType type, IReadOnlyList<string>? values = null) =>
        new(name, description, "{" + name + "}", SelfVariables.Category) { Type = type, Values = values };
}
