using System.Text.Json.Serialization;

namespace MacroGrid.Core.Plugins;

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
/// when the plugin registered an <see cref="MacroGrid.Plugin.Abstractions.IPluginSettingsPage"/>,
/// which is what the editor's gear button checks before opening the settings window.
/// <paramref name="HasIcon"/> is true when the manifest's optional <c>icon</c> path resolved to a valid
/// file (see PluginManager.ResolveIconPath) — the editor then fetches it from <c>GET /api/plugins/{id}/icon</c>.</summary>
public sealed record LoadedPlugin(string Id, string Name, string Version, PluginLoadStatus Status, string? Detail, bool HasSettings = false, IReadOnlyList<string>? PendingPermissions = null, bool HasIcon = false);
