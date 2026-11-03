using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>Checks what a script hands over as options (its static field options and the answers of <c>host.options.source</c>).
/// An option picture is only ever a reference or a built-in name, never data: a <c>plugin-image:&lt;pluginId&gt;/&lt;imageId&gt;</c> of the
/// plugin's own, or the name of a built-in icon.</summary>
internal static partial class JsOptions
{
    public const int MaxJsonChars = 1024 * 1024;
    public const int MaxValueChars = 512;
    public const int MaxLabelChars = 200;
    public const int MaxGroupChars = 100;
    public const int MaxErrorChars = 300;

    public static bool IsValidIcon(string? icon, string pluginId)
    {
        if (string.IsNullOrEmpty(icon)) return false;
        if (BuiltInIcon().IsMatch(icon)) return true;
        return ImageReference().Match(icon) is { Success: true } m && m.Groups[1].Value.Equals(pluginId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The option with a picture it may not have dropped (the option itself stays).</summary>
    public static SettingOption Sanitize(SettingOption option, string pluginId) =>
        option.Icon is not null && !IsValidIcon(option.Icon, pluginId) ? option with { Icon = null } : option;

    /// <summary>The same for a field and its list rows.</summary>
    public static SettingField Sanitize(SettingField field, string pluginId) => field with
    {
        Options = field.Options?.Select(o => Sanitize(o, pluginId)).ToArray(),
        ItemFields = field.ItemFields?.Select(f => Sanitize(f, pluginId)).ToArray(),
    };

    /// <summary>Reads the JSON an options source returned. Anything malformed becomes an error result, never an exception.</summary>
    public static OptionsResult Read(string? json, string pluginId)
    {
        if (json is null || json.Length > MaxJsonChars) return Fail("The options source gave an answer that is too large.");
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch (System.Text.Json.JsonException) { root = null; }
        if (root is null) return Fail("The options source gave an answer that is not valid.");

        if (root["error"] is JsonValue e && e.TryGetValue<string>(out var error) && error.Length > 0)
            return Fail(Cut(error, MaxErrorChars));

        var options = new List<SettingOption>();
        if (root["options"] is JsonArray array)
        {
            if (array.Count > OptionsPaging.MaxOptions) return Fail($"The options source returned more than {OptionsPaging.MaxOptions} options for one page.");
            foreach (var item in array)
            {
                if (item is not JsonObject o || Text(o["value"]) is not { Length: > 0 } value || value.Length > MaxValueChars) continue;
                var option = new SettingOption(value, Cut(Text(o["label"]) ?? value, MaxLabelChars), Text(o["group"]) is { Length: > 0 } g ? Cut(g, MaxGroupChars) : null, Text(o["icon"]));
                options.Add(Sanitize(option, pluginId));
            }
        }
        return new OptionsResult(options)
        {
            HasMore = Flag(root["more"]) == true,
            SelectedMissing = Flag(root["selectedMissing"]),
        };
    }

    public static OptionsResult Fail(string message) => new([], message);

    private static string? Text(JsonNode? node) =>
        node is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.TryGetValue<double>(out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : null) : null;

    private static bool? Flag(JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]{0,47}$")]
    private static partial Regex BuiltInIcon();

    [GeneratedRegex(@"^plugin-image:([A-Za-z0-9._-]{1,64})/([A-Za-z0-9._:-]{1,128})$")]
    private static partial Regex ImageReference();
}
