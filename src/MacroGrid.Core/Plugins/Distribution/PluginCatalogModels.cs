namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// One version of one plugin inside a <c>macrogrid-index.json</c>, or the single <c>plugin.json</c> resolved from
/// a direct link. Field names match the JSON (camelCase) documented in the plugin repository's
/// website/reference/source-index.md.
/// </summary>
/// <param name="MacroGrid">The oldest Macro Grid the version runs on ("1.3.0"). Older index entries carry
/// <paramref name="SdkVersion"/> and <paramref name="MinServerVersion"/> instead; see <see cref="Plugins.PluginCompatibility"/>.</param>
public sealed record PluginCatalogVersion(
    string Version,
    string? MacroGrid,
    string? SdkVersion,
    string? MinServerVersion,
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
/// <summary>A single-plugin repository's root <c>plugin.json</c> (method 4, a pasted link), with the URL and
/// hash still unresolved — the caller builds those from <see cref="Id"/>/<see cref="Version"/> and fetches the
/// <c>.sha256</c> asset once the person confirms compatibility.</summary>
public sealed record PluginSingleManifest(
    string Id,
    string Name,
    string? Description,
    string? Author,
    string? Homepage,
    string Kind,
    string Version,
    string? MacroGrid,
    string? SdkVersion,
    string? MinServerVersion,
    IReadOnlyList<string>? Permissions);

public sealed record PluginCatalogIndex(
    int FormatVersion,
    string Name,
    string? Author,
    string Owner,
    string Repo,
    IReadOnlyList<PluginCatalogEntry> Plugins);
