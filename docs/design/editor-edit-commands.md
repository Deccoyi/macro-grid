# Editor edit commands: context menus, clipboard, undo/redo, shortcuts

**Status:** built. Right-click menus, the Edit menu, one shared snapshot-based undo/redo history, standard Windows shortcuts and a
header toolbar (added on top of this plan at the owner's request) all shipped. Built on [docking-workspace.md](../design/docking-workspace.md)
(built in 1.2.0). **Repositories:** `macro-grid` only (`editor/`). No change to the plugin SDK, the WebSocket protocol, the phone app
or the server.

This was the plan for the roadmap's "editor keyboard shortcuts with undo/redo" item. The hierarchy tree's phase 6
([plugins-tool-window.md](../design/plugins-tool-window.md)) shipped separately and does not depend on this one.

## Goal

Every right-click menu offers the edit operations that apply to what was clicked: **Undo, Redo, Cut, Copy, Paste, Duplicate, Delete**
(plus the existing item-specific ones). The same operations are in a new **Edit** menu and on the usual keyboard shortcuts, and all three
paths run the same code.

## 1. Command registry

`editor/src/commands/`

```ts
export interface Command {
  id: string;                          // "edit.undo", "edit.copy", "view.focusProperties", ...
  labelKey: DictKey;
  label?: () => string;                // dynamic label, for example "Undo Move widget"
  shortcuts?: string[];                // display and matching form: "Ctrl+Z", "Ctrl+Shift+4", "Delete", "F2"; menus show the first
  enabled: () => boolean;
  run: () => void | Promise<void>;
}
```

- `CommandsProvider` + `useCommands()`: holds the registry; features register their commands with `useRegisterCommands([...])`.
- `ShortcutListener`: one `keydown` listener on `window`. It matches the pressed keys against the registered shortcuts, calls
  `preventDefault()` on a match, and **does nothing while focus is in an `input`, `textarea`, `select` or `contenteditable`** (their own
  undo, copy and paste must keep working), except Escape.
- A context menu item or menu item made from a command shows its first shortcut right-aligned and is disabled when `enabled()` is false:
  `commandItem(cmd): ContextMenuItem`. `ContextMenuItem.shortcut` is added by the docking plan; if it is not there yet, add it here.
- **No Ctrl+Alt shortcuts.** On the Turkish Q layout Ctrl+Alt is AltGr, which types `@ € ₺ # $ { } [ ] \ |`.
- Check `src/MacroGrid.Host/Ui/EditorWindow.cs`: the WebView2 browser accelerators (Ctrl+R / F5 reload, Ctrl+P print, Ctrl+F find) should
  be off in the editor window. If they are on, turn them off (`AreBrowserAcceleratorKeysEnabled = false`) in the same change, or a stray
  Ctrl+R reloads the editor and loses unsaved work.

### Shortcuts

The classic Windows shortcuts must all work; a user coming from any Windows program should not have to learn anything. A command may
have several shortcuts (`shortcut` becomes `shortcuts: string[]`; menus show the first).

**Edit (canvas or tree, by focus scope)**

| Command | Shortcuts |
|---|---|
| Undo | Ctrl+Z, Alt+Backspace |
| Redo | Ctrl+Y, Ctrl+Shift+Z |
| Cut | Ctrl+X, Shift+Delete |
| Copy | Ctrl+C, Ctrl+Insert |
| Paste | Ctrl+V, Shift+Insert |
| Duplicate | Ctrl+D |
| Delete | Delete |
| Select all | Ctrl+A (all widgets on the page; in the tree, all items of the focused item's level and family) |
| Clear selection, close a menu, cancel a drag or an inline rename | Escape |
| Save | Ctrl+S |
| Context menu for the selection | Shift+F10, the Menu (Application) key |

**Canvas**

| Command | Shortcuts |
|---|---|
| Move the selected widgets one cell | Arrow keys (blocked at the grid edge or an occupied cell, like a drag) |
| Resize the selected widget one cell | Shift+Arrow keys |
| Select next / previous widget | Tab / Shift+Tab (in grid order) |

**Hierarchy tree** (Windows Explorer conventions)

| Command | Shortcuts |
|---|---|
| Move the focus | Up / Down, Home / End, Page Up / Page Down |
| Collapse / go to parent; expand / go to first child | Left / Right |
| Extend the selection | Shift+Up / Down, Shift+Home / End, Shift+click |
| Add or remove one item | Ctrl+Space, Ctrl+click (Ctrl+Up / Down moves the focus without changing the selection) |
| Open the page (or open the profile) | Enter |
| Rename | F2 |
| New page / new folder | Ctrl+N / Ctrl+Shift+N |
| Type-ahead: jump to the next item starting with the typed letters | letters |

**Documents and workspace**

| Command | Shortcuts |
|---|---|
| Next / previous page tab | Ctrl+Tab / Ctrl+Shift+Tab, Ctrl+Page Down / Ctrl+Page Up |
| Close the page tab | Ctrl+W, Ctrl+F4 |
| Focus the menu bar | F10, Alt pressed and released (existing mnemonics stay) |
| Next / previous pane (document area and each open tool window) | F6 / Shift+F6 |
| Focus Hierarchy / Toolbox / Properties / Error List | Ctrl+Shift+1 / 2 / 3 / 4 (Ctrl+Shift+4 while the Error List has focus closes it) |

Inside text fields the field's own standard keys win (Ctrl+Z, Ctrl+A, Home, End, arrows and so on), as said above. Enter confirms and
Escape cancels an inline edit, as the Properties guideline already asks.

The four panel commands come from the docking plan's registry (`view.focus.<toolWindowId>`); new tool windows get a focus command
automatically but no shortcut unless their registry entry names one (add an optional `shortcut` field to `ToolWindowDefinition`).

## 2. Menus

- **Edit** menu in `MenuBar.tsx`, between File and View (mnemonic `Z` for "Düzen", `E` for "Edit"): Undo, Redo | Cut, Copy, Paste,
  Duplicate, Delete | Select All. Labels follow the focus: with the canvas focused they act on widgets, with Hierarchy focused on tree
  items (see "Focus scope").
- **Canvas, on a widget:** Undo, Redo | Cut, Copy, Paste, Duplicate, Delete | Move / Copy to... (existing).
- **Canvas, on empty space:** Undo, Redo | Paste | Select All.
- **Hierarchy:** defined in the hierarchy tree plan; it uses the same commands.
- Replace the hand-built items in `widgetContextItems` / `pageContextItems` (`App.tsx`) with command items so both menus and shortcuts
  share one implementation.

### Focus scope

Cut, Copy, Paste, Duplicate and Delete act on the **focused scope**: `"canvas"` (the widget selection) or `"hierarchy"` (the tree
selection). The workspace tracks which tool window or document area last received focus; a right-click sets the scope to where it
happened. Commands read `activeScope` and are disabled when the scope has nothing to act on.

## 3. Clipboard

An in-memory editor clipboard (`editor/src/commands/clipboard.ts`); nothing is written to the system clipboard in this plan.

```ts
type ClipboardContent =
  | { kind: "widgets"; widgets: Widget[]; sourcePageId: string; sourceProfileId: string }
  | { kind: "tree"; items: TreeItemRef[]; mode: "copy" | "cut" }   // pages, page folders, profiles, profile folders; see the tree plan
```

- **Widgets.** Copy stores deep clones. Cut = copy, then delete (one undo step). Paste into the current page: each widget keeps its
  position if that cell range is free, otherwise it goes to `findFreeCell` (as `duplicateSelectedWidgets` does); fresh ids (`tempId`);
  the pasted widgets become the selection. If nothing fits, paste what fits and say in the status bar how many did not.
  Pasting into another page or profile works because the canvas always shows a page of the open profile.
- **Tree items.** Copy / Cut / Paste as in a file manager: cut items are drawn dimmed until pasted, and a cut is a move that happens on
  paste. Rules (what may go where) are the hierarchy plan's drop rules; paste uses exactly the same function as drag-and-drop.
- Pasting is one undo step.

## 4. Undo / redo

In `useProfileDocument` (the only place that changes the open profile).

- Snapshot history: `past: Snapshot[]`, `future: Snapshot[]`, where `Snapshot = { profile: Profile; currentPageId: string | null;
  selectedIds: string[]; label: DictKey }`. Profiles are small JSON documents and `mutate` already clones them, so snapshots are cheap;
  cap `past` at 100 entries (lightweight resource budget).
- `mutate(fn, opts?: { label?: DictKey; coalesceKey?: string })`: pushes the previous state before applying `fn` and clears `future`.
  Two calls with the same `coalesceKey` within 600 ms become one step: typing in a Properties field, dragging a slider, moving or
  resizing a widget by drag. Every existing caller of `mutate` / `mutatePage` gets a `label` (and a `coalesceKey` where it fires
  repeatedly); the labels are new i18n keys (`undo.moveWidget`, `undo.editProperty`, ...).
- Undo restores the snapshot's profile, current page and selection (dropping selected ids that no longer exist); Redo the reverse.
- **Dirty flag:** keep the history index of the last save; `dirty` is true when the current index differs from it. Undoing back to the
  saved state clears the dirty mark; redoing past it sets it again.
- History is cleared when another profile is opened, a profile is reloaded from the server, or an import replaces it.
- **Not undoable** (say so in the status bar when it happens): anything saved to the server immediately rather than through the open
  profile document: creating, deleting or duplicating a profile, profile-tree and folder changes of profiles, and page transfers between
  profiles (see the tree plan). These keep their existing confirmation dialogs.
- Edit → Undo / Redo show the step's label: "Undo Move widget" / "Widget taşımayı geri al".

## 5. Delete behaviour

Unchanged where a confirmation exists today (page delete asks; widget delete does not). With undo in place, no new confirmations are
added for undoable deletes. Deleting folders is defined in the hierarchy plan.

## Phases

1. Command registry, shortcut listener, Edit menu, `commandItem`, the browser-accelerator check; move the existing widget and page menu
   items onto commands. Panel focus commands.
2. Undo / redo (history in `useProfileDocument`, labels and coalescing on every `mutate` caller, dirty by save index).
3. Clipboard for widgets; Cut / Copy / Paste / Duplicate / Delete on the canvas and in its menus.
4. Tree clipboard (together with, or right after, the hierarchy plan's drag-and-drop, since it shares the move / copy function).

Each phase: `npm run typecheck`, `npm run build`, and a manual run of the editor. Check in particular that Ctrl+Z / Ctrl+C / Ctrl+V still
work natively inside a Properties text field, and that undo across a page switch returns to the right page.

## Changelogs

`CHANGELOG.md` `[Unreleased]`: New: Undo and redo; cut, copy and paste for widgets and pages; an Edit menu; keyboard shortcuts.
`CHANGELOG-developer.md`: nothing.

## Open questions

1. Should the clipboard also go to the system clipboard (as JSON) so widgets can be pasted between two editor sessions or shared as
   text? Not in this plan.
2. The shortcut table is a proposal; the owner may want different panel shortcuts.
