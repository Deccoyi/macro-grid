# Hierarchy tree: every profile and page, folders, drag-and-drop

**Status:** planned, not started. Build after [docking-workspace-plan.md](docking-workspace-plan.md) and phases 1 and 2 of
[editor-edit-commands-plan.md](editor-edit-commands-plan.md). **Repositories:** `macro-grid` (`editor/`, `src/MacroGrid.Core`,
`src/MacroGrid.Host`, `packages/renderer`; phase 6 also `src/MacroGrid.Plugin.Abstractions`, the plugin SDK). Phase 6's first use
(SoundBoard) is in `macro-grid-plugin`. The phone app (`macro-grid-client`) needs no change: the new fields are optional and it ignores
them (verify with its build). **Phase 6 changes the plugin SDK** (additive, optional interface): it updates `../architecture.md` and
follows `../guides/versioning.md` in the same change.

## Goal

The Hierarchy tool window becomes a project tree, replacing `ProfilePagesPanel`'s profile picker + page list:

```
Profiles
├─ Streaming                 (profile folder)
│  ├─ Profile 1
│  │  ├─ Scenes              (page folder)
│  │  │  ├─ Page 1
│  │  │  └─ Page 2
│  │  └─ Page 3
│  └─ Profile 2
│     ├─ Page 1
│     └─ Page 2
└─ Profile 3
   ├─ Page 1
   └─ Page 2
Plugins
├─ <plugin>
└─ SoundBoard
```

- Profiles and pages can be organised into folders.
- A page always belongs to a profile. So there are **two kinds of folder that never mix**: a *profile folder* holds only profile folders
  and profiles; a *page folder* lives inside one profile and holds only page folders and pages. A profile can never go into a page folder
  (or into a folder that holds pages), and a page or page folder can never go into a profile folder or directly under "Profiles".
- Pages can be reordered, moved into and out of folders, and moved between profiles by drag-and-drop, several at once.
- While dragging, the drop position is shown in colour, and it is always clear whether the items will land above an item, below it, or
  inside a folder or profile.
- Deleting a folder that still contains pages or profiles warns first.

## Data model

### Page folders (inside the profile)

`packages/renderer/src/types.ts`, and the same in `src/MacroGrid.Core/Model/Profile.cs` (the server must keep the field, or it is lost
on the next save):

```ts
export type PageTreeNode =
  | { type: "page"; id: string }
  | { type: "folder"; id: string; name: string; children: PageTreeNode[] };

export interface Profile {
  // ...existing fields
  /** Editor-only: how the pages are arranged into folders and ordered. Ignored by the renderer and the phone app. */
  pageTree?: PageTreeNode[];
}
```

- `pages` stays the one place page data lives. `pageTree` only arranges it.
- **Invariant:** after every change, `pages` is rewritten in the tree's depth-first order. The phone app's page order (swipes, "next
  page") then always matches what the editor shows. Put this in one function, `normalizePageTree(profile)`, used by every operation.
- Normalisation also repairs: a page missing from the tree is appended at the root; a tree node naming an unknown page is dropped; a
  duplicate is dropped; a profile without `pageTree` gets a flat tree from `pages` (old data keeps its order).
- Folder ids use the existing `tempId` helper's format. Folders can be empty.

### Profile folders (on the server)

Profiles are one file each and are not loaded together, so their arrangement lives in its own file, `profile-tree.json` in the data
directory:

```jsonc
{ "schemaVersion": 1, "nodes": [ { "type": "folder", "id": "…", "name": "Streaming", "children": [ { "type": "profile", "id": "…" } ] }, { "type": "profile", "id": "…" } ] }
```

- `src/MacroGrid.Core/Profiles/ProfileTreeStore.cs`, the same write pattern as `PreferencesStore`. Normalised against `ProfileStore` on
  every read: profiles missing from the tree are appended at the root (sorted by name, the old order), unknown ids dropped.
- `ProfileStore` creating a profile appends it (at the root, or into the folder given in the create request); deleting removes it.
- The server does not change what it sends to the phone app in this plan (open question 1).

### Tree summaries for the editor

The tree shows pages of profiles that are not open. Extend `GET /api/profiles` (`src/MacroGrid.Host/Api/ProfileApi.cs`) to return the
arrangement and, per profile, `{ id, name, pages: { id, name }[], pageTree }`. It is still one small request; widgets are not included.
`ProfileSummary` in `editor/src/api/types.ts` gains the same fields. For the open profile the tree uses the in-memory document, so unsaved
renames and moves show at once.

## Operations and where they happen

The editor keeps one profile document in memory (unchanged). That decides what is undoable:

| Operation | Where | Undo |
|---|---|---|
| Reorder, move, create, rename, delete pages and page folders **inside the open profile** | in-memory `mutate` (dirty until Save) | yes |
| The same inside a profile that is **not open** | server, saved at once | no |
| Moving or copying pages or page folders **between two profiles** | server, one atomic request | no |
| Profile folders: create, rename, move, delete; moving profiles | server (`profile-tree.json`), saved at once | no |

### Transfer between profiles

`POST /api/profiles/transfer` with `{ sourceProfileId, nodeIds, targetProfileId, targetParentFolderId | null, index, mode: "move" | "copy" }`.
The server does it under the `ProfileStore` lock and saves both profiles, so a failure never leaves a page in both or neither. Copies get
fresh page and widget ids (as `duplicatePageToProfile` does). Moves keep the page ids.

- If the open profile is the source or the target and has unsaved changes, first ask with `choiceAsync`: **Save and continue** / Cancel.
  After the transfer the open profile is reloaded (its tabs kept where the pages still exist; undo history cleared, per the edit-commands
  plan).
- "Go to page" actions in the source profile that point at a moved page now point nowhere. Before a move, count them and, if there are
  any, name the count in the confirmation. (Copy is unaffected.)
- One status bar line after it: "3 pages moved to Profile 2".

## Tree UI

`editor/src/panels/hierarchy/`: `HierarchyPanel.tsx` (the tool window's `Content`), `treeModel.ts` (builds rows from the profile tree, the
summaries and the open document; pure functions, no React), `dropRules.ts`, `useTreeSelection.ts`, `useTreeDrag.ts`. The existing
`AppMatchesEditor` and profile-level settings that `ProfilePagesPanel` shows today move to the Properties panel when a profile node is
selected (guideline: "can it be solved in the Properties panel? Use it."). Then delete `ProfilePagesPanel`.

- Rows 22 px, indent 14 px per level, a chevron for containers, 14 px icons in `--ms-text-secondary`, as specified in
  [../ui/editor-icons.md](../ui/editor-icons.md): `Folder` / `FolderOpen` for both folder kinds (the position tells them apart), the
  `LibraryBig` for a profile and `LayoutTemplate` for a page, `Puzzle` for a plugin without a logo, no icon on the "Profiles" and
  "Plugins" roots.
- The open profile's name is drawn `--ms-text-primary` semibold; the active document page has a 2 px `--ms-accent` left bar; selected
  rows `--ms-accent-bg-muted`; the keyboard-focus row a 1 px `--ms-border-strong` outline.
- Clicking a page selects it and opens it in a document tab (opening its profile first, through the existing discard-changes question,
  when needed). Ctrl-click and Shift-click only change the selection.
- Expanded and collapsed state is kept in the workspace layout file's `panelState.hierarchy` (docking plan).
- Inline rename on F2 or on a second slow click: an input in the row, Enter confirms, Escape cancels.

### Selection

- Multi-select with Ctrl-click, Shift-click, Shift+arrows, Ctrl+Space (full key list in the edit-commands plan).
- A selection holds items of **one family**: pages and page folders, or profiles and profile folders. Ctrl-clicking an item of the other
  family starts a new selection with it. The "Profiles" and "Plugins" roots and plugin rows are never part of a selection.
- A selection may span several profiles (pages from Profile 1 and Profile 2 together).

### Drag-and-drop

Pointer-based (pointer events, not the HTML5 drag API), so it cannot collide with the docking library's own drag handling and so the
indicator can be drawn exactly.

**Starting.** Dragging a selected row drags the whole selection; dragging an unselected row selects it first. A drag starts after 4 px
of movement. The dragged rows fade to 50 % opacity in place. Next to the pointer: a compact label in `--ms-bg-surface-raised` with a
1 px `--ms-border-strong` edge and the count and kind ("3 pages", "Profile 2"). Holding Ctrl copies instead of moving; the label then
says "Copy 3 pages" and shows a small `Plus`.

**Where it drops.** The row under the pointer is split by height:

| Target row | Top 25 % | Middle 50 % | Bottom 25 % |
|---|---|---|---|
| Container that accepts the items (folder, profile for pages) | above | **inside** | below |
| Any other row | above (top half) | | below (bottom half) |

"Below" an expanded container means before its first child, as in file managers. Horizontal position left of an item's indent while
at the bottom of the last child of a folder means "below the folder" at the parent level (so the user can drag an item out of a folder).

**How it looks** (all colours from the colour bible; `component-states.html` in the docking mockups has the drop-guide language):

- **Above / below:** a 2 px `--ms-accent` line across the row, starting at the indent of the level the items will land on, with a 6 px
  hollow circle at its left end. The start position shows the level, so above/below inside a folder and above/below the folder itself
  look different.
- **Inside:** the whole target row filled `--ms-accent-bg-muted` with a 1 px `--ms-accent` outline, and the container's chevron shown
  open.
- **Not allowed:** no line and no fill, the pointer shows `not-allowed`, and the label next to it turns `--ms-text-disabled`.
- Hovering a collapsed container for 600 ms expands it. Near the top or bottom edge of the tree it scrolls.
- Escape cancels; dropping where nothing is shown does nothing.

**Drop rules** (`dropRules.ts`, one pure function `canDrop(items, target, position)`; paste uses the same function):

- Page or page folder → inside a page folder or a profile; above / below a page or page folder. Never inside or next to anything in the
  profile-folder family.
- Profile or profile folder → inside a profile folder or the "Profiles" root; above / below a profile or profile folder. Never inside a
  profile, a page folder, or next to pages.
- A folder cannot go inside itself or any of its descendants.
- If any dragged item is not allowed at the target, nothing is allowed there (no partial drops).
- Dropping items onto their current place is a no-op (no undo step).
- The dragged items keep their relative order.
- Dropped into another profile → the transfer request above; otherwise the matching in-memory or server operation from the table.

### Context menus

Built from commands (edit-commands plan); Undo and Redo are at the top of every one.

- **"Profiles" root:** New profile, New folder | Paste.
- **Profile folder:** New profile, New folder | Rename | Cut, Copy, Paste | Delete.
- **Profile:** Open | New page, New folder | Rename | Cut, Copy, Paste, Duplicate | Delete.
- **Page folder:** New page, New folder | Rename | Cut, Copy, Paste | Delete.
- **Page:** Open | New page, New folder | Rename | Cut, Copy, Paste, Duplicate | Copy to profile... (existing dialog) | Delete.
- With several items selected, only the items valid for all of them are enabled.

### Deleting

- Page, one or several: as today (confirmation), undoable in the open profile.
- Profile: as today (confirmation; the last profile cannot be deleted).
- **Folder that is empty:** deleted without a question (in the open profile it is undoable anyway).
- **Folder with contents:** always asks, with `choiceAsync`, naming what is inside, counted recursively:
  "'Scenes' contains 4 pages and 1 folder." Buttons: **Delete folder and contents** (danger), **Keep contents** (the contents move up to
  the folder's parent in the folder's place), Cancel. For a profile folder the message adds that profiles are deleted permanently and
  cannot be undone, and "Delete folder and contents" is disabled when it would delete every profile.
- Several folders selected: one question with the totals.

## Plugins node

The rule here is performance: the tree must never slow the editor's start or ask a plugin for anything the user has not opened. The
host has a tight CPU and memory budget (it runs next to streaming software and games).

### What is shown

- The "Plugins" root lists the installed plugins (name, and the manifest icon if `hasIcon`), in the Plugins window's order. Plugins are
  not draggable and have no edit commands.
- **Plugins are not required to add anything to the tree.** A plugin only gets a chevron (expandable) when it declares that it provides
  tree items (`PluginInfo.hasTreeItems`, phase 6). Others are a single row.
- Double-click or Enter on a plugin row opens its existing settings window if it has one (`hasSettings`), otherwise the Plugins window on
  that plugin.

### Show / hide

A **view menu in the Hierarchy header**: an icon button (`lucide-react` `EllipsisVertical`, with an `aria-label`; the header gets an
optional actions slot from the docking plan's `DockGroupHeader`) opening a normal `ContextMenu` with checkable items:

- Show Plugins (the whole section)
- one item per installed plugin: show / hide that plugin's row
- Expand all / Collapse all (profiles only; never expands plugin items)

The choice is an editor view setting, so it is saved in the workspace layout file (`panelState.hierarchy.hiddenPlugins`,
`showPlugins`), not in the plugin's own settings: plugin settings belong to the plugin, and one place to switch it is enough. A hidden
plugin or section costs nothing: its rows are not built and nothing is requested for it. The same menu is the one place to add further
view options later.

### Loading

- **Start:** the tree needs one request, `GET /api/profiles` (with summaries, above). The Plugins root starts **collapsed** and
  `api.listPlugins()` is called the first time it is expanded. If the saved state has it expanded, the list is fetched after the first
  paint of the tree, not before it.
- **Plugin items are loaded lazily, one level at a time, when that node is expanded**, never earlier: `GET /api/plugins/{id}/tree-items?parent=<itemId>`
  (no `parent` for the plugin's top level). While loading, the node shows one "Loading..." row in `--ms-text-secondary`; the rest of
  the tree stays usable.
- The server passes the call to the plugin with a 2 s timeout and a cancellation token; the editor cancels the request if the node is
  collapsed before the answer comes. A timeout or error shows one "Could not load. Retry" row under the node, never a dialog.
- At most 500 items per level; a provider that has more returns a continuation token and the tree shows a "Show more" row.
- Loaded children are cached in memory for the session. Collapsing keeps the cache; the node's context menu has **Refresh**. A plugin can
  also tell the host that a level changed (phase 6, `TreeItemsChanged(parentId)`); the editor reloads that level only if it is expanded,
  otherwise it just drops the cache.
- Nothing about plugin items is persisted except which nodes were expanded; on the next start expanded plugin nodes load after the
  first paint, as above.

### Rendering

The tree renders only the rows in view (fixed 22 px rows make windowing simple; write it in the tree, no new dependency) once it has
more than about 200 visible rows, so a large profile collection or a plugin with many items does not slow scrolling or dragging.

### Plugin items (phase 6, SDK)

A new **optional** interface in `MacroGrid.Plugin.Abstractions`; a plugin that does not implement it is unaffected:

```csharp
public interface IPluginTreeProvider
{
    /// Children of parentId (null: the plugin's top level). Called only when the user expands that node.
    Task<PluginTreePage> GetTreeItemsAsync(string? parentId, string? continuationToken, CancellationToken cancellationToken);

    /// Raised when a level changed; the host refreshes it only if it is on screen.
    event Action<string?>? TreeItemsChanged;
}

public sealed record PluginTreeItem(string Id, string Label, string? Icon, bool HasChildren, string? Tooltip);
public sealed record PluginTreePage(IReadOnlyList<PluginTreeItem> Items, string? ContinuationToken);
```

- `Icon` is a `lucide-react` icon name, drawn in the chrome colour like every tree icon; unknown names fall back to a dot. Labels are
  already localised by the plugin (the host passes the language as it does for other plugin text).
- Items are not draggable and have no edit commands in this plan. Double-click opens the plugin's settings window with the item id, so a
  plugin can jump to it; a plugin without a settings window gets nothing on double-click.
- The JavaScript plugin host gets the same capability when its async host API exists (roadmap); until then only .NET plugins can
  provide items.
- First user: SoundBoard in `macro-grid-plugin` lists its sounds (flat, one level).

## Phases

1. **Data model.** `pageTree` in the renderer types and the C# model, `normalizePageTree`, `ProfileTreeStore`, the extended
   `GET /api/profiles`, tests for normalisation (old profiles without a tree, unknown ids, duplicates) in the existing .NET test project.
2. **Read-only tree** with every profile, page folders and profile folders, the Plugins node (plugin rows only, lazily listed), the
   header's view menu (show / hide), row windowing, selection, keyboard navigation, opening pages; profile settings moved to Properties;
   `ProfilePagesPanel` removed.
3. **Folder commands:** new, rename, delete (with the contents question), in-memory and server paths.
4. **Drag-and-drop** inside one profile and for profiles/profile folders, with the drop indicator; tree cut/copy/paste (edit-commands
   plan phase 4) on the same `canDrop` and move function.
5. **Transfers between profiles** (the endpoint, save-first question, "go to page" warning, reload).
6. **Plugin tree items** (SDK): `IPluginTreeProvider`, `PluginInfo.hasTreeItems`, the `tree-items` route with timeout, cancellation and
   paging, lazy loading and caching in the tree; then SoundBoard's sounds in `macro-grid-plugin` (a separate SoundBoard release, asked
   for first like every plugin release). Independent of phases 3 to 5.

Performance checks for phase 2 and 6: with 50 profiles of 20 pages each and every plugin shown, the editor's start time must not grow by
more than the one `GET /api/profiles` request (measure before and after); expanding a plugin node makes exactly one request; a hidden
plugin makes none (check the Host log or the network panel); scrolling a 1,000-row tree stays smooth.

Each phase: `npm run typecheck` and `npm run build` in `editor/`, `dotnet build` and `dotnet test`, the phone app's build in
`macro-grid-client` after phase 1, and a manual run. Manual checks at the end: drag three pages from two different folders into a third
folder; drag a page folder into another profile with Ctrl (copy); try to drop a profile into a page folder and a page under "Profiles"
(both refused, visibly); delete a folder with contents both ways; undo each in-profile step; restart and check the order on the phone app.

## Changelogs

`CHANGELOG.md` `[Unreleased]`: New: profiles and pages can be put into folders and rearranged by drag-and-drop, also several at once and
between profiles. `CHANGELOG-developer.md`: the profile JSON gains an optional `pageTree` field (editor-only, additive; the renderer and
the phone app ignore it), and the new `/api/profiles/transfer` and `profile-tree.json`. Phase 6: the optional `IPluginTreeProvider`
SDK interface (additive, no existing plugin needs a change).

## Open questions

1. Should the phone app's profile list follow the tree (folder order, or even show folders)? This plan keeps it as today (sorted by name).
   Following the order only would be a server-side change without a protocol change; showing folders would change the protocol.
2. Should a plugin item be draggable onto the canvas later (for example drop a sound to create a button that plays it)? Not in this
   plan; it would build on phase 6.
