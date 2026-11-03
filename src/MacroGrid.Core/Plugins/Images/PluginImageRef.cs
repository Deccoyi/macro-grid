using System.Text.RegularExpressions;
using MacroGrid.Core.Model;

namespace MacroGrid.Core.Plugins.Images;

/// <summary>The reference a profile stores instead of a plugin's picture: <c>plugin-image:&lt;pluginId&gt;/&lt;imageId&gt;</c>. The server turns it into the
/// picture when it sends a layout or a widget state; a device never sees it.</summary>
public static partial class PluginImageRef
{
    public const string Prefix = "plugin-image:";

    public const int MaxImageIdLength = 128;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex PluginIdPattern();

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,128}$")]
    private static partial Regex ImageIdPattern();

    public static bool IsValidPluginId(string? id) => id is not null && PluginIdPattern().IsMatch(id);

    public static bool IsValidImageId(string? id) => id is not null && ImageIdPattern().IsMatch(id);

    /// <summary>True when <paramref name="value"/> starts like a reference, valid or not.</summary>
    public static bool LooksLikeRef(string? value) => value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Make(string pluginId, string imageId) => $"{Prefix}{pluginId}/{imageId}";

    public static bool TryParse(string? value, out string pluginId, out string imageId)
    {
        pluginId = imageId = "";
        if (!LooksLikeRef(value)) return false;
        var rest = value![Prefix.Length..];
        var slash = rest.IndexOf('/');
        if (slash <= 0) return false;
        var plugin = rest[..slash];
        var image = rest[(slash + 1)..];
        if (!IsValidPluginId(plugin) || !IsValidImageId(image)) return false;
        pluginId = plugin;
        imageId = image;
        return true;
    }

    /// <summary>Every valid reference a widget can show as its icon: its own <c>style.icon</c> and the results (and default) of a dynamic <c>style.icon</c> binding.</summary>
    public static IEnumerable<(string PluginId, string ImageId)> Of(Widget widget)
    {
        if (TryParse(widget.Style?.Icon, out var plugin, out var image)) yield return (plugin, image);
        if (!widget.Dynamic.TryGetValue("style.icon", out var binding)) yield break;
        foreach (var value in binding.Cases.Select(c => c.Result).Append(binding.Default))
            if (TryParse(value, out plugin, out image)) yield return (plugin, image);
    }

    public static bool HasAny(Widget widget) => Of(widget).Any();
}
