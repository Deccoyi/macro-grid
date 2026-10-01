using System.Globalization;
using System.Text.Json.Nodes;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>
/// Changes a variable from the person's Global Variable List.
/// Settings: { "variable": "user.name", "mode": "set"|"toggle"|"add"|"reset", "value"?: string, "amount"?: number }
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

    public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var name = settings["variable"]?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return Done(ActionOutcome.Failed(ActionFailureCode.NotConfigured, "No variable is chosen."));
        if (variables.Find(name) is not { } definition)
            return Done(ActionOutcome.Failed(ActionFailureCode.NotFound, $"The variable '{name}' does not exist."));

        var mode = settings["mode"]?.ToString() ?? "set";
        UserVariableWrite result;
        switch (mode)
        {
            case "set":
                var text = settings["value"]?.ToString() ?? "";
                if (text.Length == 0 && context.Value is { } dragged)
                    result = variables.Set(name, dragged);
                else if (text.Length == 0 && definition.Type != VariableType.Text)
                    return Done(ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "The value is empty."));
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
                    return Done(ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "The amount is not a number."));
                result = variables.Add(name, amount);
                break;
            case "reset":
                result = variables.Reset(name);
                break;
            default:
                return Done(ActionOutcome.Failed(ActionFailureCode.InvalidParameter, "This way of changing a variable is not known."));
        }

        return Done(result switch
        {
            UserVariableWrite.Ok => ActionOutcome.Success,
            UserVariableWrite.NotFound => ActionOutcome.Failed(ActionFailureCode.NotFound, $"The variable '{name}' does not exist."),
            UserVariableWrite.WrongType => ActionOutcome.Failed(ActionFailureCode.InvalidParameter,
                mode == "toggle" ? "Only a True/False variable can be switched." : "Only a Number variable can be counted."),
            _ => ActionOutcome.Failed(ActionFailureCode.InvalidParameter, $"The value does not fit the type of '{name}'."),
        });
    }

    private static Task<ActionOutcome> Done(ActionOutcome outcome) => Task.FromResult(outcome);
}
