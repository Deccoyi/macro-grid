using System.Collections.Concurrent;
using MacroGrid.Core.Actions;

namespace MacroGrid.Core.Widgets;

/// <summary>How the last run of each widget's actions ended, keyed by widget id. In memory only, shared by every device.</summary>
public sealed class LastResultStore
{
    private readonly ConcurrentDictionary<string, (string Result, string Error)> _results = new();

    /// <summary>Records a run: "Failed" with the code of the first failure, or "Success".</summary>
    public void Set(string widgetId, IReadOnlyList<ActionFailure> failures) =>
        _results[widgetId] = failures.Count == 0 ? (Variables.SelfVariables.Success, "") : (Variables.SelfVariables.Failed, failures[0].Code.ToString());

    public (string Result, string Error) Get(string widgetId) => _results.GetValueOrDefault(widgetId, ("", ""));
}
