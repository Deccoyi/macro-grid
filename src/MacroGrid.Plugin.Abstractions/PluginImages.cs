namespace MacroGrid.Plugin.Abstractions;

/// <summary>
/// Implemented by the plugin class (the one implementing <see cref="IPlugin"/>) when it supplies pictures for buttons. A profile never holds the picture,
/// only the reference <c>plugin-image:&lt;pluginId&gt;/&lt;imageId&gt;</c>; when a button needs a picture the host does not have, it calls
/// <see cref="RequestImages"/> and the plugin answers with <see cref="IPluginImages.Put"/>, now or later.
/// </summary>
public interface IPluginImageSource
{
    /// <summary>The host wants these pictures. At most 20 ids per call and at most one call every 500 ms; an id is asked for again only after
    /// about 10 minutes without an answer. Called on a host thread; do not block it. An id the plugin does not know can simply be ignored.</summary>
    void RequestImages(IReadOnlyList<string> imageIds);
}

/// <summary>Where a plugin hands over the pictures the host asked for (see <see cref="IPluginImageSource"/>).</summary>
public interface IPluginImages
{
    /// <summary>Offers one picture. <paramref name="imageId"/> is 1 to 128 characters of letters, digits, '.', '_', ':' and '-'.
    /// <paramref name="dataUrl"/> is <c>data:image/png|jpeg|webp|gif;base64,...</c> and decodes to at most 256 KB whose first bytes match the type and whose header says at most 1024 x 1024 pixels
    /// (a GIF may have at most 64 frames; SVG is not accepted). The host only reads the header, it never decodes the picture, and metadata passes through unchanged. Only an id the host asked for in the last 10 minutes is accepted. Returns whether the picture was accepted; a refusal
    /// puts one line in the Error List and never throws. The optional <paramref name="version"/> (at most 64 printable characters) is kept with the picture. The host also asks again
    /// about once a week for a picture it still uses, so a changed picture is picked up.</summary>
    bool Put(string imageId, string dataUrl, string? version = null);

    /// <summary>Says the picture of <paramref name="imageId"/> changed. If it is cached, the host asks for it again (through <see cref="IPluginImageSource"/>) and the new picture replaces the old.
    /// With a <paramref name="version"/> equal to the one given to <see cref="Put"/> nothing happens. An id that is not cached is ignored: it is asked for when a button needs it.</summary>
    void Invalidate(string imageId, string? version = null);
}

/// <summary>An <see cref="IPluginImages"/> that accepts nothing; what a host that keeps no picture cache (a test double) hands out.</summary>
public sealed class NullPluginImages : IPluginImages
{
    public static readonly NullPluginImages Instance = new();

    private NullPluginImages() { }

    public bool Put(string imageId, string dataUrl, string? version = null) => false;

    public void Invalidate(string imageId, string? version = null) { }
}
