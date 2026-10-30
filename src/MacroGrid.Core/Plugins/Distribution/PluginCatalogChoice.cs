namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>What Discover offers for one catalog entry. <see cref="Withdrawn"/> is true when the entry lists versions but none is left to install.</summary>
public sealed record PluginCatalogChoice(
    PluginCatalogVersion? Latest,
    PluginCatalogVersion? Installable,
    string? IncompatibleReason,
    bool Withdrawn,
    bool InstalledWithdrawn,
    IReadOnlyList<PluginCatalogVersion> Offered,
    PluginCatalogVersion? Previous,
    bool InstalledRevoked)
{
    /// <summary>The most versions the install endpoint accepts for one plugin (newest first).</summary>
    public const int MaxOffered = 5;

    public bool Compatible => Installable is not null;

    /// <summary>Picks the versions to show. <see cref="Offered"/> is the short list the person may install (compatible, newest first, at most five); <see cref="Previous"/> is the newest of them below the installed version. A version that is withdrawn, or that <paramref name="isRevoked"/> says the official list switched off,
    /// is never offered; "latest" and "installable" are computed over what is left.</summary>
    public static PluginCatalogChoice Choose(PluginCatalogEntry entry, string macroGridVersion, string? installedVersion, Func<PluginCatalogVersion, bool>? isRevoked = null)
    {
        var comparer = Comparer<string>.Create(SemVer.CompareVersionStrings);
        var offered = entry.Versions.Where(v => !v.Withdrawn && isRevoked?.Invoke(v) != true).ToList();
        var compatibleVersions = offered
            .Where(v => PluginCompatibility.Check(macroGridVersion, v.MinMacroGrid, v.MacroGrid, v.SdkVersion).Compatible)
            .OrderByDescending(v => v.Version, comparer)
            .Take(MaxOffered)
            .ToList();
        var installable = compatibleVersions.FirstOrDefault();
        var previous = installedVersion is null
            ? null
            : compatibleVersions.FirstOrDefault(v => SemVer.CompareVersionStrings(v.Version, installedVersion) < 0);
        var installedRevoked = installedVersion is not null && isRevoked is not null
            && entry.Versions.FirstOrDefault(v => v.Version == installedVersion) is { } current && isRevoked(current);
        var latest = offered.OrderByDescending(v => v.Version, comparer).FirstOrDefault();
        var reason = installable is null && latest is not null
            ? PluginCompatibility.Check(macroGridVersion, latest.MinMacroGrid, latest.MacroGrid, latest.SdkVersion).Reason
            : null;
        var installedWithdrawn = installedVersion is not null && entry.Versions.Any(v => v.Withdrawn && v.Version == installedVersion);
        return new PluginCatalogChoice(latest, installable, reason, entry.Versions.Count > 0 && offered.Count == 0, installedWithdrawn,
            compatibleVersions, previous, installedRevoked);
    }
}
