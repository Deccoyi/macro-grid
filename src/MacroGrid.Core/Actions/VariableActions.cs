using System.Globalization;
using System.Text.Json.Nodes;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>
/// The older all-in-one action: changes a variable from the person's Global Variable List by <c>mode</c>.
/// Settings: { "variable": "user.name", "mode": "set"|"toggle"|"add"|"reset", "value"?: string, "amount"?: number }
/// New buttons use the four small actions below; this one stays so saved profiles keep running (the editor no longer offers it).
/// </summary>
public sealed class SetVariableAction(UserVariableService variables) : IActionHandler, IActionDescriptor, IActionOutcomeHandler
{
    public const string TypeId = "core.setVariable";

    public string Type => TypeId;
    public string DisplayName => "Set variable";
    public string Category => "Variables";
    public string? Description => "Sets, switches, counts or resets a variable from the Global Variable List";
    public string? Icon => "variable";

    // Only "value" is declared so the dispatcher fills its {templates}; the editor form draws the rest.
    public IReadOnlyList<SettingField> Fields =>
        [new("value", "Value", SettingFieldKind.Text) { AllowVariables = true }];

    public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        (await ExecuteWithOutcomeAsync(context, settings, cancellationToken)).ThrowIfFailed();

    public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        Task.FromResult(Apply(variables, context, settings, settings["mode"]?.ToString() ?? "set"));

    /// <summary>Changes the variable named in <c>settings["variable"]</c> the way <paramref name="mode"/> says.</summary>
    internal static ActionOutcome Apply(UserVariableService variables, ActionContext context, JsonObject settings, string mode)
    {
        var name = settings["variable"]?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ActionOutcome.Failed(ActionFailureCode.NotConfigured, "No variable is chosen.");
        if (variables.Find(name) is not { } definition)
            return ActionOutcome.Failed(ActionFailureCode.NotFound, $"The variable '{name}' does not exist.");

        UserVariableWrite result;
        switch (mode)
        {
            case "set":
                var text = settings["value"]?.ToString() ?? "";
                if (text.Length == 0 && context.Value is { } dragged)
                    result = variables.Set(name, dragged);
                else if (text.Length == 0 && definition.Type != VariableType.Text)
                    return ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "The value is empty.");
                else
                    result = variables.Set(name, text);
                break;
            case "toggle":
                result = variables.Toggle(name);
                break;
            case "add":
                var amountText = settings["amount"]?.ToString();
                var amount = 1.0;
                if (!string.IsNullOrWhiteSpace(amountText)
                    && !double.TryParse(amountText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                    return ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "The amount is not a number.");
                result = variables.Add(name, amount);
                break;
            case "reset":
                result = variables.Reset(name);
                break;
            default:
                return ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "This way of changing a variable is not known.");
        }

        return result switch
        {
            UserVariableWrite.Ok => ActionOutcome.Success,
            UserVariableWrite.NotFound => ActionOutcome.Failed(ActionFailureCode.NotFound, $"The variable '{name}' does not exist."),
            UserVariableWrite.WrongType => ActionOutcome.Failed(ActionFailureCode.InvalidParameter,
                mode == "toggle" ? "Only a True/False variable can be switched." : "Only a Number variable can be counted."),
            _ => ActionOutcome.Failed(ActionFailureCode.InvalidParameter, $"The value does not fit the type of '{name}'."),
        };
    }
}

/// <summary>Shared shape of the four small variable actions: each one fixes the mode and reads only the settings it needs.</summary>
public abstract class VariableActionBase(UserVariableService variables, string mode) : IActionHandler, IActionDescriptor, IActionOutcomeHandler
{
    public abstract string Type { get; }
    public abstract string DisplayName { get; }
    public abstract string? Description { get; }
    public string Category => "Variables";
    public string? Icon => "variable";
    public virtual IReadOnlyList<SettingField> Fields => [];

    public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        (await ExecuteWithOutcomeAsync(context, settings, cancellationToken)).ThrowIfFailed();

    public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        Task.FromResult(SetVariableAction.Apply(variables, context, settings, mode));
}

/// <summary>Settings: { "variable": "user.name", "value": string }. An empty value writes the number a slider is dragged to.</summary>
public sealed class VariableSetValueAction(UserVariableService variables) : VariableActionBase(variables, "set")
{
    public const string TypeId = "core.variable.set";
    public override string Type => TypeId;
    public override string DisplayName => "Set variable to a value";
    public override string? Description => "Puts a value into a variable";

    // Only "value" is declared so the dispatcher fills its {templates}; the editor form draws the rest.
    public override IReadOnlyList<SettingField> Fields =>
        [new("value", "Value", SettingFieldKind.Text) { AllowVariables = true }];
}

/// <summary>Settings: { "variable": "user.count", "amount": number } (default 1, negative to subtract).</summary>
public sealed class VariableAddAction(UserVariableService variables) : VariableActionBase(variables, "add")
{
    public const string TypeId = "core.variable.add";
    public override string Type => TypeId;
    public override string DisplayName => "Change a number variable";
    public override string? Description => "Adds to or subtracts from a Number variable";
}

/// <summary>Settings: { "variable": "user.flag" }.</summary>
public sealed class VariableToggleAction(UserVariableService variables) : VariableActionBase(variables, "toggle")
{
    public const string TypeId = "core.variable.toggle";
    public override string Type => TypeId;
    public override string DisplayName => "Switch True / False variable";
    public override string? Description => "Flips a True/False variable to the other value";
}

/// <summary>Settings: { "variable": "user.name" }.</summary>
public sealed class VariableResetAction(UserVariableService variables) : VariableActionBase(variables, "reset")
{
    public const string TypeId = "core.variable.reset";
    public override string Type => TypeId;
    public override string DisplayName => "Reset variable";
    public override string? Description => "Puts a variable back to its start value";
}
