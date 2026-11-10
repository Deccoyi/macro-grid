namespace MacroGrid.Plugin.Abstractions;

/// <summary>Tells a plugin whether anything currently needs the variables it publishes: a widget on a page a device shows, a dynamic rule, an
/// automation trigger or condition, a plugin widget's subscription, or a script that read the variable a moment ago. A plugin that polls a slow
/// source can skip the poll while nothing needs the result. Optional: <see cref="IPluginHost.Demand"/> has a default that says "everything is
/// needed", so a plugin that never looks at it, and a host that does not implement it, behave as before.</summary>
public interface IVariableDemand
{
    /// <summary>True when something needs the variable <paramref name="name"/> now. When in doubt the host answers true; a false answer is only given
    /// when nothing it knows of reads the variable, so skipping the work cannot leave a visible value stale.</summary>
    bool IsObserved(string name);

    /// <summary>True when something needs any variable whose name starts with <paramref name="prefix"/> (for example <c>obs.stream.</c>).</summary>
    bool IsPrefixObserved(string prefix);

    /// <summary>Raised when demand may have grown (a page was shown, a profile was saved, a rule was added, a script read a variable). It is not
    /// raised when demand shrinks: ask again at the next tick. Refresh the values you skipped, then publish them. The handler runs on a host thread
    /// and must return quickly (hand the work to your own loop); an exception in it is caught and logged by the host.</summary>
    event Action? Changed;
}

/// <summary>The default demand: everything is needed, nothing ever changes. Used when the host does not provide demand information.</summary>
public sealed class NullVariableDemand : IVariableDemand
{
    public static readonly NullVariableDemand Instance = new();

    private NullVariableDemand() { }

    public bool IsObserved(string name) => true;

    public bool IsPrefixObserved(string prefix) => true;

    public event Action? Changed { add { } remove { } }
}
