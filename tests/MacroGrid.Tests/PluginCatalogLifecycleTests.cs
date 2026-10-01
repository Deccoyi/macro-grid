using System.Net;
using System.Security.Cryptography;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginCatalogVersionFieldsTests
{
    private const string Own = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/x-1.zip";

    private static string Index(string versionExtra) => $$"""
        { "formatVersion": 2, "name": "N", "plugins": [ { "id": "x", "name": "X", "kind": "js",
          "versions": [ { "version": "1.0.0", "minMacroGrid": "1.0.0", "sha256": "aa", "size": 5 {{versionExtra}} } ] } ] }
        """;

    private static PluginCatalogVersion Version(string extra, bool signed = false) =>
        PluginCatalogClient.Parse(Index(extra), "Deccoyi", "macro-grid-plugin", signed).Plugins[0].Versions[0];

    [Fact]
    public void Withdrawn_is_read_and_defaults_to_false()
    {
        Assert.True(Version($$""", "url": "{{Own}}", "withdrawn": true""").Withdrawn);
        Assert.False(Version($$""", "url": "{{Own}}" """).Withdrawn);
        Assert.False(Version($$""", "url": "{{Own}}", "withdrawn": "yes" """).Withdrawn);
    }

    [Fact]
    public void A_list_of_addresses_is_read_capped_at_three_and_url_may_be_missing()
    {
        var v = Version($$""", "urls": ["{{Own}}", "{{Own}}?a=1", "{{Own}}?a=2", "{{Own}}?a=3"]""");

        Assert.Equal(3, v.Addresses.Count);
        Assert.Equal(Own, v.Url);
    }

    [Fact]
    public void Without_a_list_the_single_url_is_the_only_address()
    {
        Assert.Equal([Own], Version($$""", "url": "{{Own}}" """).Addresses);
    }

    [Fact]
    public void A_version_with_neither_url_nor_urls_is_invalid()
    {
        Assert.Throws<PluginCatalogException>(() => Version(""));
    }

    [Fact]
    public void A_foreign_address_is_refused_in_a_plain_index_and_accepted_in_a_signed_one()
    {
        const string mirror = "https://mirror.example.org/x-1.zip";

        Assert.Throws<PluginCatalogException>(() => Version($$""", "urls": ["{{Own}}", "{{mirror}}"]"""));
        Assert.Equal(mirror, Version($$""", "urls": ["{{Own}}", "{{mirror}}"]""", signed: true).Addresses[1]);
        Assert.Throws<PluginCatalogException>(() => Version(""", "urls": ["http://mirror.example.org/x.zip"]""", signed: true));
    }
}

public sealed class PluginMirrorDownloadTests
{
    private static readonly byte[] Content = "pretend this is a plugin zip"u8.ToArray();
    private static readonly byte[] Other = "something else entirely!!!!!"u8.ToArray();
    private static readonly string Hex = Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant();
    private const string Own = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/x-1.zip";
    private const string Own2 = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/v1/x-1b.zip";
    private const string Mirror = "https://mirror.example.org/x-1.zip";

    private static HttpResponseMessage Ok(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private static PluginCatalogVersion Version(string? signature, params string[] urls) =>
        new("1.0.0", "1.0.0", null, null, null, urls[0], Hex, Content.Length, null, signature, Urls: urls);

    [Fact]
    public async Task The_next_address_is_used_when_the_first_fails()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.AbsolutePath.EndsWith("x-1.zip") ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(Content));

        var bytes = await new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version(null, Own, Own2), false, CancellationToken.None);

        Assert.Equal(Content, bytes);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task The_next_address_is_used_when_the_first_returns_the_wrong_hash()
    {
        var handler = new FakeHttpHandler(r => Ok(r.RequestUri!.AbsolutePath.EndsWith("x-1.zip") ? Other[..Content.Length] : Content));

        var bytes = await new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version(null, Own, Own2), false, CancellationToken.None);

        Assert.Equal(Content, bytes);
    }

    [Fact]
    public async Task A_mirror_is_not_tried_when_the_official_signature_is_not_required()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "github.com" ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(Content));

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version(null, Own, Mirror), false, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Refused, ex.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task A_mirror_whose_package_has_no_valid_official_signature_is_refused()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "github.com" ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(Content));
        using var other = TestPluginSigning.NewOtherKey();
        var signature = Convert.ToBase64String(other.SignData(Content, HashAlgorithmName.SHA256));

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version(signature, Own, Mirror), true, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Signature, ex.Code);
    }

    [Fact]
    public async Task A_redirect_from_a_mirror_to_a_third_host_is_refused()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "mirror.example.org"
            ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://third.example.net/x.zip") } }
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version("c2ln", Mirror), true, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Refused, ex.Code);
        Assert.Equal("mirror.example.org", handler.Last!.RequestUri!.Host);
    }

    [Fact]
    public async Task A_redirect_inside_the_mirror_host_is_followed()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.AbsolutePath == "/x-1.zip"
            ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://mirror.example.org/cdn/x-1.zip") } }
            : Ok(Content));

        // The real official key cannot be used to sign in a test, so reaching the signature check proves the bytes arrived over the redirect.
        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            new PluginPackageDownloader(new HttpClient(handler)).DownloadAsync(Version("c2ln", Mirror), true, CancellationToken.None));

        Assert.Equal(PluginDownloadException.Signature, ex.Code);
        Assert.Equal("/cdn/x-1.zip", handler.Last!.RequestUri!.AbsolutePath);
    }
}

public sealed class PluginCatalogChoiceTests
{
    private static PluginCatalogVersion V(string version, bool withdrawn = false, string min = "1.0.0") =>
        new(version, min, null, null, null, "https://github.com/a/b/releases/download/v/x.zip", "aa", 1, null, null, withdrawn);

    private static PluginCatalogEntry Entry(params PluginCatalogVersion[] versions) => new("x", "X", null, null, null, "js", versions);

    [Fact]
    public void The_newest_version_is_chosen_normally()
    {
        var c = PluginCatalogChoice.Choose(Entry(V("1.0.0"), V("1.1.0")), "1.5.0", null);

        Assert.Equal("1.1.0", c.Installable!.Version);
        Assert.False(c.Withdrawn);
    }

    [Fact]
    public void When_the_newest_is_withdrawn_the_one_before_is_offered()
    {
        var c = PluginCatalogChoice.Choose(Entry(V("1.0.0"), V("1.1.0", withdrawn: true)), "1.5.0", null);

        Assert.Equal("1.0.0", c.Installable!.Version);
        Assert.Equal("1.0.0", c.Latest!.Version);
        Assert.False(c.Withdrawn);
    }

    [Fact]
    public void When_every_version_is_withdrawn_nothing_is_offered()
    {
        var c = PluginCatalogChoice.Choose(Entry(V("1.0.0", true), V("1.1.0", true)), "1.5.0", null);

        Assert.Null(c.Installable);
        Assert.Null(c.Latest);
        Assert.True(c.Withdrawn);
    }

    [Fact]
    public void An_installed_version_that_is_withdrawn_is_reported()
    {
        var entry = Entry(V("1.0.0", withdrawn: true), V("1.1.0"));

        Assert.True(PluginCatalogChoice.Choose(entry, "1.5.0", "1.0.0").InstalledWithdrawn);
        Assert.False(PluginCatalogChoice.Choose(entry, "1.5.0", "1.1.0").InstalledWithdrawn);
    }

    [Fact]
    public void A_revoked_version_is_not_offered_either()
    {
        var c = PluginCatalogChoice.Choose(Entry(V("1.0.0"), V("1.1.0")), "1.5.0", null, v => v.Version == "1.1.0");

        Assert.Equal("1.0.0", c.Installable!.Version);
    }

    [Fact]
    public void An_incompatible_newest_gives_a_reason_and_no_installable_version()
    {
        var c = PluginCatalogChoice.Choose(Entry(V("1.0.0", min: "9.0.0")), "1.5.0", null);

        Assert.Null(c.Installable);
        Assert.NotNull(c.IncompatibleReason);
    }
}
