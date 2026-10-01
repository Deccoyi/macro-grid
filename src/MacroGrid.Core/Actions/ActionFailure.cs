using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>One action that did not do its job: which action, why (a code) and a short plain sentence for a person.</summary>
public sealed record ActionFailure(string ActionType, ActionFailureCode Code, string Message);
