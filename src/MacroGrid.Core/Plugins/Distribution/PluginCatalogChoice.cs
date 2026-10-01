namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>What Discover offers for one catalog entry. <see cref="Withdrawn"/> is true when the entry lists versions but none is left to install.</summary>
public sealed record PluginCatalogChoice(
    PluginCatalogVersion? Latest,
    PluginCatalogVersion? Installable,
    string? IncompatibleReason,
    bool Withdrawn,
    bool InstalledWithdrawn)
{
    public bool Compatible => Installable is not null;

    /// <summary>Picks the versions to show. A version that is withdrawn, or that <paramref name="isRevoked"/> says the official list switched off,
    /// is never offered; "latest" and "installable" are computed over what is left.</summary>
    public static PluginCatalogChoice Choose(PluginCatalogEntry entry, string macroGridVersion, string? installedVersion, Func<PluginCatalogVersion, bool>? isRevoked = null)
    {
        var comparer = Comparer<string>.Create(SemVer.CompareVersionStrings);
        var offered = entry.Versions.Where(v => !v.Withdrawn && isRevoked?.Invoke(v) != true).ToList();
        var installable = offered
            .Where(v => PluginCompatibility.Check(macroGridVersion, v.MinMacroGrid, v.MacroGrid, v.SdkVersion).Compatible)
            .OrderByDescending(v => v.Version, comparer)
            .FirstOrDefault();
        var latest = offered.OrderByDescending(v => v.Version, comparer).FirstOrDefault();
        var reason = installable is null && latest is not null
            ? PluginCompatibility.Check(macroGridVersion, latest.MinMacroGrid, latest.MacroGrid, latest.SdkVersion).Reason
            : null;
        var installedWithdrawn = installedVersion is not null && entry.Versions.Any(v => v.Withdrawn && v.Version == installedVersion);
        return new PluginCatalogChoice(latest, installable, reason, entry.Versions.Count > 0 && offered.Count == 0, installedWithdrawn);
    }
}
