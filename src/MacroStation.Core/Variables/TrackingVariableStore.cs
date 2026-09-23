using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Variables;

/// <summary>
/// The <see cref="IVariableStore"/> a plugin's providers write through: forwards to the shared store but
/// remembers every name that was set, so unloading the plugin can remove exactly its variables and no
/// stale value lingers in widgets or the variable picker.
/// </summary>
internal sealed class TrackingVariableStore(IVariableStore inner) : IVariableStore
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _names = [];

    public void Set(string name, object? value)
    {
        lock (_lock) _names.Add(name);
        inner.Set(name, value);
    }

    public object? Get(string name) => inner.Get(name);

    public void Remove(string name)
    {
        lock (_lock) _names.Remove(name);
        inner.Remove(name);
    }

    public void RemoveAll()
    {
        string[] names;
        lock (_lock)
        {
            names = [.. _names];
            _names.Clear();
        }
        foreach (var name in names)
            inner.Remove(name);
    }
}
