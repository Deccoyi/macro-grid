namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// One version of one plugin inside a <c>macrogrid-index.json</c>, or the single <c>plugin.json</c> resolved from
/// a direct link. Field names match the JSON (camelCase) documented in the plugin repository's
/// website/reference/source-index.md.
/// </summary>
public sealed record PluginCatalogVersion(
    string Version,
    string SdkVersion,
    string MinServerVersion,
    string Url,
    string Sha256,
    long Size,
    IReadOnlyList<string>? Permissions,
    string? Signature);

public sealed record PluginCatalogEntry(
    string Id,
    string Name,
    string? Description,
    string? Author,
    string? Homepage,
    string Kind,
    IReadOnlyList<PluginCatalogVersion> Versions);

/// <summary>The parsed and validated contents of a <c>macrogrid-index.json</c>. <see cref="Owner"/> and
/// <see cref="Repo"/> are the repository it was fetched from, used to check that every version's <c>url</c>
/// points back at that same repository (see <see cref="PluginCatalogClient"/>).</summary>
public sealed record PluginCatalogIndex(
    int FormatVersion,
    string Name,
    string? Author,
    string Owner,
    string Repo,
    IReadOnlyList<PluginCatalogEntry> Plugins);
