using System.Net;
using System.Security.Cryptography;
using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class InstallerDownloaderTests : IDisposable
{
    private static readonly byte[] Content = "pretend this is an installer"u8.ToArray();
    private static readonly string Hex = Convert.ToHexString(SHA256.HashData(Content)).ToLowerInvariant();
    private const string AssetUrl = "https://github.com/Deccoyi/macro-grid/releases/download/server-v0.3.0/MacroGrid-Setup-0.3.0.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static ReleaseInfo Release(string url = AssetUrl, string? sha = "auto", long size = -1)
    {
        var asset = new ReleaseAsset("MacroGrid-Setup-0.3.0.exe", new Uri(url), size < 0 ? Content.Length : size, sha == "auto" ? Hex : sha);
        return new ReleaseInfo(new ReleaseVersion(0, 3, 0), "server-v0.3.0", "0.3.0", "", null, null, asset);
    }

    private static HttpResponseMessage File200(byte[] content) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };

    private static HttpResponseMessage Redirect(string to) => new(HttpStatusCode.Found) { Headers = { Location = new Uri(to) } };

    private static InstallerDownloader Downloader(FakeHttpHandler handler) => new(new HttpClient(handler));

    private string Target => Path.Combine(_root, "0.3.0", "MacroGrid-Setup-0.3.0.exe");

    [Fact]
    public async Task Downloads_verifies_and_reports_progress()
    {
        var handler = new FakeHttpHandler(_ => File200(Content));
        var reported = new List<double>();

        var path = await Downloader(handler).DownloadAsync(Release(), _root, new Progress<double>(reported.Add), CancellationToken.None);
        await Task.Delay(50); // Progress<T> posts to the pool

        Assert.Equal(Target, path);
        Assert.Equal(Content, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(path + ".part"));
        Assert.Contains(1.0, reported);
    }

    [Fact]
    public async Task Follows_redirects_to_an_allowed_host()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "github.com"
            ? Redirect("https://release-assets.githubusercontent.com/blob/abc")
            : File200(Content));

        var path = await Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None);

        Assert.True(File.Exists(path));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Refuses_a_redirect_to_another_host()
    {
        var handler = new FakeHttpHandler(_ => Redirect("https://evil.example/installer.exe"));

        var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Refused, ex.Code);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Refuses_a_redirect_to_plain_http()
    {
        var handler = new FakeHttpHandler(_ => Redirect("http://github.com/x.exe"));

        var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Refused, ex.Code);
    }

    [Fact]
    public async Task Refuses_a_release_that_starts_outside_the_allow_list_or_has_no_digest()
    {
        var handler = new FakeHttpHandler(_ => File200(Content));

        var outside = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(handler).DownloadAsync(Release(url: "https://evil.example/a.exe"), _root, null, CancellationToken.None));
        var noDigest = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(handler).DownloadAsync(Release(sha: null), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Refused, outside.Code);
        Assert.Equal(UpdateDownloadException.Refused, noDigest.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Digest_mismatch_deletes_the_file_and_fails()
    {
        var tampered = new byte[Content.Length];
        var handler = new FakeHttpHandler(_ => File200(tampered));

        var ex = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Verify, ex.Code);
        Assert.False(File.Exists(Target));
        Assert.False(File.Exists(Target + ".part"));
    }

    [Fact]
    public async Task A_download_larger_or_smaller_than_the_release_says_fails()
    {
        var bigger = new FakeHttpHandler(_ => File200([.. Content, 1, 2, 3]));
        var smaller = new FakeHttpHandler(_ => File200(Content[..5]));

        var ex1 = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(bigger).DownloadAsync(Release(), _root, null, CancellationToken.None));
        var ex2 = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(smaller).DownloadAsync(Release(), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Verify, ex1.Code);
        Assert.Equal(UpdateDownloadException.Verify, ex2.Code);
        Assert.False(File.Exists(Target + ".part"));
    }

    [Fact]
    public async Task Network_and_http_errors_are_reported_as_failed()
    {
        var offline = new FakeHttpHandler(_ => throw new HttpRequestException("offline"));
        var missing = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex1 = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(offline).DownloadAsync(Release(), _root, null, CancellationToken.None));
        var ex2 = await Assert.ThrowsAsync<UpdateDownloadException>(() => Downloader(missing).DownloadAsync(Release(), _root, null, CancellationToken.None));

        Assert.Equal(UpdateDownloadException.Failed, ex1.Code);
        Assert.Equal(UpdateDownloadException.Failed, ex2.Code);
    }

    [Fact]
    public async Task An_already_verified_file_is_reused_without_downloading()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllBytesAsync(Target, Content);
        var handler = new FakeHttpHandler(_ => File200(Content));

        var path = await Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None);

        Assert.Equal(Target, path);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_corrupt_leftover_file_is_downloaded_again()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Target)!);
        await File.WriteAllBytesAsync(Target, [1, 2, 3]);
        var handler = new FakeHttpHandler(_ => File200(Content));

        var path = await Downloader(handler).DownloadAsync(Release(), _root, null, CancellationToken.None);

        Assert.Equal(Content, await File.ReadAllBytesAsync(path));
        Assert.Equal(1, handler.Calls);
    }
}
