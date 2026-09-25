using System.Text.Json.Serialization;

namespace MacroGrid.Plugin.Abstractions;

public enum PluginKind
{
    Csharp,
    Js,
}

/// <summary>
/// A plugin's <c>plugin.json</c>, one per plugin folder. Schema mirrors the "Plugin compatibility" section of
/// docs/guides/versioning.md and the manifest table in the plugin repository's docs/plugin-authoring.md; keep them in
/// sync with this record if the schema changes (that is a MINOR/MAJOR host change, see versioning.md).
/// </summary>
public sealed record PluginManifest
{
    /// <summary>Unique, stable id (e.g. "demo", "audio"). The host refuses to load a second plugin with the same id.</summary>
    public required string Id { get; init; }

    /// <summary>Display name shown in the editor's plugin list.</summary>
    public required string Name { get; init; }

    /// <summary>The plugin's own semver — independent of the host's version (docs/guides/versioning.md).</summary>
    public required string Version { get; init; }

    /// <summary>npm-style caret range against the Plugin SDK version, e.g. "^1.0.0".</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Minimum host (server) semver this plugin requires.</summary>
    public required string MinServerVersion { get; init; }

    /// <summary>Entry assembly file name for a csharp plugin (e.g. "Demo.Plugin.dll"). Entry script for js (not yet supported by the loader).</summary>
    public required string Entry { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<PluginKind>))]
    public required PluginKind Kind { get; init; }

    /// <summary>Language the plugin's own texts (action names, descriptions, form labels, ...) are written in, e.g. "en". Defaults to "en".
    /// Other languages come from <c>locales/&lt;language&gt;.json</c> next to plugin.json (default-language text to translation); a
    /// language without a file falls back to the texts as written.</summary>
    public string? DefaultLanguage { get; init; }

    /// <summary>JS plugins only — permission strings like "variables", "actions", "http:localhost:4455". Ignored for csharp plugins.</summary>
    public string[]? Permissions { get; init; }

    /// <summary>Optional path (relative to the plugin folder, e.g. "icon.svg") to a small square logo shown
    /// in the editor's Plugins window and the Store instead of the generic category glyph. Additive: an
    /// older host ignores it. Must be .svg or .png, at most 100 KB — square, roughly 256x256 recommended for
    /// .png (the host does not decode the image to check pixel size, only the file extension and size, so a
    /// too-large or wrong-shaped picture is a plugin-author mistake, not a load failure: the host just falls
    /// back to no icon for it, logged as a warning). A missing or invalid path is the same as not set.</summary>
    public string? Icon { get; init; }
}
