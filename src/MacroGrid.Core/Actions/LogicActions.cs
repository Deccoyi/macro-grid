using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>The steps that steer an action list (see <see cref="ActionFlow"/>). The dispatcher interprets them; as handlers they only let the catalog know their names, and run alone they do nothing.</summary>
public abstract class LogicAction(string type, string displayName, string description, string icon) : IActionHandler, IActionDescriptor
{
    public string Type => type;
    public string DisplayName => displayName;
    public string Category => "Logic";
    public string? Description => description;
    public string? Icon => icon;
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class IfAction() : LogicAction(ActionFlow.If, "If", "Runs the steps up to the matching End only when a condition is met", "split");

public sealed class ElseAction() : LogicAction(ActionFlow.Else, "Otherwise", "The steps that run when the If was not met", "split");

public sealed class EndIfAction() : LogicAction(ActionFlow.EndIf, "End", "Ends an If block", "split");

public sealed class StopAction() : LogicAction(ActionFlow.Stop, "Stop", "Ends the action list here", "octagon");
