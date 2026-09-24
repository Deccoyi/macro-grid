namespace MacroGrid.Core.Plugins.Js;

/// <summary>
/// The resource ceiling of one JS plugin. Every entry into the script (start-up, an action, a timer callback)
/// gets its own budget, so a runaway callback is cut off without harming the rest of the server.
/// </summary>
public sealed record JsPluginLimits(
    TimeSpan CallTimeout,
    long MemoryBytes,
    int MaxStatements,
    int MaxRecursion,
    int MaxTimers,
    TimeSpan HttpTimeout,
    int MaxHttpResponseBytes,
    int MaxConsecutiveErrors)
{
    public static JsPluginLimits Default { get; } = new(
        CallTimeout: TimeSpan.FromSeconds(2),
        MemoryBytes: 32 * 1024 * 1024,
        MaxStatements: 2_000_000,
        MaxRecursion: 100,
        MaxTimers: 20,
        HttpTimeout: TimeSpan.FromSeconds(5),
        MaxHttpResponseBytes: 1024 * 1024,
        MaxConsecutiveErrors: 5);
}
