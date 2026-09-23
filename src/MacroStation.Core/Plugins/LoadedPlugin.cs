using System.Text.Json.Serialization;

namespace MacroStation.Core.Plugins;

[JsonConverter(typeof(JsonStringEnumConverter<PluginLoadStatus>))]
public enum PluginLoadStatus
{
    Loaded,
    Incompatible,
    Error,
}

/// <summary>One entry in the editor's plugin list (`GET /api/plugins`) — every folder under `plugins/`
/// that had a plugin.json, whether or not it actually loaded.</summary>
public sealed record LoadedPlugin(string Id, string Name, string Version, PluginLoadStatus Status, string? Detail);
