using System.Text.Json.Serialization;

namespace MacroStation.Plugin.Abstractions;

public enum PluginKind
{
    Csharp,
    Js,
}

/// <summary>
/// A plugin's <c>plugin.json</c>, one per plugin folder. Schema mirrors the "Plugin compatibility" section of
/// docs/versioning.md and the manifest table in the plugin repository's docs/plugin-authoring.md; keep them in
/// sync with this record if the schema changes (that is a MINOR/MAJOR host change, see versioning.md).
/// </summary>
public sealed record PluginManifest
{
    /// <summary>Unique, stable id (e.g. "obs", "audio"). The host refuses to load a second plugin with the same id.</summary>
    public required string Id { get; init; }

    /// <summary>Display name shown in the editor's plugin list.</summary>
    public required string Name { get; init; }

    /// <summary>The plugin's own semver — independent of the host's version (docs/versioning.md).</summary>
    public required string Version { get; init; }

    /// <summary>npm-style caret range against the Plugin SDK version, e.g. "^1.0.0".</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Minimum host (server) semver this plugin requires.</summary>
    public required string MinServerVersion { get; init; }

    /// <summary>Entry assembly file name for a csharp plugin (e.g. "Obs.Plugin.dll"). Entry script for js (not yet supported by the loader).</summary>
    public required string Entry { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<PluginKind>))]
    public required PluginKind Kind { get; init; }

    /// <summary>JS plugins only — permission strings like "variables", "actions", "http:localhost:4455". Ignored for csharp plugins.</summary>
    public string[]? Permissions { get; init; }
}
