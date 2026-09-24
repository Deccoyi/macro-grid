namespace MacroGrid.Plugin.Abstractions;

/// <summary>Shared key/value store for live data (system.cpu, demo.stream.duration, ...) that widget text templates read from.</summary>
public interface IVariableStore
{
    void Set(string name, object? value);

    object? Get(string name);

    /// <summary>Removes a dynamic variable entirely (e.g. an input that was deleted or renamed),
    /// so it stops showing up in the variable picker. Without this, per-instance variables for objects
    /// that come and go would accumulate forever.</summary>
    void Remove(string name);
}

/// <summary>A background source of variables (system metrics, a service connection, ...). One instance runs for the whole process lifetime.</summary>
public interface IVariableProvider
{
    Task RunAsync(IVariableStore store, CancellationToken cancellationToken);
}
