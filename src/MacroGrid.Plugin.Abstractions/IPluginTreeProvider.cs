using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>Optional: implemented by a plugin's <see cref="IPlugin"/> class to list its own items (sounds, scenes,
/// saved presets, ...) as a tree in the editor's Plugins tool window. A plugin that does not implement it is
/// unaffected and simply has no expandable row there. The host asks for one level at a time, only when the user
/// expands that node (and never before the Plugins tool window is first shown), with a short timeout, so a slow
/// or large source never costs anything up front.</summary>
public interface IPluginTreeProvider
{
    /// <summary>The children of <paramref name="parentId"/> (null: the plugin's top level). Called only when the
    /// user expands that node. Return at most 500 items per call; a source with more returns a
    /// <see cref="PluginTreePage.ContinuationToken"/>, and the host passes it back as
    /// <paramref name="continuationToken"/> when the user asks for the next page. The host cancels
    /// <paramref name="cancellationToken"/> after 2 seconds or when the editor gives up on the request.</summary>
    Task<PluginTreePage> GetTreeItemsAsync(string? parentId, string? continuationToken, CancellationToken cancellationToken);

    /// <summary>Raise when the children of a node changed (null: the top level). The host reloads that level
    /// only if the editor has it on screen, and otherwise just forgets its cached copy.</summary>
    event Action<string?>? TreeItemsChanged;
}

/// <summary>One node of a plugin's tree. <paramref name="Id"/> is the plugin's own stable id for it (unique within
/// the plugin), and is what <see cref="IPluginTreeProvider.GetTreeItemsAsync"/> and
/// <see cref="IPluginTreeItemSettings"/> receive back. <paramref name="Label"/> and <paramref name="Tooltip"/> go
/// through the plugin's translation table like its other text. <paramref name="Icon"/> is an icon name from the
/// editor's icon set (the same names a status item uses); an unknown name falls back to a plain dot.
/// <paramref name="HasChildren"/> gives the row a chevron. <paramref name="HasSettings"/> says that selecting it
/// shows its own settings form (see <see cref="IPluginTreeItemSettings"/>) in the Properties panel.</summary>
public sealed record PluginTreeItem(
    string Id,
    string Label,
    string? Icon = null,
    bool HasChildren = false,
    string? Tooltip = null,
    bool HasSettings = false);

/// <summary>One page of children. <paramref name="ContinuationToken"/> is null when there are no more.</summary>
public sealed record PluginTreePage(IReadOnlyList<PluginTreeItem> Items, string? ContinuationToken = null);

/// <summary>Optional side interface of an <see cref="IPluginTreeProvider"/> (the same pattern as
/// <see cref="ISettingsCommandHandler"/> on a settings page): an item's own settings, drawn by the host in the
/// editor's Properties panel from <see cref="GetItemFields"/> with the same schema-driven form a plugin's settings
/// window uses. Only called for items whose <see cref="PluginTreeItem.HasSettings"/> is true. A provider that
/// also implements <see cref="IOptionsSource"/> serves the dynamic dropdowns of these forms.</summary>
public interface IPluginTreeItemSettings
{
    IReadOnlyList<SettingField> GetItemFields(string itemId);

    JsonObject LoadItem(string itemId);

    void SaveItem(string itemId, JsonObject values);
}
