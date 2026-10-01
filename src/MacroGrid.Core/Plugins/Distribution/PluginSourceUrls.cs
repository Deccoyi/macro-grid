using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>One place the official catalog's signed files can be read from. <see cref="IsApi"/> sources have an hourly quota that is watched.</summary>
public sealed record CatalogSource(Uri Url, bool IsApi);

/// <summary>
/// The only URLs the plugin distribution feature ever talks to. Added sources and pasted links use fixed
/// <c>raw.githubusercontent.com</c> and <c>github.com/.../releases/download/...</c> addresses built from an owner/repo pair.
/// The official catalog's two signed files come from the repository contents API first and the project's Pages site second
/// (<see cref="OfficialSources"/>); both addresses are built here, never from a request or a file.
/// </summary>
public static partial class PluginSourceUrls
{
    public const string OfficialOwner = "Deccoyi";
    public const string OfficialRepo = "macro-grid-plugin";

    [GeneratedRegex(@"^(?:https?://github\.com/)?(?<owner>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)/(?<repo>[A-Za-z0-9._-]+?)(?:\.git)?/?$")]
    private static partial Regex RepoPattern();

    /// <summary>Parses a pasted GitHub repository reference — a full URL or a bare <c>owner/repo</c> — into its
    /// owner and repo. Rejects anything that is not that shape; the caller still needs to check the repository
    /// actually exists and is reachable (this is a syntax check only).</summary>
    public static bool TryParseRepo(string input, out string owner, out string repo)
    {
        var match = RepoPattern().Match(input.Trim());
        if (!match.Success)
        {
            owner = "";
            repo = "";
            return false;
        }
        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        return true;
    }

    /// <summary>Hosts a package or asset URL may point at: raw.githubusercontent.com for metadata, github.com for
    /// the releases/download redirect, and the hosts that redirect actually resolves to.</summary>
    private static readonly string[] AllowedHosts =
    [
        "raw.githubusercontent.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    /// <summary>The signed files of the official catalog live in <c>website/public/catalog/</c> of the plugin repository: the API reads them
    /// from the repository, the Pages site serves the same committed copy.</summary>
    public static IReadOnlyList<CatalogSource> OfficialSources(string fileName) =>
    [
        new(new Uri($"https://api.github.com/repos/{OfficialOwner}/{OfficialRepo}/contents/website/public/catalog/{fileName}"), IsApi: true),
        new(new Uri($"https://{OfficialOwner.ToLowerInvariant()}.github.io/{OfficialRepo}/catalog/{fileName}"), IsApi: false),
    ];

    public const string IndexFileName = "index.signed.json";
    public const string RevokedFileName = "revoked.signed.json";

    public static Uri IndexUrl(string owner, string repo) =>
        new($"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/macrogrid-index.json");

    public static Uri ManifestUrl(string owner, string repo) =>
        new($"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/plugin.json");

    /// <summary>A single-plugin repository's release asset (method 4): tag <c>v&lt;version&gt;</c>, asset
    /// <c>&lt;id&gt;-&lt;version&gt;.zip</c> (or that name plus <c>.sha256</c>), per the plugin repository's
    /// website/reference/source-index.md.</summary>
    public static Uri SinglePluginPackageUrl(string owner, string repo, string id, string version, string suffix = "") =>
        new($"https://github.com/{owner}/{repo}/releases/download/v{version}/{id}-{version}.zip{suffix}");

    /// <summary>Only https on an allowed GitHub host; anything else is refused (nothing is ever fetched from
    /// elsewhere, even if an index or a pasted link says so).</summary>
    public static bool IsAllowedUrl(Uri? url) =>
        url is { IsAbsoluteUri: true, Scheme: "https" } && AllowedHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase);

    /// <summary>A version's download <c>url</c> must be a <c>releases/download</c> asset of the exact repository the
    /// index came from — an index can only point at its own repository's releases.</summary>
    public static bool BelongsToRepo(Uri url, string owner, string repo)
    {
        if (!IsAllowedUrl(url) || !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        var expectedPrefix = $"/{owner}/{repo}/releases/download/";
        return url.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
