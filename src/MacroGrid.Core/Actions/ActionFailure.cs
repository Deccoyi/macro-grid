using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>One action that did not do its job: which action, why (a code) and a short plain sentence for a person.
/// <see cref="Missing"/> is set when no action of that type is registered at all (its plugin was removed or is switched off).</summary>
public sealed record ActionFailure(string ActionType, ActionFailureCode Code, string Message, bool Missing = false);
