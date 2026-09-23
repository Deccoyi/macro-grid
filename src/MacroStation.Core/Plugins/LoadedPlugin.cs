using System.Text.Json.Serialization;

namespace MacroStation.Core.Plugins;

[JsonConverter(typeof(JsonStringEnumConverter<PluginLoadStatus>))]
public enum PluginLoadStatus
{
    Loaded,
    Incompatible,
    Error,
    /// <summary>A JS plugin that declares permissions the user has not approved yet; it does not run until they do.</summary>
    NeedsApproval,
}

/// <summary>One entry in the editor's plugin list (`GET /api/plugins`) — every folder under `plugins/`
/// that had a plugin.json, whether or not it actually loaded. <paramref name="HasSettings"/> is true
/// when the plugin registered an <see cref="MacroStation.Plugin.Abstractions.IPluginSettingsPage"/>,
/// which is what the editor's gear button checks before opening the settings window.</summary>
public sealed record LoadedPlugin(string Id, string Name, string Version, PluginLoadStatus Status, string? Detail, bool HasSettings = false, IReadOnlyList<string>? PendingPermissions = null);
