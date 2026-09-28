# Docking workspace for the editor

**Status:** built in 1.2.0 (the docking workspace, tool windows, auto-hide rail, tabbed document area, saved layouts and the Error List). The Error List has no producer yet, see `../roadmap.md`. User guide: `website/guide/editor.md`. **Repositories:** `macro-grid` only (`editor/` and `src/MacroGrid.Host`, `src/MacroGrid.Core`).
No change to the plugin SDK, the WebSocket protocol or the phone app.

Related documents: [hierarchy-tree-and-folders.md](hierarchy-tree-and-folders.md) (the Hierarchy panel's content, built on this one) and
[editor-edit-commands-plan.md](../plans/editor-edit-commands-plan.md) (Edit menu, clipboard, undo/redo, keyboard shortcuts — planned, not started).

Visual reference: [docking-workspace-mockups/](docking-workspace-mockups/) holds three static HTML pages (open them in a browser):
`default-layout.html`, `custom-layout.html` (tabbed group, auto-hide, floating panel) and `component-states.html` (header, tab, splitter,
drop guide, severity marks). They show the look, not the code. Where a mockup and this text disagree, this text wins.

Read [../ui/ui-guidelines.md](../ui/ui-guidelines.md) and [../ui/color-bible.md](../ui/color-bible.md) before building any of it.

## Goal

Replace the fixed three-column grid in `editor/src/App.tsx` (`gridTemplateColumns: "200px 1fr 300px"`) with a docking workspace in the
single editor window: tool windows can be docked to any edge, split, tabbed together, floated inside the window, auto-hidden to an edge,
closed and restored, resized, and the layout survives a restart. The page editor gets document tabs. A new Error List tool window is
added with a data model only (no producers).

Adding another tool window later must take one new content component, one registry entry and two i18n keys, and nothing else (see
"Adding a tool window" below). This is a hard requirement: the owner will ask for new dockable windows by describing only their content.

## Decisions already made (do not reopen)

- **One window only.** Floating panels float inside the editor window. No panel ever becomes a separate OS window. The native tool
  windows in `src/MacroGrid.Host/Ui/ToolWindow.cs` (Preferences, Plugins, Pairing, Help, Update) are a different mechanism and stay as
  they are; they are not dockable.
- **Use a docking library, not a hand-rolled engine.** The npm package is `dockview-react` (MIT). It provides the split tree, tab groups,
  splitters, drag with drop guides, in-window floating groups and JSON serialization. Everything about it is wrapped in
  `editor/src/workspace/`; no other file imports it. Add it to `THIRD_PARTY_NOTICES.md` in the same change. (The project rule against
  naming third-party products applies to prose; the package name appears here and in `package.json` only because the build needs it.)
- **Auto-hide is our own layer** on top of the library, which has no pin-to-edge mode.
- **The visual rules of `docs/ui/` apply in full.** Chrome uses only `--ms-*` tokens, icons only `lucide-react`, no shadows, gradients,
  cards or large radii. The library's default theme is overridden completely.

## Architecture

```
App.tsx
 ├─ MenuBar                      (unchanged except the new View menu)
 ├─ toolbar <header>             (unchanged)
 ├─ <EditorStateProvider value={useEditorState()}>
 │    └─ <DockWorkspace>         (replaces the 3-column grid)
 │         ├─ library root       groups, tabs, splits, floating groups
 │         │    ├─ "document" panel   our DocumentArea: tab strip + DevicePreviewFrame + EditorCanvas
 │         │    └─ tool panels        DockPanelFrame → portal target for the tool window's content
 │         ├─ AutoHideRail × 3   left, right, bottom edges
 │         └─ AutoHideFlyout     the one open auto-hide panel, if any
 └─ StatusBar                    (unchanged)
```

### Files (all new unless marked)

`editor/src/workspace/`
- `toolWindows.ts`: the tool window registry (below). The only list of dockable panels.
- `DockWorkspace.tsx`: mounts the library, builds the default layout, owns the layout state, renders the rails and the flyout, exposes
  `useWorkspace()` (open, close, focus, toggle, isOpen, resetLayout, openDocument).
- `PanelHost.tsx`: renders every open tool window's content exactly once through `createPortal` into a stable DOM node per panel id.
  Docked panels, floating panels and the auto-hide flyout attach that node instead of rendering the content themselves. Moving a panel
  between dock, float and auto-hide then never remounts it, so scroll position, expanded tree nodes and filter text survive.
- `DockPanelFrame.tsx`: the library's panel component; only attaches the portal node.
- `DockGroupHeader.tsx`: the header of a tool window group: tabs (a single panel shows one tab that reads as its title), then the
  pin and close buttons for the active panel. Right-click on a tab opens the existing `ContextMenu` with Float / Dock, Auto Hide, Close.
  A tool window may put its own icon buttons before pin and close (an optional `HeaderActions` component in its registry entry, for
  example the Hierarchy's view menu); they use the same 18 px icon-button style.
- `AutoHideRail.tsx`, `AutoHideFlyout.tsx`: the edge tabs and the slide-out panel.
- `DocumentArea.tsx`: the page document tabs plus the existing `DevicePreviewFrame` + `EditorCanvas` (moved here from `App.tsx`, not
  rewritten).
- `defaultLayout.ts`: builds the default layout from the registry's `defaultPlacement`s.
- `layoutPersistence.ts`: load, validate, migrate, debounce-save.
- `dock-theme.css`: maps the library's CSS variables onto `--ms-*` tokens and removes its shadows and radii.

`editor/src/state/EditorStateContext.tsx`: a context holding the result of `useEditorState()`. Today `App.tsx` passes that state to
each panel as props. Tool windows read it from this context instead, so a new tool window never needs an edit in `App.tsx`. Existing
panels are **not** rewritten: their registry entries are small adapters that read the context and pass the same props they get today.

`editor/src/diagnostics/`: `types.ts`, `DiagnosticsContext.tsx` (Error List data, below).

`editor/src/panels/ErrorListPanel.tsx`: the new tool window.

Host: `src/MacroGrid.Core/Workspace/WorkspaceLayoutStore.cs` and two routes in `src/MacroGrid.Host/Api/AppApi.cs` (persistence, below).

### Tool window registry

```ts
// editor/src/workspace/toolWindows.ts
export interface ToolWindowDefinition {
  id: string;                    // stable and persisted: never rename one that has shipped
  titleKey: DictKey;             // i18n key for the tab title and the View menu
  icon: LucideIcon;              // shown in the View menu and on auto-hide tabs, 14 px
  Content: ComponentType;        // the content only; reads what it needs from context
  defaultPlacement: {
    edge: "left" | "right" | "bottom";
    group: string;               // panels with the same group id start tabbed together
    order: number;               // position inside the edge / group
    size: number;                // px across the edge (width for left/right, height for bottom)
  };
  minSize: { width: number; height: number };
  defaultOpen: boolean;          // false: starts closed, reachable from View
  HeaderActions?: ComponentType; // optional icon buttons in the header, before pin and close
}

export const TOOL_WINDOWS: ToolWindowDefinition[] = [
  { id: "hierarchy",  titleKey: "panel.hierarchy",  icon: FolderTree,    Content: HierarchyToolWindow,  defaultPlacement: { edge: "left",   group: "left-top",    order: 0, size: 220 }, minSize: { width: 160, height: 120 }, defaultOpen: true },
  { id: "toolbox",    titleKey: "panel.toolbox",    icon: Shapes,        Content: ToolboxToolWindow,    defaultPlacement: { edge: "left",   group: "left-bottom", order: 1, size: 220 }, minSize: { width: 160, height: 100 }, defaultOpen: true },
  { id: "properties", titleKey: "panel.properties", icon: Wrench,          Content: PropertiesToolWindow, defaultPlacement: { edge: "right",  group: "right",       order: 0, size: 300 }, minSize: { width: 240, height: 160 }, defaultOpen: true },
  { id: "errorList",  titleKey: "panel.errorList",  icon: ListX,         Content: ErrorListPanel,       defaultPlacement: { edge: "bottom", group: "bottom",      order: 0, size: 220 }, minSize: { width: 320, height: 100 }, defaultOpen: true },
];
```

Panels know nothing about docking. `DockWorkspace` alone decides where a panel is. Every icon in this plan (sizes, colours, the
custom ones) is specified in [../ui/editor-icons.md](../ui/editor-icons.md).

### Adding a tool window (the recipe the owner relies on)

1. Create `editor/src/panels/<Name>Panel.tsx`: a plain component with no props (or read `useEditorState` context / its own context).
   It fills its container, scrolls on its own and follows the Properties-panel density rules. No header of its own: the workspace
   draws the header.
2. Add one entry to `TOOL_WINDOWS`.
3. Add `panel.<id>` to `editor/src/i18n/tr.ts` and `en.ts`.

That is all: the View menu, the header, pin, close, float, auto-hide, persistence and reset pick it up from the registry. A saved layout
that does not know the new id places the panel at its `defaultPlacement` on the next start if `defaultOpen` is true, otherwise leaves it
closed. Write this recipe as `docs/guides/adding-a-tool-window.md` in the same change (short, the three steps plus one example).

## Behaviour

### Default layout

Exactly the ASCII layout of the task and `default-layout.html`: left column Hierarchy over Toolbox (about 45 / 55), the document area in
the middle, Properties on the right, Error List across the full width at the bottom, above the status bar. At 1920 x 1080 the document
area must be at least as large as it is today.

### Document area

- It is a single, fixed panel: it cannot be closed, floated, auto-hidden or tabbed with a tool window. Tool windows dock around it.
  The library's own tab header is hidden for it; `DocumentArea` draws its own tab strip.
- A document is a **page of the open profile**. The editor keeps one profile document in memory (`useProfileDocument`), and this plan
  does not change that. Only the active page's canvas is mounted; switching tabs switches `currentPageId`, so memory stays flat no matter
  how many tabs are open.
- `openPageIds` and the active page are kept per profile id in the workspace layout file (below). Opening another profile restores that
  profile's tabs (the first page if it has none remembered).
- Selecting a page in Hierarchy opens it in a tab if it is not open, and activates it. A new tab goes to the right of the active one.
- Closing a tab activates its right neighbour, else its left one. The last tab cannot be closed (its close button is hidden): the rest
  of the editor always has a current page, as today.
- Deleting a page closes its tab; renaming it renames the tab. Switching tabs clears the widget selection, the same as selecting a
  page in the tree does today.
- Unsaved changes stay per profile (the Save button as today). Tabs show no per-tab dirty mark, because saving is per profile.
- Middle-click closes a tab. The `+` button at the end of the strip adds a page (existing `addPage`) and opens it.

### Tool window header and tabs

Per `component-states.html`: 28 px high, `--ms-bg-surface`, a 1 px `--ms-border` bottom line. Title 12.5 px semibold. The group that
holds keyboard focus shows its active tab with `--ms-text-primary` on `--ms-bg-canvas` and a 2 px `--ms-accent` bottom line; other groups
show no accent. Pin and close are 18 px icon buttons, 12 px icons, `--ms-text-secondary`, hover background `--ms-bg-surface-raised`, the
pin turns `--ms-accent` on hover. Both have `aria-label`s and tooltips through i18n.

### Dock, split, tab

Dragging a tab (or a floating panel's header) shows the library's drop guides, restyled: target zones filled `--ms-accent-bg-muted` with a
1 px `--ms-accent` edge, no shadow. Dropping on an edge docks there, on a side of a group splits it, in the centre tabs it into the
group. The document area accepts edge and split drops (tool windows dock around it) but not centre drops.

### Floating

"Float" in the tab menu, or dragging a tab out onto the document area and releasing it outside any drop guide, makes a floating group
inside the window: header like a docked group, 1 px `--ms-border-strong` outline, no shadow, resizable from its edges. It is kept inside
the workspace bounds; after a window resize or on restore it is clamped back into view. "Dock" in its tab menu returns it to where it was
docked last (or its default placement).

### Pin and auto-hide

- The pin button of a docked panel unpins it: the panel leaves the layout and becomes a tab on the rail of the edge it was docked to
  (bottom, left or right; a panel docked in the middle of a split goes to the nearest edge). A whole tab group unpins together and gets
  one rail tab per panel.
- Rails are 22 px wide (left/right, vertical text) or high (bottom); they take no space when empty.
- Clicking a rail tab opens the flyout; hovering it for 400 ms also opens it. The flyout slides in from its edge over the document area,
  sized as the panel was when docked, with a 1 px `--ms-border-strong` edge, no shadow, no backdrop. It closes on Escape, on a click
  outside, or when focus leaves it. Animation: 120 ms transform, none when `prefers-reduced-motion` is set.
- The pin button inside the flyout pins the panel back to its previous place in the layout.
- Auto-hide is not hide: a closed panel has no rail tab; an auto-hidden one always has one.

### Collapsible sections

Unchanged and separate: sections inside a panel (for example Properties' Appearance, type fields, Actions) stay the panel's own business.
The docking layer never collapses panel content.

### Close and restore

- Close removes the panel from the layout and remembers where it was.
- **View** menu (new, between File and Settings, as desktop menus order it; mnemonic `G` for "Görünüm", `V` for "View"): one item per
  registry entry, checked when open. Clicking an unchecked item restores the panel to its remembered place (or its default placement);
  clicking a checked item focuses it (and opens the flyout if it is auto-hidden). Then a separator and **Reset Layout**.
- `ContextMenuItem` has no checked state today; add an optional `checked?: boolean` that draws a check mark column, and an optional
  `shortcut?: string` right-aligned column (the edit-commands plan uses it too).

### Reset layout

View → Reset Layout rebuilds `defaultLayout()`. If the current layout differs from the default, ask first with `confirmAsync` (never
the browser's `confirm()`); otherwise reset without asking. Reset keeps document tabs.

### Resizing and small windows

- Every splitter is the library's sash restyled: 4 px hit area, a 1 px `--ms-border` line that turns `--ms-border-strong` on hover.
- `minSize` from the registry is enforced; the document area's minimum is 360 x 240.
- When the window is too narrow for the docked side panels plus the document minimum, the workspace auto-hides the right edge's groups
  first, then the left's, **transiently**: that is not saved, and they come back when the window is wide enough again. A panel the user
  auto-hid on purpose stays auto-hidden.

### Keyboard

The editor has no shortcut system yet (only the menu bar's Alt mnemonics); the edit-commands plan introduces a command registry. This plan
only makes the View menu reachable (Alt+G / Alt+V) and gives every button and tab a keyboard focus. The panel shortcuts (focus
Hierarchy / Toolbox / Properties / Error List, toggle Error List) are registered as commands in the edit-commands plan.

## Error List

### Data model

```ts
// editor/src/diagnostics/types.ts
export type DiagnosticSeverity = "error" | "warning" | "info";

export interface DiagnosticTarget {
  profileId: string;
  pageId?: string;
  widgetId?: string;
  field?: string;                // for example "actions.press[0]"; free text for display only
}

export interface Diagnostic {
  id: string;                    // unique inside its source
  source: string;                // producer id, for example "profile-validation"
  severity: DiagnosticSeverity;
  code: string;                  // for example "E102"; each producer documents its codes
  messageKey: DictKey;           // translated at render time, so a language switch re-renders
  messageArgs?: string[];
  target?: DiagnosticTarget;
}
```

`DiagnosticsContext` exposes `diagnostics`, `report(source, list)` (replaces everything that source reported before, the way a
validator re-run works), `clear(source?)` and `useDiagnosticsCounts()`. It lives in memory only.

**No producer is part of this plan.** Nothing reports anything yet; the Error List shows its empty state. Do not invent validation
to fill it. The mockup's rows are illustrations of the layout only.

### UI

Per `default-layout.html`, a table, not cards:

- A filter row: a compact search box (matches code, message, screen and location text), a segmented control All / Errors / Warnings /
  Messages with counts (the existing `Seg` control in `panels/fields/controls.tsx`), and Clear (clears the current entries; a
  producer's next `report` brings its entries back).
- Columns: Severity, Code, Description, Screen (profile name / page name), Location (widget name and field). Click a header to sort,
  click again to reverse; default sort severity then screen.
- Severity is shape-coded: error `CircleAlert` in `--ms-danger`, warning `TriangleAlert` and info `Info` in `--ms-text-secondary`.
  The chrome has no warning colour and none may be added (colour bible rule).
- 24 px rows, selected row `--ms-accent-bg-muted`. Keyboard: arrows move, Enter navigates.
- Double-click or Enter navigates when there is a target: open that profile (through the existing discard-changes confirmation), open
  the page tab, select the widget, focus Properties. A target that no longer exists shows a short status-bar message instead.
- Empty state: one line of `--ms-text-secondary` text ("No problems" / "Sorun yok").
- The status bar shows the error and warning counts; clicking them opens / focuses the Error List.

## Persistence

### Where

A new file `workspace-layout.json` in the app data directory, not `preferences.json`: the layout changes on every splitter drag, only the
editor window uses it, and `PreferencesStore.Changed` would otherwise broadcast every drag to the other windows.

- `src/MacroGrid.Core/Workspace/WorkspaceLayoutStore.cs`: same pattern as `PreferencesStore` (lock, write to a temp file then atomic
  rename). It stores the JSON as an opaque document: the server checks only that it is a JSON object under 256 KB and does not interpret
  it. A missing or unreadable file reads as "none"; an unreadable file is renamed to `.broken` as `ProfileStore` does.
- `GET /api/workspace-layout` (200 with the document, or 204 when there is none) and `PUT /api/workspace-layout` in `AppApi.cs`, next
  to the preferences routes. Add `getWorkspaceLayout` / `saveWorkspaceLayout` to `editor/src/api/client.ts`.
- This is a local API like the others; it does not change the security model.

### What

```jsonc
{
  "schemaVersion": 1,
  "layout": { /* the library's serialized layout: groups, splits and ratios, tabs, active tabs, floating groups and their bounds */ },
  "panels": {
    "<toolWindowId>": {
      "state": "docked" | "floating" | "autoHidden" | "closed",
      "autoHideEdge": "left" | "right" | "bottom",   // when autoHidden
      "autoHideSize": 300,                            // flyout size
      "lastDocked": { /* enough to put it back: group id, neighbour, direction, size */ }
    }
  },
  "documents": { "<profileId>": { "open": ["<pageId>"], "active": "<pageId>" } },
  "panelState": { "<toolWindowId>": { /* optional per-panel UI state, for example Hierarchy's expanded nodes */ } }
}
```

### Rules

- Save debounced (500 ms after the last change) and once more on `pagehide`. Never on every mouse move.
- On start, load before the first paint of the workspace (show the existing "loading" text until then). Validate: schema version known,
  the library accepts the layout, every panel id exists in the registry (unknown ids are dropped), the document panel exists. Anything
  invalid → the default layout, and one line in the status bar saying the layout was reset. Never a dialog.
- `schemaVersion` changes go through a `migrate(from, doc)` function in `layoutPersistence.ts`, one step per version.
- Page ids in `documents` that no longer exist are dropped silently; a profile id that no longer exists drops its entry.

## Phases

Each phase ends with `npm run typecheck` and `npm run build` in `editor/` passing, `dotnet build` passing when C# changed, and a manual
run of the editor (the `run` skill) checking the phase's behaviour and that nothing that worked before broke.

1. **Workspace shell, no visible change.** Add the dependency and its notice entry. `EditorStateContext`, the registry with adapters for
   the three existing panels, `PanelHost`, `DockWorkspace` with the default layout, `DocumentArea` with a single tab, the theme file,
   the header. Splitters resize. No persistence yet. The editor must look and behave like today apart from the new headers.
2. **Persistence.** `WorkspaceLayoutStore`, the two routes, `layoutPersistence.ts` with validation and fallback.
3. **Dock, split, tab, float, close, View menu, Reset Layout**, `ContextMenuItem.checked`/`shortcut`.
4. **Pin and auto-hide**, rails, flyout, transient auto-hide on narrow windows.
5. **Document tabs** (`openPageIds` per profile, persisted).
6. **Error List** panel, `DiagnosticsContext`, status bar counts, navigation. Write `docs/guides/adding-a-tool-window.md`.

Test these layouts by hand at the end, then restart the editor and check each comes back exactly:

1. Hierarchy and Toolbox tabbed on the left.
2. Properties and Error List stacked on the right.
3. Error List docked at the bottom (the default).
4. Properties floating over the document area.
5. Every tool window auto-hidden.
6. The document area taking almost the whole window.
7. A complex custom layout, then a restart.
8. A corrupted `workspace-layout.json` → default layout and a status bar line.
9. A 1280 x 720 window with the default layout → the right panel goes to its rail, and returns at 1920 wide.
10. Light theme and dark theme.

## Changelogs

`CHANGELOG.md` under `[Unreleased]`: New: panels can be moved, docked, tabbed, floated, auto-hidden and restored, and the layout is
remembered; pages open in tabs; an Error List panel. `CHANGELOG-developer.md`: nothing (no SDK or protocol change).

## Open questions

1. Hover-to-open on auto-hide tabs (400 ms) or click only? The plan says both; the owner may prefer click only.
2. Should a floating panel be able to leave the window's bounds partially (as long as its header stays reachable)? The plan says no.
3. Once validation exists, which producer comes first (for example missing plugin actions, "go to page" targets that no longer exist,
   widgets outside the grid)? Each needs its own small design; none is in this plan.
