using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// The only URLs the plugin distribution feature ever talks to: fixed <c>raw.githubusercontent.com</c> and
/// <c>github.com/.../releases/download/...</c> addresses built from an owner/repo pair, never the GitHub API. See
/// the plugin repository's website/reference/source-index.md for why (no rate limit, and this is the host's
/// first outbound connection, opt-in and user-triggered only).
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

    public static Uri IndexUrl(string owner, string repo) =>
        new($"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/macrogrid-index.json");

    public static Uri ManifestUrl(string owner, string repo) =>
        new($"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/plugin.json");

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
