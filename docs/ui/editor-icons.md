# Editor icons

The icon list for the editor's docking workspace, Hierarchy tree, Error List, Toolbox and edit menus
([../plans/docking-workspace-plan.md](../plans/docking-workspace-plan.md), [../plans/editor-edit-commands-plan.md](../plans/editor-edit-commands-plan.md),
[../plans/hierarchy-tree-and-folders-plan.md](../plans/hierarchy-tree-and-folders-plan.md)). The rules come from
[ui-guidelines.md](ui-guidelines.md) and [color-bible.md](color-bible.md); this file makes them concrete. The custom icons are drawn in
[editor-icons-preview.html](editor-icons-preview.html) (open it in a browser) at every size and state, in both themes.

Everything comes from `lucide-react` (already a dependency) except five small glyphs for the docking drop guides, which are drawn and
final — the SVG source files are in [icons/](icons/) (`dock-left.svg`, `dock-right.svg`, `dock-top.svg`, `dock-bottom.svg`,
`dock-center.svg`).

## Format (every chrome icon, bought or drawn)

- **SVG, outline only**, on a **24 x 24** grid, the Lucide style: `fill="none"`, `stroke="currentColor"`, `stroke-width="2"`,
  `stroke-linecap="round"`, `stroke-linejoin="round"`.
- **Live area 20 x 20** (2 px padding on every side); corner radius 2 on rectangles; no detail smaller than 2 units (it vanishes at
  12 px). No text, no gradients, no shadows, no second colour.
- **One colour, set by the parent**: the SVG uses only `currentColor`, and CSS sets `color` from a token. The only exception is the dock
  guide glyphs, which fill the target area with `currentColor` at 35 % opacity (`fill="currentColor" fill-opacity=".35"`).
- **In code**: the five dock glyphs are made with `createLucideIcon(name, iconNode)` from `lucide-react`, in `editor/src/icons/`
  (converting the path data in [icons/](icons/) to that call's node array is the only implementation step left), so they take the same
  props (`size`, `strokeWidth`, `color`, `aria-hidden`) and look the same as the Lucide ones. No image files, no icon font.
- Stroke width 2 at every size up to 16 px (Lucide scales it with the size). Never go under 12 px.

## Colours (by state; every value is a token from the colour bible)

| State | Token | Dark | Light |
|---|---|---|---|
| Default | `--ms-text-secondary` | `#9a9ea6` | `#565b63` |
| Hover, active, selected, open profile's row | `--ms-accent` | `#d97706` | `#b45f04` |
| Hover of an accent icon | `--ms-accent-hover` | `#f59e0b` | `#d97706` |
| On an accent background (a selected segment, the Save button) | `--ms-accent-on` | `#1c1e22` | `#ffffff` |
| Disabled | `--ms-text-disabled` | `#5b5e64` | `#9a9ea6` |
| Error severity only | `--ms-danger` | `#c0392b` | `#b02e21` |
| Main text colour, where an icon sits in front of primary text (the active tab) | `--ms-text-primary` | `#e6e7ea` | `#1c1e22` |

No other colour. Warning and Info are told apart by shape, never by colour (there is no warning token and none may be added).

## Sizes (by place)

| Place | Icon | Row or button |
|---|---|---|
| Hierarchy rows, auto-hide rail tabs, Error List rows, status bar | 14 px | 22 px rows |
| Panel header buttons (pin, close, view menu), tab close, Error List filter, sort arrows | 12 px | 18 px square button |
| Context menus and the menu bar's menus | 13 px (as today) | the existing menu row |
| Toolbar | 16 px | 26 px button |
| Toolbox items | 16 px | the palette tile |
| Dock guide targets | 16 px glyph | 28 px square target |

## Icon list

### Hierarchy tree

| Use | Icon | Notes |
|---|---|---|
| "Profiles" and "Plugins" roots | none | Section header rows: semibold text, no icon. |
| Expand / collapse | `ChevronRight`, rotated 90° when open | 12 px, in its own 12 px column before the icon; no chevron on rows that cannot expand. |
| Folder (both kinds) | `Folder` / `FolderOpen` | One icon for profile folders and page folders: page folders only ever sit under a profile, so the position already says which kind it is. |
| Profile | `LibraryBig` | Accent colour on the open profile's row. |
| Page | `LayoutTemplate` | Accent colour on the active document page. |
| Plugin, no manifest logo | `Puzzle` | A plugin with a manifest logo shows the logo at 14 px, as the Plugins window does (open question 1). |
| Plugin item | the Lucide name the plugin gives; fallback `Dot` | Chrome colour, like every tree icon. |
| Loading row | `LoaderCircle`, spinning 1 turn / s | Static when `prefers-reduced-motion` is set. |
| "Could not load. Retry" row | `RotateCw` | |
| Hierarchy view menu (header) | `EllipsisVertical` | |
| Drag label, copy mode | `Plus` | 12 px, before the "Copy 3 pages" text. |

### Docking

| Use | Icon | Notes |
|---|---|---|
| Pin (docked; click to auto-hide) | `Pin` | Upright pin. |
| Pinned-off (in the auto-hide flyout; click to dock again) | `Pin` rotated −90° (lying on its side) | The usual desktop convention: a pin on its side means "not pinned". One icon, rotated with CSS, no second drawing. |
| Close panel, close tab | `X` | |
| New page tab | `Plus` | |
| Floating panel header grip | `GripVertical` | Optional; the whole header is the drag handle anyway. |
| Menu check mark (View menu) | `Check` | 12 px, in the menu's check column. |
| Hierarchy (View menu, auto-hide tab) | `FolderTree` | |
| Toolbox | `Shapes` | |
| Properties | `Wrench` | Not a slider icon, so it is not confused with the slider widget in the Toolbox. |
| Error List | `ListX` | Not a severity icon, so it cannot be mistaken for one. |
| Dock guide targets | **five drawn glyphs**, [icons/](icons/) (below) | Drawn inside the drop-guide squares while a panel is dragged. |

### Error List and status bar

| Use | Icon | Colour |
|---|---|---|
| Error | `CircleAlert` | `--ms-danger` |
| Warning | `TriangleAlert` | `--ms-text-secondary` |
| Information | `Info` | `--ms-text-secondary` |
| Search box | `Search` | `--ms-text-disabled` inside the field |
| Clear | `Eraser` | default |
| Sort direction in the column header | `ChevronUp` / `ChevronDown` | 10 px, only on the sorted column |

### Edit commands (context menus and the Edit menu)

| Command | Icon |
|---|---|
| Undo / Redo | `Undo2` / `Redo2` |
| Cut / Copy / Paste | `Scissors` / `Copy` / `ClipboardPaste` |
| Duplicate | `CopyPlus` |
| Delete | `Trash2` (the item is drawn in `--ms-danger`, as today) |
| Rename | `PencilLine` |
| New folder | `FolderPlus` |
| New page, New profile, Open, Move / Copy to..., Select All | no icon (text only; the menu keeps its icon column empty) |

### Toolbox (widget types)

| Widget | Icon |
|---|---|
| `button` | `RectangleHorizontal` |
| `toggle` | `ToggleRight` |
| `slider` | `SlidersHorizontal` |
| `knob` | `CircleGauge` |
| `label` | `Type` |
| `image` | `Image` |
| `web` | `Globe` |
| `plugin-html` | `Puzzle` |

## The five dock guide glyphs

Shown at 16 px in the middle of the 28 px drop-guide squares, drawn in `--ms-accent` on `--ms-accent-bg-muted` with a 1 px `--ms-accent`
square border (see `component-states.html` in the docking mockups). Each is a window frame with the part where the panel will land
filled and separated by a line; `DockCenterGlyph`'s whole frame is filled, with a tab-strip line near the top. Final SVGs, drawn and
delivered, are in [icons/](icons/):

| Glyph | File | Filled part |
|---|---|---|
| `DockLeftGlyph` | `icons/dock-left.svg` | left third |
| `DockRightGlyph` | `icons/dock-right.svg` | right third |
| `DockTopGlyph` | `icons/dock-top.svg` | top third |
| `DockBottomGlyph` | `icons/dock-bottom.svg` | bottom third |
| `DockCenterGlyph` | `icons/dock-center.svg` | whole frame + tab strip: "joins this group as a tab" |

These use a solid `fill="currentColor"` on the landing-area path rather than the `fill-opacity=".35"` draft shown earlier in this file's
history; at implementation time, either add `fill-opacity=".35"` to that path or keep it solid and let the drop-guide's own
`--ms-accent-bg-muted` background carry the "translucent" read — check both against `component-states.html` and pick whichever reads
better at 16 px.

## Not icons (drawn with CSS, listed so nobody draws them)

- **Drop indicator in the tree:** a 2 px `--ms-accent` line with a 6 px hollow circle at its left end (above / below); a
  `--ms-accent-bg-muted` fill with a 1 px `--ms-accent` outline (inside). See the hierarchy plan.
- **Selection bar:** the 2 px `--ms-accent` left bar on the active page row.
- **Connection dot** in the status bar: a 6 px circle in `--ms-success` or `--ms-danger` (as today).
- **Auto-hide rail tab:** vertical text plus the panel's 14 px icon; no drawn tab shape.

## Open questions

1. Plugin logos in the tree: show them as the plugin supplies them (as the Plugins window does), or in one colour to keep the chrome
   quiet? This file assumes as supplied, 14 px.
