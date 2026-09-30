using System.Net;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginCatalogIconsTests
{
    private const string Package = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static PluginCatalogEntry Entry(string? icon, string url = Package) => new(
        "obs", "OBS", null, null, null, "csharp",
        [new PluginCatalogVersion("0.2.0", "1.0.0", null, null, null, url, "aa", 10, [], null)])
    { Icon = icon };

    private static PluginCatalogIcons Icons(Func<HttpRequestMessage, HttpResponseMessage> respond, out FakeHttpHandler handler)
    {
        handler = new FakeHttpHandler(respond);
        return new PluginCatalogIcons(new PluginPackageDownloader(new HttpClient(handler)));
    }

    private static HttpResponseMessage Bytes(byte[] data) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };

    [Fact]
    public async Task A_png_icon_is_fetched_from_the_release_of_the_newest_version_and_cached()
    {
        var icons = Icons(_ => Bytes(Png), out var handler);

        var first = await icons.GetAsync("Deccoyi", "macro-grid-plugin", Entry("obs-0.2.0.icon.png"), CancellationToken.None);
        await icons.GetAsync("Deccoyi", "macro-grid-plugin", Entry("obs-0.2.0.icon.png"), CancellationToken.None);

        Assert.Equal("image/png", first!.Value.ContentType);
        Assert.Equal("https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.icon.png", handler.Last!.RequestUri!.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task An_svg_is_accepted_and_anything_else_is_refused()
    {
        var svg = Icons(_ => Bytes("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray()), out _);
        var html = Icons(_ => Bytes("<html><script>alert(1)</script></html>"u8.ToArray()), out _);

        Assert.Equal("image/svg+xml", (await svg.GetAsync("Deccoyi", "macro-grid-plugin", Entry("a.svg"), CancellationToken.None))!.Value.ContentType);
        Assert.Null(await html.GetAsync("Deccoyi", "macro-grid-plugin", Entry("a.png"), CancellationToken.None));
    }

    [Fact]
    public async Task Nothing_is_fetched_for_an_entry_without_an_icon_or_with_a_download_from_another_repository()
    {
        var icons = Icons(_ => Bytes(Png), out var handler);

        Assert.Null(await icons.GetAsync("Deccoyi", "macro-grid-plugin", Entry(null), CancellationToken.None));
        Assert.Null(await icons.GetAsync("Deccoyi", "macro-grid-plugin", Entry("a.png", "https://github.com/Evil/other/releases/download/t/x.zip"), CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task A_download_larger_than_the_limit_gives_nothing()
    {
        var icons = Icons(_ => Bytes(new byte[PluginCatalogIcons.MaxBytes + 10]), out _);

        Assert.Null(await icons.GetAsync("Deccoyi", "macro-grid-plugin", Entry("a.png"), CancellationToken.None));
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("obs-0.2.0.icon.svg", true)]
    [InlineData("../a.png", false)]
    [InlineData("a/b.png", false)]
    [InlineData("a.exe", false)]
    [InlineData("", false)]
    public void Only_a_plain_png_or_svg_file_name_is_accepted_in_the_index(string name, bool expected) =>
        Assert.Equal(expected, CatalogText.IsIconFileName(name));
}
