namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// Response headers for plugin icons. An icon can be an author-written SVG served from the editor's own origin; inside an
/// <c>img</c> it runs nothing, but opened directly as a URL a script in it would. The sandbox policy and <c>nosniff</c> stop that.
/// </summary>
public static class PluginIconResponse
{
    public const string ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
    public const string ContentTypeOptions = "nosniff";
}
