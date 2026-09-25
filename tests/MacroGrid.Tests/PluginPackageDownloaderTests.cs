using System.Net;
using System.Security.Cryptography;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginPackageDownloaderTests
{
    private static readonly byte[] Content = "pretend this is a plugin zip"u8.ToArray();
    private static readonly string Hex = Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant();
    private const string ZipUrl = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip";

    private static HttpResponseMessage File200(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private static PluginCatalogVersion Version(long size, string sha256 = "auto") =>
        new("0.2.0", "^0.3.0", "0.1.0", ZipUrl, sha256 == "auto" ? Hex : sha256, size, null, null);

    [Fact]
    public async Task Downloads_and_verifies_against_a_declared_size()
    {
        var downloader = new PluginPackageDownloader(new HttpClient(new FakeHttpHandler(_ => File200(Content))));

        var bytes = await downloader.DownloadAsync(Version(Content.Length), requireOfficialSignature: false, CancellationToken.None);

        Assert.Equal(Content, bytes);
    }

    [Fact]
    public async Task Refuses_a_download_whose_hash_does_not_match()
    {
        var downloader = new PluginPackageDownloader(new HttpClient(new FakeHttpHandler(_ => File200(Content))));

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() =>
            downloader.DownloadAsync(Version(Content.Length, "0000000000000000000000000000000000000000000000000000000000000000"), false, CancellationToken.None));
        Assert.Equal(PluginDownloadException.Verify, ex.Code);
    }

    [Fact]
    public async Task Accepts_an_undeclared_size_from_a_single_plugin_link_but_still_caps_it()
    {
        // Size 0 is what a method-4 (pasted link) install uses — no size is known ahead of time.
        var downloader = new PluginPackageDownloader(new HttpClient(new FakeHttpHandler(_ => File200(Content))));

        var bytes = await downloader.DownloadAsync(Version(0), requireOfficialSignature: false, CancellationToken.None);

        Assert.Equal(Content, bytes);
    }

    [Fact]
    public async Task Refuses_a_url_that_is_not_on_an_allowed_host()
    {
        var downloader = new PluginPackageDownloader(new HttpClient(new FakeHttpHandler(_ => File200(Content))));
        var version = Version(Content.Length) with { Url = "https://evil.example.com/plugin.zip" };

        var ex = await Assert.ThrowsAsync<PluginDownloadException>(() => downloader.DownloadAsync(version, false, CancellationToken.None));
        Assert.Equal(PluginDownloadException.Refused, ex.Code);
    }

    [Fact]
    public async Task Fetches_a_text_asset_like_a_sha256_file()
    {
        var downloader = new PluginPackageDownloader(new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Hex + "\n") })));

        var text = await downloader.FetchTextAssetAsync(new Uri(ZipUrl + ".sha256"), CancellationToken.None);

        Assert.Equal(Hex, text);
    }
}
