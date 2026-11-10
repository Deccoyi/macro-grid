using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Variables;

/// <summary>Something in the host that reads variables, and can say which ones.</summary>
public interface IVariableDemandSource
{
    bool Observes(string name);

    bool ObservesPrefix(string prefix);
}

/// <summary>
/// What the plugins see as <see cref="IPluginHost.Demand"/>. The host's readers (shown pages, automations, plugin widgets) register as sources;
/// readers that cannot say in advance what they read (a script's <c>variables.get</c>, the editor's one-shot snapshot) hold a short lease instead.
/// A failing source counts as "observed": the answer errs on the side of work being done.
/// </summary>
public sealed class VariableDemand(TimeProvider? clock = null) : IVariableDemand
{
    /// <summary>How long after a script read a variable, or the editor took a snapshot, the variable still counts as needed.</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _lock = new();
    private readonly List<IVariableDemandSource> _sources = [];
    private readonly Dictionary<string, DateTimeOffset> _leases = [];
    private DateTimeOffset _allUntil;

    public event Action? Changed;

    /// <summary>Adds a reader. Sources live as long as the process.</summary>
    public void AddSource(IVariableDemandSource source)
    {
        lock (_lock) _sources.Add(source);
    }

    public bool IsObserved(string name) => Evaluate(s => s.Observes(name), (leases, now) => leases.Any(l => l.Value > now && l.Key == name));

    public bool IsPrefixObserved(string prefix) =>
        Evaluate(s => s.ObservesPrefix(prefix), (leases, now) => leases.Any(l => l.Value > now && l.Key.StartsWith(prefix, StringComparison.Ordinal)));

    private bool Evaluate(Func<IVariableDemandSource, bool> source, Func<Dictionary<string, DateTimeOffset>, DateTimeOffset, bool> lease)
    {
        IVariableDemandSource[] sources;
        var now = _clock.GetUtcNow();
        lock (_lock)
        {
            if (_allUntil > now || lease(_leases, now)) return true;
            sources = [.. _sources];
        }
        foreach (var s in sources)
        {
            try
            {
                if (source(s)) return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A script read <paramref name="name"/>: it counts as needed for <see cref="LeaseDuration"/>. Raises <see cref="Changed"/> only when the lease starts.</summary>
    public void Touch(string name)
    {
        var now = _clock.GetUtcNow();
        bool started;
        lock (_lock)
        {
            started = !_leases.TryGetValue(name, out var until) || until <= now;
            _leases[name] = now + LeaseDuration;
            if (started && _leases.Count > 256)
                foreach (var gone in _leases.Where(l => l.Value <= now).Select(l => l.Key).ToList()) _leases.Remove(gone);
        }
        if (started) NotifyChanged();
    }

    /// <summary>Every variable counts as needed for <see cref="LeaseDuration"/> (the editor took a snapshot of all values).</summary>
    public void TouchAll()
    {
        var now = _clock.GetUtcNow();
        bool started;
        lock (_lock)
        {
            started = _allUntil <= now;
            _allUntil = now + LeaseDuration;
        }
        if (started) NotifyChanged();
    }

    /// <summary>A reader started to need more (a page was shown, a profile or rule was saved). Plugins ask again and refresh.</summary>
    public void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception) { /* a plugin's handler must not break the host path that raised the change */ }
        }
    }
}
