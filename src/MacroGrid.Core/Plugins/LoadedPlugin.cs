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
    /// <summary>A C# plugin that is not officially signed, or whose files no longer match its signature. It never runs (the
    /// reason is in <see cref="LoadedPlugin.Detail"/>); the person can only remove it.</summary>
    NotAllowed,
}

/// <summary>One entry in the editor's plugin list (`GET /api/plugins`) — every folder under `plugins/`
/// that had a plugin.json, whether or not it actually loaded. <paramref name="HasSettings"/> is true
/// when the plugin registered an <see cref="MacroGrid.Plugin.Abstractions.IPluginSettingsPage"/>,
/// which is what the editor's gear button checks before opening the settings window.
/// <paramref name="HasIcon"/> is true when the manifest's optional <c>icon</c> path resolved to a valid
/// file (see PluginManager.ResolveIconPath) — the editor then fetches it from <c>GET /api/plugins/{id}/icon</c>.
/// <paramref name="Unsigned"/> is true only in a development build, for a C# plugin that loaded without a valid signature.
/// <paramref name="KeyboardUsesToday"/> is how many button presses used the keyboard through a JavaScript plugin today.
/// <paramref name="HasTreeItems"/> is true when the running plugin implements the optional
/// <see cref="MacroGrid.Plugin.Abstractions.IPluginTreeProvider"/>: only then does the editor's Plugins tool window
/// give it a chevron and ask <c>GET /api/plugins/{id}/tree-items</c> for anything.</summary>
public sealed record LoadedPlugin(string Id, string Name, string Version, PluginLoadStatus Status, string? Detail, bool HasSettings = false, IReadOnlyList<string>? PendingPermissions = null, bool HasIcon = false, bool HasTreeItems = false, bool Unsigned = false, int KeyboardUsesToday = 0, IReadOnlyList<string>? Permissions = null, IReadOnlyList<string>? SwitchedOffPermissions = null);
