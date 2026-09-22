using System.Collections.Concurrent;

namespace MacroStation.Core.Sessions;

/// <summary>
/// On/off state of every toggle-type widget, keyed by widget id (ids are globally unique, so no profile/page key is needed).
/// Shared across every client showing that widget, so flipping it on one device updates all of them.
/// </summary>
public sealed class ToggleStateStore
{
    private readonly ConcurrentDictionary<string, bool> _states = new();

    /// <summary>Flips the widget's state (defaulting to off) and returns the new value.</summary>
    public bool Toggle(string widgetId) => _states.AddOrUpdate(widgetId, true, (_, current) => !current);

    public bool Get(string widgetId) => _states.GetValueOrDefault(widgetId);
}
