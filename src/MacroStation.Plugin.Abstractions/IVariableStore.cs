namespace MacroStation.Plugin.Abstractions;

/// <summary>Shared key/value store for live data (system.cpu, obs.stream.duration, ...) that widget text templates read from.</summary>
public interface IVariableStore
{
    void Set(string name, object? value);

    object? Get(string name);
}

/// <summary>A background source of variables (system metrics, an OBS connection, ...). One instance runs for the whole process lifetime.</summary>
public interface IVariableProvider
{
    Task RunAsync(IVariableStore store, CancellationToken cancellationToken);
}
