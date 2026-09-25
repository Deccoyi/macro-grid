using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MacroGrid.Core.Updates;

/// <summary>What one call to the releases endpoint returned. <see cref="Body"/> is null when the server answered 304 (nothing changed since the sent ETag).</summary>
public sealed record FeedResponse(string? ETag, string? Body)
{
    public bool NotModified => Body is null;
}

/// <summary>
/// The GitHub releases list as a source of server updates: one HTTP call (with <c>If-None-Match</c>, so an unchanged list is a 304 that does
/// not count against the anonymous rate limit), and pure functions that turn the JSON into <see cref="ReleaseInfo"/> and pick the update.
/// Nothing here runs by itself; the host's update service decides when to call it.
/// </summary>
public sealed class ReleaseFeed(HttpClient http, Uri endpoint)
{
    public const string TagPrefix = "server-v";
    public const string InstallerPrefix = "MacroGrid-Setup-";

    /// <summary>Hosts a download or page URL may point at: GitHub itself and the hosts its release downloads redirect to.</summary>
    private static readonly string[] AllowedHosts =
    [
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    public async Task<FeedResponse> FetchAsync(string? etag, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out var tag)) request.Headers.IfNoneMatch.Add(tag);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified) return new FeedResponse(etag, null);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FeedResponse(response.Headers.ETag?.ToString(), body);
    }

    /// <summary>
    /// Keeps the published server releases (tag "server-v…", not a draft) whose tag is a version. Malformed entries are skipped, never thrown on:
    /// the feed is data from outside, and one odd entry must not hide the others. Newest version first.
    /// </summary>
    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        var releases = new List<ReleaseInfo>();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return releases;

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (GetBool(item, "draft")) continue;
            var tag = GetString(item, "tag_name");
            if (!ReleaseVersion.TryParseTag(tag, TagPrefix, out var version)) continue;

            releases.Add(new ReleaseInfo(
                version,
                tag!,
                GetString(item, "name") ?? tag!,
                GetString(item, "body") ?? "",
                DateTimeOffset.TryParse(GetString(item, "published_at"), out var published) ? published : null,
                AllowedUrl(GetString(item, "html_url")),
                FindInstaller(item, version),
                GetBool(item, "prerelease")));
        }

        releases.Sort((a, b) => b.Version.CompareTo(a.Version));
        return releases;
    }

    /// <summary>
    /// The update to offer: the newest release strictly newer than <paramref name="current"/> (never a downgrade), and every release newer than
    /// <paramref name="current"/> up to it. Pre-releases count only when <paramref name="includePreReleases"/> is on. Null when there is none.
    /// </summary>
    public static UpdateOffer? FindUpdate(IEnumerable<ReleaseInfo> releases, ReleaseVersion current, bool includePreReleases)
    {
        var newer = releases
            .Where(r => r.Version > current && (includePreReleases || !r.IsPreRelease))
            .OrderByDescending(r => r.Version)
            .ToList();
        return newer.Count == 0 ? null : new UpdateOffer(newer[0], newer);
    }

    /// <summary>Only https on a GitHub host; anything else is refused (an installer from elsewhere is never downloaded).</summary>
    public static bool IsAllowedUrl(Uri? url) =>
        url is { IsAbsoluteUri: true, Scheme: "https" } && AllowedHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase);

    private static Uri? AllowedUrl(string? text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var url) && IsAllowedUrl(url) ? url : null;

    private static ReleaseAsset? FindInstaller(JsonElement release, ReleaseVersion version)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        var expected = $"{InstallerPrefix}{version.Core}.exe";

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object) continue;
            var name = GetString(asset, "name");
            if (!string.Equals(name, expected, StringComparison.OrdinalIgnoreCase)) continue;

            var url = AllowedUrl(GetString(asset, "browser_download_url"));
            if (url is null) return null;
            var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var s) ? s : 0;
            return new ReleaseAsset(name!, url, size, ParseDigest(GetString(asset, "digest")));
        }
        return null;
    }

    /// <summary>GitHub reports "sha256:&lt;hex&gt;"; any other algorithm or a malformed value counts as no digest.</summary>
    internal static string? ParseDigest(string? digest)
    {
        const string prefix = "sha256:";
        if (digest is null || !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var hex = digest[prefix.Length..].Trim().ToLowerInvariant();
        return hex.Length == 64 && hex.All(Uri.IsHexDigit) ? hex : null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
