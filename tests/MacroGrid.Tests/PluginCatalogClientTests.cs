using System.Net;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginCatalogClientTests
{
    private const string Owner = "Deccoyi";
    private const string Repo = "macro-grid-plugin";

    private static HttpResponseMessage Text200(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static string ValidIndexJson(string url = "https://github.com/Deccoyi/macro-grid-plugin/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip") => $$"""
        {
          "formatVersion": 1,
          "name": "Macro Grid Plugins",
          "author": "Deccoyi",
          "plugins": [
            {
              "id": "obs",
              "name": "OBS Control",
              "description": "Controls OBS.",
              "author": "Deccoyi",
              "homepage": "https://github.com/Deccoyi/macro-grid-plugin/tree/main/OBS",
              "kind": "csharp",
              "versions": [
                {
                  "version": "0.2.0",
                  "sdkVersion": "^0.3.0",
                  "minServerVersion": "0.1.0",
                  "url": "{{url}}",
                  "sha256": "deadbeef",
                  "size": 12345,
                  "permissions": [],
                  "signature": "c2ln"
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task Parses_a_valid_index()
    {
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "raw.githubusercontent.com" ? Text200(ValidIndexJson()) : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var index = await client.FetchIndexAsync(Owner, Repo, CancellationToken.None);

        Assert.Equal(1, index.FormatVersion);
        var plugin = Assert.Single(index.Plugins);
        Assert.Equal("obs", plugin.Id);
        var version = Assert.Single(plugin.Versions);
        Assert.Equal("0.2.0", version.Version);
        Assert.Equal("deadbeef", version.Sha256);
    }

    [Fact]
    public async Task Parses_a_format_2_index_with_macro_grid_and_no_legacy_fields()
    {
        var json = ValidIndexJson()
            .Replace("\"formatVersion\": 1", "\"formatVersion\": 2")
            .Replace("\"sdkVersion\": \"^0.3.0\",", "\"macroGrid\": \"1.0.0\",")
            .Replace("\"minServerVersion\": \"0.1.0\",", "");
        var handler = new FakeHttpHandler(r => r.RequestUri!.Host == "raw.githubusercontent.com" ? Text200(json) : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var index = await client.FetchIndexAsync(Owner, Repo, CancellationToken.None);

        var version = Assert.Single(Assert.Single(index.Plugins).Versions);
        Assert.Equal("1.0.0", version.MacroGrid);
        Assert.Null(version.SdkVersion);
        Assert.Null(version.MinServerVersion);
    }

    [Fact]
    public async Task Refuses_a_version_whose_url_points_at_a_different_repository()
    {
        var handler = new FakeHttpHandler(_ => Text200(ValidIndexJson("https://github.com/someone-else/other-repo/releases/download/v1/x.zip")));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<PluginCatalogException>(() => client.FetchIndexAsync(Owner, Repo, CancellationToken.None));
        Assert.Equal(PluginCatalogException.Invalid, ex.Code);
    }

    [Fact]
    public async Task Refuses_an_unsupported_format_version()
    {
        var handler = new FakeHttpHandler(_ => Text200("""{ "formatVersion": 3, "name": "x", "plugins": [] }"""));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<PluginCatalogException>(() => client.FetchIndexAsync(Owner, Repo, CancellationToken.None));
        Assert.Equal(PluginCatalogException.Invalid, ex.Code);
    }

    [Fact]
    public async Task Refuses_a_version_missing_sha256()
    {
        var withoutSha = ValidIndexJson().Replace("\"sha256\": \"deadbeef\",", "");
        var handler = new FakeHttpHandler(_ => Text200(withoutSha));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<PluginCatalogException>(() => client.FetchIndexAsync(Owner, Repo, CancellationToken.None));
        Assert.Equal(PluginCatalogException.Invalid, ex.Code);
    }

    [Fact]
    public async Task Refuses_a_response_larger_than_the_cap()
    {
        var handler = new FakeHttpHandler(_ => Text200(new string('x', 2 * 1024 * 1024)));
        var client = new PluginCatalogClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<PluginCatalogException>(() => client.FetchIndexAsync(Owner, Repo, CancellationToken.None));
        Assert.Equal(PluginCatalogException.TooLarge, ex.Code);
    }
}
