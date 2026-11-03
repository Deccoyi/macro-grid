using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginIconResponseTests
{
    [Fact]
    public void Policy_sandboxes_and_blocks_everything_but_inline_styles()
    {
        Assert.Equal("default-src 'none'; style-src 'unsafe-inline'; sandbox", PluginIconResponse.ContentSecurityPolicy);
        Assert.Equal("nosniff", PluginIconResponse.ContentTypeOptions);
        Assert.Equal("same-origin", PluginIconResponse.CrossOriginResourcePolicy);
    }

    [Fact]
    public void The_shared_helper_sets_all_three_headers()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "MacroGrid.slnx"))) root = Path.GetDirectoryName(root);
        var text = File.ReadAllText(Path.Combine(root!, "src/MacroGrid.Host/Api/ApiResults.cs"));
        var start = text.IndexOf("void SandboxIcon", StringComparison.Ordinal);
        var body = text[start..text.IndexOf("}", start, StringComparison.Ordinal)];
        Assert.Contains("ContentSecurityPolicy = PluginIconResponse.ContentSecurityPolicy", body);
        Assert.Contains("XContentTypeOptions = PluginIconResponse.ContentTypeOptions", body);
        Assert.Contains("\"Cross-Origin-Resource-Policy\"] = PluginIconResponse.CrossOriginResourcePolicy", body);
    }

    // Every icon endpoint must call SandboxIcon before returning the file (the Host project has no endpoint test harness).
    [Theory]
    [InlineData("src/MacroGrid.Host/Api/PluginApi.cs", "/plugins/{id}/icon")]
    [InlineData("src/MacroGrid.Host/Api/PluginApi.cs", "/icon-packs/{packId}/{iconName}")]
    [InlineData("src/MacroGrid.Host/Api/PluginApi.cs", "/plugins/{pluginId}/images/{imageId}")]
    [InlineData("src/MacroGrid.Host/Api/PluginCatalogApi.cs", "/plugin-catalog/icon")]
    public void Icon_endpoints_apply_the_headers(string file, string route)
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "MacroGrid.slnx"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var text = File.ReadAllText(Path.Combine(root, file));
        var start = text.IndexOf(route, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = text.IndexOf("MapGet", start, StringComparison.Ordinal);
        var body = end < 0 ? text[start..] : text[start..end];
        Assert.Contains("ApiResults.SandboxIcon(context)", body);
    }
}
