using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>
/// Process-wide store of live values (system.cpu, obs.stream.duration, ...).
/// <see cref="WidgetStateService"/> in a separate assembly cannot see this class directly? No —
/// it lives in Core too and subscribes to <see cref="Changed"/> to know which widgets need a refresh.
/// </summary>
public sealed class VariableStore : IVariableStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, object?> _values = [];

    /// <summary>Raised after a variable's value actually changes; a Set with an unchanged (Equals) value is silent.</summary>
    public event Action<string>? Changed;

    public void Set(string name, object? value)
    {
        lock (_lock)
        {
            if (_values.TryGetValue(name, out var existing) && Equals(existing, value))
                return;
            _values[name] = value;
        }
        Changed?.Invoke(name);
    }

    public object? Get(string name)
    {
        lock (_lock) return _values.GetValueOrDefault(name);
    }

    public void Remove(string name)
    {
        lock (_lock)
        {
            if (!_values.Remove(name))
                return;
        }
        Changed?.Invoke(name);
    }

    /// <summary>A point-in-time copy of every known variable, for the editor's one-shot live preview.</summary>
    public IReadOnlyDictionary<string, object?> Snapshot()
    {
        lock (_lock) return new Dictionary<string, object?>(_values);
    }
}
