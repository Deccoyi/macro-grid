using System.Net;
using System.Net.Http.Headers;
using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class ReleaseFeedTests
{
    private static readonly string Digest = "sha256:" + new string('a', 64);
    private static readonly Uri Endpoint = new("https://api.github.com/repos/Deccoyi/macro-grid/releases");

    private static ReleaseVersion V(string text) => ReleaseVersion.TryParse(text, out var v) ? v : throw new FormatException(text);

    private static string Release(string tag, bool draft = false, bool prerelease = false, string? assetName = "auto", string? digest = "auto",
        string assetUrl = "https://github.com/Deccoyi/macro-grid/releases/download/x/y.exe", string pageUrl = "https://github.com/Deccoyi/macro-grid/releases/tag/x")
    {
        var version = tag.StartsWith("server-v") ? tag["server-v".Length..] : tag;
        var core = version.Split('-')[0];
        var name = assetName == "auto" ? $"MacroGrid-Setup-{core}.exe" : assetName;
        var digestJson = digest == "auto" ? $"\"{Digest}\"" : digest is null ? "null" : $"\"{digest}\"";
        var assets = name is null
            ? "[]"
            : $"[{{\"name\":\"{name}\",\"browser_download_url\":\"{assetUrl}\",\"size\":1234,\"digest\":{digestJson}}}]";
        return $"{{\"tag_name\":\"{tag}\",\"name\":\"Release {tag}\",\"body\":\"notes for {tag}\",\"draft\":{draft.ToString().ToLowerInvariant()},"
            + $"\"prerelease\":{prerelease.ToString().ToLowerInvariant()},\"published_at\":\"2026-09-01T10:00:00Z\",\"html_url\":\"{pageUrl}\",\"assets\":{assets}}}";
    }

    private static string Feed(params string[] releases) => "[" + string.Join(",", releases) + "]";

    private static IReadOnlyList<string> Versions(IEnumerable<ReleaseInfo> releases) => releases.Select(r => r.Version.ToString()).ToList();

    [Fact]
    public void Parse_keeps_server_releases_newest_first_and_skips_drafts_and_other_tags()
    {
        var releases = ReleaseFeed.Parse(Feed(
            Release("server-v0.2.0-alpha", prerelease: true),
            Release("server-v0.3.0-alpha", prerelease: true),
            Release("server-v0.9.0", draft: true),
            Release("client-v1.0.0"),
            Release("server-vnonsense")));

        Assert.Equal(["0.3.0-alpha", "0.2.0-alpha"], Versions(releases));
    }

    [Fact]
    public void Parse_reads_notes_installer_and_digest()
    {
        var release = Assert.Single(ReleaseFeed.Parse(Feed(Release("server-v0.2.1-alpha", prerelease: true))));

        Assert.Equal("notes for server-v0.2.1-alpha", release.Notes);
        Assert.Equal("MacroGrid-Setup-0.2.1.exe", release.Installer?.Name);
        Assert.Equal(new string('a', 64), release.Installer?.Sha256);
        Assert.Equal(1234, release.Installer?.Size);
        Assert.True(release.CanInstall);
        Assert.NotNull(release.PageUrl);
    }

    [Fact]
    public void Release_without_the_installer_asset_cannot_install()
    {
        var wrongName = Assert.Single(ReleaseFeed.Parse(Feed(Release("server-v0.3.0", assetName: "MacroGrid-portable.zip"))));
        var none = Assert.Single(ReleaseFeed.Parse(Feed(Release("server-v0.3.0", assetName: null))));

        Assert.Null(wrongName.Installer);
        Assert.Null(none.Installer);
        Assert.False(wrongName.CanInstall);
    }

    [Fact]
    public void Release_without_a_usable_digest_cannot_install()
    {
        foreach (var digest in new string?[] { null, "md5:abc", "sha256:zz", "sha256:" + new string('a', 63) })
        {
            var release = Assert.Single(ReleaseFeed.Parse(Feed(Release("server-v0.3.0", digest: digest))));
            Assert.NotNull(release.Installer);
            Assert.False(release.CanInstall);
        }
    }

    [Fact]
    public void Parse_refuses_urls_outside_the_allow_list()
    {
        var release = Assert.Single(ReleaseFeed.Parse(Feed(Release("server-v0.3.0",
            assetUrl: "https://evil.example/MacroGrid-Setup-0.3.0.exe", pageUrl: "http://github.com/x"))));

        Assert.Null(release.Installer);
        Assert.Null(release.PageUrl);
    }

    [Theory]
    [InlineData("https://github.com/a/b/releases/download/t/f.exe", true)]
    [InlineData("https://objects.githubusercontent.com/x", true)]
    [InlineData("https://release-assets.githubusercontent.com/x", true)]
    [InlineData("http://github.com/x", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://evilgithub.com/x", false)]
    [InlineData("https://example.com/x", false)]
    [InlineData("file:///C:/x.exe", false)]
    public void Url_allow_list(string url, bool allowed) => Assert.Equal(allowed, ReleaseFeed.IsAllowedUrl(new Uri(url)));

    [Fact]
    public void Parse_tolerates_odd_entries_and_non_arrays()
    {
        var releases = ReleaseFeed.Parse("[1, null, {\"tag_name\": 5}, {\"tag_name\":\"server-v0.3.0\",\"assets\":\"nope\"}]");

        Assert.Equal("0.3.0", Assert.Single(releases).Version.ToString());
        Assert.Empty(ReleaseFeed.Parse("{\"message\":\"rate limited\"}"));
    }

    [Fact]
    public void FindUpdate_offers_the_newest_and_everything_in_between()
    {
        var releases = ReleaseFeed.Parse(Feed(
            Release("server-v0.2.0-alpha", prerelease: true),
            Release("server-v0.2.1-alpha", prerelease: true),
            Release("server-v0.3.0-alpha", prerelease: true),
            Release("server-v0.3.1-alpha", prerelease: true)));

        var offer = ReleaseFeed.FindUpdate(releases, V("0.2.1"), includePreReleases: true);

        Assert.NotNull(offer);
        Assert.Equal("0.3.1-alpha", offer.Latest.Version.ToString());
        Assert.Equal(["0.3.1-alpha", "0.3.0-alpha"], Versions(offer.Included));
    }

    [Fact]
    public void FindUpdate_never_offers_the_same_or_an_older_version()
    {
        var releases = ReleaseFeed.Parse(Feed(Release("server-v0.2.1-alpha", prerelease: true), Release("server-v0.2.0-alpha", prerelease: true)));

        Assert.Null(ReleaseFeed.FindUpdate(releases, V("0.2.1"), true));
        Assert.Null(ReleaseFeed.FindUpdate(releases, V("0.3.0"), true));
    }

    [Fact]
    public void FindUpdate_ignores_pre_releases_when_the_channel_is_off()
    {
        var releases = ReleaseFeed.Parse(Feed(
            Release("server-v0.3.0-alpha", prerelease: true),
            Release("server-v0.3.5", prerelease: true), // flagged by GitHub even without a label
            Release("server-v0.3.0")));

        var offer = ReleaseFeed.FindUpdate(releases, V("0.2.1"), includePreReleases: false);

        Assert.NotNull(offer);
        Assert.Equal("0.3.0", offer.Latest.Version.ToString());
        Assert.Equal(["0.3.0"], Versions(offer.Included));
        Assert.Null(ReleaseFeed.FindUpdate(releases.Where(r => r.IsPreRelease), V("0.2.1"), false));
    }

    [Fact]
    public async Task Fetch_returns_body_and_etag()
    {
        var handler = new FakeHttpHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
            response.Headers.ETag = new EntityTagHeaderValue("\"abc\"");
            return response;
        });

        var result = await new ReleaseFeed(new HttpClient(handler), Endpoint).FetchAsync(null, CancellationToken.None);

        Assert.False(result.NotModified);
        Assert.Equal("[]", result.Body);
        Assert.Equal("\"abc\"", result.ETag);
        Assert.Empty(handler.Last!.Headers.IfNoneMatch);
    }

    [Fact]
    public async Task Fetch_sends_the_etag_and_treats_304_as_not_modified()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotModified));

        var result = await new ReleaseFeed(new HttpClient(handler), Endpoint).FetchAsync("\"abc\"", CancellationToken.None);

        Assert.True(result.NotModified);
        Assert.Equal("\"abc\"", result.ETag);
        Assert.Equal("\"abc\"", handler.Last!.Headers.IfNoneMatch.Single().ToString());
    }

    [Fact]
    public async Task Fetch_throws_when_rate_limited()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseFeed(new HttpClient(handler), Endpoint).FetchAsync(null, CancellationToken.None));
    }
}
