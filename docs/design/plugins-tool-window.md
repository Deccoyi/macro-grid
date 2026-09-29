# Plugins tool window

**Status:** built. A standalone dockable tool window (`editor/src/workspace/toolWindows.ts`, id `pluginsTree`, icon `Blocks`), a sibling
of Hierarchy, not a node inside it — the owner changed the placement `hierarchy-tree-and-folders.md`'s original design used (a "Plugins
node" inside the Hierarchy tree). **Repositories:** `macro-grid` (`editor/`, `src/MacroGrid.Core`, `src/MacroGrid.Host`,
`src/MacroGrid.Plugin.Abstractions`). Builds on [docking-workspace.md](docking-workspace.md) for the tool window mechanics and reuses
[hierarchy-tree-and-folders.md](hierarchy-tree-and-folders.md)'s lazy-loading rules for the tree itself. First use (SoundBoard) is in
`macro-grid-plugin`. **Changes the plugin SDK** (additive, optional interface): see `../architecture.md` and `../guides/versioning.md`.

## Goal

Every installed plugin gets one row; a plugin only gets a chevron when it opts in to listing its own items (sounds, scenes, saved
presets, ...) as a lazily-loaded tree. Selecting a row or an item shows its settings in the existing Properties tool window, through the
same schema-driven form a plugin's own settings page already uses — nothing new is built for rendering a settings form, only for
getting the right one there.

## What is shown

- One row per installed plugin (`api.listPlugins()`, the Plugins window's order — no re-sort), name and manifest icon if `hasIcon`.
  Plugins are not draggable and have no edit commands.
- **Plugins are not required to add anything to the tree.** A plugin only gets a chevron (expandable) when it implements the optional
  `IPluginTreeProvider` (`PluginInfo.hasTreeItems`). Others are a single row.
- Selecting a plugin row shows its own settings form in Properties if it has one (`hasSettings`, the same `IPluginSettingsPage` the
  Plugins management window's gear button opens); otherwise Properties shows just its name.
- Selecting a tree item shows its own settings form in Properties if `PluginTreeItem.hasSettings` is true (`IPluginTreeItemSettings`);
  a non-leaf structural node with no settings shows just its label.

## Loading

- **The tool window itself is lazy at the outermost level**: `PanelHost` only mounts a tool window's content while it is open, so
  nothing here runs until the person opens the Plugins tool window at least once in the session (it starts closed, reachable from the
  View menu — `defaultOpen: false`). No `api.listPlugins()` call happens before that.
- **Plugin tree items are loaded lazily, one level at a time, when that node is expanded**, never earlier:
  `GET /api/plugins/{id}/tree-items?parent=<itemId>` (no `parent` for the plugin's top level). While loading, the node shows one
  "Loading..." row; the rest of the tree stays usable.
- The server passes the call to the plugin with a 2 s timeout and a cancellation token (`PluginTreeReader`). A timeout or error shows
  one "Could not load. Retry" row under the node, never a dialog.
- At most 500 items per level; a provider that has more returns a continuation token and the tree shows a "Show more" row
  (`api.getPluginTreeItems(id, parentId, continuationToken)`).
- Loaded levels are cached in memory for the session (`editor/src/state/usePluginTree.ts`, mirrors `useProfileTree.ts`'s
  lazy-fetch-and-cache pattern). Collapsing keeps the cache. While the tool window is open, it polls
  `GET /api/plugins/tree-changes?since=<revision>` (`PluginTreeChangeLog`) every few seconds: a level's own change reloads it only if it
  is currently expanded, otherwise the cached copy is just dropped; a whole-plugin change (loaded, reloaded or removed) does the same for
  every level of that plugin. `reset: true` (the server restarted under a running editor, or the revision is older than the log's
  capacity) drops the whole cache.
- Nothing about plugin items is persisted (no expanded-node memory yet) — every open of the tool window starts collapsed.

## Backend

An optional interface in `MacroGrid.Plugin.Abstractions` (`src/MacroGrid.Plugin.Abstractions/IPluginTreeProvider.cs`) — a plugin that
does not implement it is unaffected, the same convention as `IPluginSettingsPage` and `IPluginHost.Secrets`:

```csharp
public interface IPluginTreeProvider
{
    // Children of parentId (null: the plugin's top level). Called only when the user expands that node.
    Task<PluginTreePage> GetTreeItemsAsync(string? parentId, string? continuationToken, CancellationToken cancellationToken);

    // Raised when a level changed (null: the top level); the host reloads it only if it is on screen.
    event Action<string?>? TreeItemsChanged;
}

public sealed record PluginTreeItem(string Id, string Label, string? Icon = null, bool HasChildren = false, string? Tooltip = null, bool HasSettings = false);
public sealed record PluginTreePage(IReadOnlyList<PluginTreeItem> Items, string? ContinuationToken = null);

// Optional side interface (only called for an item whose HasSettings is true), the same schema-driven form a
// plugin's own settings page uses:
public interface IPluginTreeItemSettings
{
    IReadOnlyList<SettingField> GetItemFields(string itemId);
    JsonObject LoadItem(string itemId);
    void SaveItem(string itemId, JsonObject values);
}
```

- `Icon` is a `lucide-react` icon name, the same set a status item uses; an unknown or missing name falls back to a plain dot. Labels
  and tooltips are localised by the plugin the same way as its other text (`PluginLocalizer`).
- `PluginTreeReader` enforces the limits the editor relies on regardless of what a plugin returns: a 2 s timeout, at most 500 items, no
  item without an id or with an id already seen on the page.
- `PluginTreeChangeLog` is a small, bounded (256-entry) in-memory log of `TreeItemsChanged` events plus whole-plugin load/unload, kept on
  `PluginManager.TreeChanges` and polled by the editor only while the tool window is open.
- Items are not draggable and have no edit commands. The JavaScript plugin host does not implement this yet (its async host API is not
  built); only .NET plugins can provide tree items today.
- First user: SoundBoard in `macro-grid-plugin` lists its sounds (flat, one level) — a separate SoundBoard release, asked for first like
  every plugin release.

Host API (`src/MacroGrid.Host/Api/PluginTreeApi.cs`), every route answering 404 for a plugin that does not implement the interface:

- `GET /api/plugins/tree-changes?since=<revision>` — `PluginTreeChangeLog.Since`, `since=-1` just returns the current revision.
- `GET /api/plugins/{id}/tree-items?parent=<id>&token=<token>` — one page, localised.
- `GET /api/plugins/{id}/tree-items/settings/schema?item=<id>`, `GET .../settings?item=<id>`, `PUT .../settings?item=<id>` — an item's
  own settings form, the same password-redaction rules (`PluginApi.RedactPasswords`/`RestoreUnchangedPasswords`) as a plugin's settings
  page.
- `POST /api/plugins/{id}/tree-items/options/{sourceId}` — dynamic dropdowns, if the tree provider also implements `IOptionsSource`.

## Frontend

- `editor/src/panels/PluginsToolWindow.tsx`: the tree itself (plugin rows, then lazily-loaded tree items under an expanded one).
- `editor/src/state/usePluginTree.ts`: the lazy per-level cache and the change-log poll, used only by the tool window above.
- `editor/src/state/pluginTreeSelectionStore.ts`: a module-level store (the same pattern as `dialogs/dialogStore.ts`, read with
  `useSyncExternalStore`) carrying "what is selected" from the Plugins tool window to the Properties tool window — neither is an
  ancestor of the other (a tool window's `Content` component takes no props), so this is the plain way to hand a selection across
  without an `App.tsx` edit. Selecting a widget, a Hierarchy page/profile row, or a plugin/tree-item row each clear the other two
  selection kinds, so Properties always shows exactly one of: the widget/page inspector, profile properties, or plugin properties.
- `editor/src/panels/PluginTreeItemProperties.tsx`: the Properties content for a plugin-tree selection — reuses `SchemaForm` exactly as
  `windows/PluginSettingsWindow.tsx` does, only pointed at the tree-item endpoints when the selection is an item rather than the plugin
  itself.
- `editor/src/workspace/toolWindows.ts`: registry entry `{ id: "pluginsTree", titleKey: "panel.pluginsTree", icon: Blocks, ... }`,
  `defaultOpen: false` (reachable from the View menu, per the "Adding a tool window" recipe in `docking-workspace.md`).

## Open questions

1. Persisting expanded plugin-tree nodes across restarts (`hierarchy-tree-and-folders.md`'s original design kept this for the
   in-tree version) — not built; every open of the tool window starts collapsed.
2. A view menu to show/hide individual plugins in the list, and Refresh/Expand-all commands — not built; the list is short enough in
   practice that this has not been asked for.
