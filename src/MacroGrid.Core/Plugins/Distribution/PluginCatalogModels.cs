namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// One version of one plugin inside a <c>macrogrid-index.json</c>, or the single <c>plugin.json</c> resolved from
/// a direct link. Field names match the JSON (camelCase) documented in the plugin repository's
/// website/reference/source-index.md.
/// </summary>
/// <param name="Withdrawn">The publisher took this version back: it cannot be installed any more, an installed copy keeps running and shows a warning.</param>
/// <param name="Urls">Optional list of download addresses; <paramref name="Url"/> is the first one.</param>
/// <param name="MacroGrid">The oldest Macro Grid the version runs on ("1.3.0"). Older index entries carry
/// <paramref name="SdkVersion"/> and <paramref name="MinServerVersion"/> instead; see <see cref="Plugins.PluginCompatibility"/>.</param>
public sealed record PluginCatalogVersion(
    string Version,
    string? MinMacroGrid,
    string? MacroGrid,
    string? SdkVersion,
    string? MinServerVersion,
    string Url,
    string Sha256,
    long Size,
    IReadOnlyList<string>? Permissions,
    string? Signature,
    bool Withdrawn = false,
    IReadOnlyList<string>? Urls = null)
{
    /// <summary>Every address the package can be fetched from, in the order to try them (at most 3). Just <see cref="Url"/> when the index gives no list.</summary>
    public IReadOnlyList<string> Addresses => Urls is { Count: > 0 } ? Urls : [Url];
}

public sealed record PluginCatalogEntry(
    string Id,
    string Name,
    string? Description,
    string? Author,
    string? Homepage,
    string Kind,
    IReadOnlyList<PluginCatalogVersion> Versions)
{
    /// <summary>What to browse by (at most 32 characters) and search words (at most 5, 24 characters each), both checked by <see cref="CatalogText"/>.</summary>
    public string? Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The file name of the plugin's icon among its release's assets (a name only, see <see cref="CatalogText.IsIconFileName"/>);
    /// the host builds the address from the release itself, so an entry cannot point the editor at another place.</summary>
    public string? Icon { get; init; }
}

/// <summary>Limits for the free text a catalog entry carries, since every such field is written by a plugin author and drawn by the editor.</summary>
public static class CatalogText
{
    public const int MaxCategoryLength = 32;
    public const int MaxTags = 5;
    public const int MaxTagLength = 24;

    /// <summary>Trims, removes control and direction-changing characters and cuts to <paramref name="max"/>; null when nothing is left.</summary>
    public static string? Clean(string? text, int max)
    {
        if (text is null) return null;
        var chars = text.Where(c => !char.IsControl(c) && c is not (>= '\u200B' and <= '\u200F') and not (>= '\u202A' and <= '\u202E') and not (>= '\u2066' and <= '\u2069') and not '\uFEFF').ToArray();
        var cleaned = new string(chars).Trim();
        if (cleaned.Length > max) cleaned = cleaned[..max].TrimEnd();
        return cleaned.Length == 0 ? null : cleaned;
    }

    public static bool IsIconFileName(string? name) =>
        name is { Length: > 0 and <= 80 } && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9._-]*\.(png|svg)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> CleanTags(IEnumerable<string?>? tags) =>
        tags is null ? [] : [.. tags.Select(t => Clean(t, MaxTagLength)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTags)];
}

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
    string? MinMacroGrid,
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
