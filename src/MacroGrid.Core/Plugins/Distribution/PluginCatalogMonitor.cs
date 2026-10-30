using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// One step of the background look at the official catalog: refresh what is due, apply a newer safety list to the running plugins, and keep
/// the warning lines up to date (a withdrawn installed version, a safety list that has not been checked for a long time). The scheduling around
/// it is in <c>PluginCatalogService</c>; this class has no timers so it can be tested with a fake clock.
/// </summary>
public sealed class PluginCatalogMonitor(
    OfficialCatalog catalog,
    PluginManager plugins,
    PluginInstallOriginStore origins,
    ProblemList problems,
    TimeProvider? clock = null)
{
    private const string StaleSource = "macro-grid";
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Only plugins the official project vouches for are looked after, so a computer without any never contacts the catalog on its own.</summary>
    public bool HasWatchedPlugins() =>
        plugins.Plugins.Any(p => origins.Get(p.Id)?.Trust == PluginTrust.Official);

    /// <summary>Refreshes the files that are due; a newly accepted file is applied. Never throws except when cancelled.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await catalog.RefreshDueAsync(cancellationToken))
                await plugins.ApplyRevocationsAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed look changes nothing: plugins keep running as they are.
        }
        UpdateLines();
    }

    /// <summary>Rewrites the warning lines from the saved copies. No network.</summary>
    public void UpdateLines()
    {
        var index = catalog.Index;
        foreach (var plugin in plugins.Plugins)
        {
            var withdrawn = origins.Get(plugin.Id)?.Trust == PluginTrust.Official
                && index?.Plugins.FirstOrDefault(e => e.Id == plugin.Id)?.Versions.Any(v => v.Withdrawn && v.Version == plugin.Version) == true;
            if (withdrawn)
                problems.Report(plugin.Id, plugin.Name, ProblemSeverity.Warning, ProblemCodes.Withdrawn,
                    $"Version {plugin.Version} was withdrawn by its publisher. Update it or remove it.");
            else
                problems.Resolve(plugin.Id, ProblemCodes.Withdrawn);
        }

        if (HasWatchedPlugins() && catalog.RevokedCheckedOrCreatedUtc is { } checkedAt && _clock.GetUtcNow() - checkedAt > PluginCatalogPolicy.StaleAfter)
            problems.Report(StaleSource, "Macro Grid", ProblemSeverity.Warning, ProblemCodes.CatalogStale,
                "The official plugin safety list could not be checked for a long time. Plugins keep running.");
        else
            problems.Resolve(StaleSource, ProblemCodes.CatalogStale);
    }

    /// <summary>The wait before the next look: until the earliest due file, at least a minute.</summary>
    public TimeSpan NextWait() => TimeSpan.FromTicks(Math.Max(catalog.NextDueIn().Ticks, PluginCatalogPolicy.MinGap.Ticks));
}
