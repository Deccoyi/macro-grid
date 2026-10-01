# UI guidelines

The project has two surfaces with different design logic:

- The **editor** (`editor/`, React, shown in the server's WebView2 window) is a **desktop application**. People work in it for hours.
- The **phone app and browser deck** (the client repository and `webclient/`) are a **full-screen touch deck**.

These rules keep both from drifting into a generic web-dashboard look. Read this file when you build or change UI. The colors are in
[color-bible.md](color-bible.md).

## The editor is a desktop application, not a web dashboard

Think of code editors, IDEs and design tools: a persistent sidebar, a toolbar, a workspace, panels, context menus, a properties inspector,
lists and tables, a status bar. Take their interaction principles (persistent navigation, a clear workspace, context-sensitive controls,
compact information, keyboard-friendly interaction, efficient use of screen space), not their visual style. It should look right on a
1080p or 1440p screen and never like a responsive mobile site stretched to a desktop.

The question to ask for every new piece of UI is not "how would a web app do this" but "how would a real Windows desktop program solve it".

### Avoid

- Generic dashboard layouts, landing-page aesthetics, hero sections, giant headings, big KPI cards.
- Rounded cards everywhere, cards inside cards, floating glass panels, glassmorphism, blur, gradients (backgrounds or buttons), neon.
- Purple/blue/cyan "AI" color schemes, decorative glows, blobs, illustrations, large decorative icons.
- Too many badges, pills, status chips, shadows or floating elements; huge padding and empty space.
- Mobile-app controls in the editor: bottom navigation, floating action buttons, hamburger menus, large round buttons.
- Card-based settings pages, device lists or plugin lists. Use tables and lists.
- Tooltips, tabs, dividers, borders and animations that serve no purpose.

### Prefer

- Flat surfaces, thin dividers, restrained borders, compact rectangular or slightly rounded controls, clear hover and selected states.
- A strong left-to-right hierarchy, consistent alignment, sensible information density and precise spacing.
- A restrained color system: the accent color marks the active navigation, selection, focus, primary action and important states; the rest
  stays neutral.
- Compact typography with a clear hierarchy: no giant headings or numbers, no hairline weights, few font sizes.
- **No emoji or text symbols as icons.** Use a real component from `lucide-react` (already a dependency, also used by the icon picker).

A final check: if you removed the shadows, gradients, rounded cards, decorative icons and large type, would the UI still look good? If not, it
relies on decoration instead of layout, typography, spacing, hierarchy and alignment.

## Windows, dialogs and overlays

The editor must not feel like a web app, so modals, popups and overlays are the last resort, not the default.

Decide in this order:

1. Can it be solved in the existing **Properties panel**? Use it.
2. Can it be solved in the existing **workspace**? Use it.
3. Does it need a separate work area (preferences, plugins, pairing, help)? Open a real **desktop window** (`Ui/ToolWindow.cs`, one native window per
   tool). The Plugins, Preferences, Pairing, Help and plugin settings screens work this way. Like an ordinary desktop dialog, a tool window is
   owned by the editor window, stays above it and blocks it while open: clicking the editor plays the Windows warning sound and flashes the
   tool window instead of working behind it (`Owner` plus `Enabled = false`, not `ShowDialog`).
4. Only a short confirmation or warning? A small **dialog**.
5. Only momentary status? Inline feedback or the **status bar**.

The order of preference is workspace or panel, then window, then small dialog, then modal. Do not use modals for simple settings, forms, profile
or widget properties, plugin settings, device information, lists or short messages.

If a dialog is unavoidable, design it as a small application window, not a web modal: a clear title bar separated from the content, compact
content, no large padding, no big border radius, no glassmorphism or blurred backdrop, a clear close action, Escape to close, sensible keyboard
focus, normal desktop controls inside. Never a centered rounded card with a backdrop blur. Never a modal inside a modal, a popup inside a popup
or a full-screen darkening overlay.

**Notifications** are small, low-attention status messages (status bar, toolbar status, inline feedback), not a floating rounded card in the
bottom-right corner, and errors are not giant red banners. **Destructive actions** (delete, overwrite) ask for confirmation in a compact
desktop-style dialog (`confirmAsync` in `editor/src/dialogs/dialogStore.ts`), never the browser's native `confirm()`.

## Menus, panels and controls

- **Menu bar:** it behaves like a real application menu bar (File, Settings, Plugins, Help): compact, text-based, classic dropdown menus that can
  show shortcuts, Alt+letter opens a menu. It is not a website navigation bar. The menu bar holds commands; the toolbar holds frequent actions.
- **Context menus** follow desktop conventions: compact, flat, no rounded floating cards.
- **Properties panel:** a persistent panel on the right that works like a property inspector, not a settings page. Compact `Label | Value` rows,
  small section headers with a thin divider (no card per section), compact inputs with units (`Width [120] px`), a swatch plus hex for colors,
  checkboxes or small toggles instead of big switches, collapsible sections only where useful, a plain "No selection" when nothing is selected,
  common properties when several items are selected, its own independent scrolling, keyboard support (Enter, Escape, F2). Every widget type shows
  the same order: Appearance, the type-specific fields, Actions. Use the shared controls in `editor/src/panels/fields/controls.tsx`
  (`SectionLabel`, `ColorField`, `Seg`) instead of raw `<select>` elements and card boxes.
  The exact measurements, layouts and responsive rules for every field are in
  [Properties panel field system](#properties-panel-field-system) below; they apply to widget properties, page properties, plugin widget
  settings and every schema-driven form (`SchemaForm`).
- **Drawers** are not a default: use an existing persistent panel instead. When one is needed it does not cover the screen and behaves like part
  of the desktop panel.
- **Forms:** compact, aligned, inline validation and inline editing, no giant inputs.
- **Dynamic-value rules (Dynamize window) and its presets:** the layout, measurements and extension steps are in
  [dynamize-window-anatomy.md](dynamize-window-anatomy.md), with the mockups in [dynamize-window/](dynamize-window/). Read it before changing
  `DynamizeModal.tsx`, `ConditionEditor.tsx` or `quickTemplates.ts`, or before adding a preset.
- **Animation:** minimal and functional. No decorative micro-interactions.

## Properties panel field system

One system draws every field in the Properties panel, so a new widget, a new plugin setting or a new field kind never needs its own spacing.
All numbers are CSS px and live as tokens in `theme.css`; never write a literal gap, height or padding in a component. Colors are the existing
`--ms-*` variables.

**Mockups (open the files in a browser; start with [index.html](properties-panel-mockups/index.html)).** Look at them before building or changing a field; copy the example field that matches your kind instead of inventing spacing.
- [01-panel.html](properties-panel-mockups/01-panel.html): two complete panels at 320 px (a plugin widget with every setting kind, and a button widget with Appearance, Content and Actions).
- [02-field-kinds.html](properties-panel-mockups/02-field-kinds.html): the catalog of all 13 field kinds (Text, Password, Number, Slider, Bool, Select, Segmented, File, List, Button, Notice, Variable, Color) with their states (empty, filled, hover, focus, disabled, invalid). Use it as the example for each kind.
- [03-spec.html](properties-panel-mockups/03-spec.html): the measurements: token table, 2x anatomy with every px value, kind-to-layout table, grouping rules, the CSS snippet and the implementation notes.
- [04-responsive.html](properties-panel-mockups/04-responsive.html): the same field list at 240, 320 and 440 px, showing the container-query tiers described below.

**Settings window.** Pages in the Settings window (General, Global variable list, Automation, ...) follow the same field system; layout, tokens and mockups are in [settings-window-design.md](settings-window-design.md).

### Tokens

| Token | Value | Meaning |
|---|---|---|
| `--pf-pad` | 12 | Padding of a section on all four sides. Sections run edge to edge; the divider between them is a 1 px `--ms-border` line (no `hr.sep`, no outer gap). |
| `--pf-section-head` | 28 | Height of a collapsible section header (13 px, weight 600). 8 px between the header and the body. |
| `--pf-gap-field` | 12 | Vertical gap between two fields. The only vertical gap between fields, everywhere. |
| `--pf-gap-col` | 8 | Gap between fields placed side by side. |
| `--pf-label-h` | 18 | Height of the label row (12 px, `--ms-text-secondary`). Label text on the left, small actions on the right (Add variable, Make dynamic, Refresh), each 18 px high. |
| `--pf-ctl-h` | 28 | Height of every single-line control: text, number, select, color, segmented, button, file row, variable picker. Horizontal padding 8. Only a multi-line text area is taller. |
| `--pf-radius` | 4 | Radius of every control, list and note. |
| label to control / control to hint | 4 / 4 | Hint text is 11 px, line height 1.4, at most two lines. |
| group label | 16 high, 4 extra above | Small caps label (10.5 px) that splits a long section into groups. No extra space above the first group. Keep a group to six fields at most. |
| indent for a dependent field (`visibleWhen`) | 6 + 2 + 10 | 6 px left margin, a 2 px `--ms-border` guide line, 10 px padding. |

Anatomy of one stacked field, top to bottom: label row 18, 4, control 28, 4, hint (only if present), then 12 to the next field.

### Layout per field kind

Every field is `stacked` (label row above the control), `inline` (label left, control right, 28 px row) or `block` (no label).

| Kind | Layout | Can share a grid row | Notes |
|---|---|---|---|
| `Text`, `Password` | stacked | no | "Add variable" in the label row. Password eye button sits inside the input, right, 28x28. |
| `Number` | stacked | yes (2 or 3 per row) | Unit (`px`, `ms`) inside the input on the right, dimmed. |
| `Slider` | stacked | no | Track grows, then a 56 px editable value box. |
| `Bool` | inline | no | Label left, small toggle (28x16) right. A description goes under the label. |
| `Select` | stacked | yes (2 per row) | Native select with a chevron. Refresh icon in the label row when options load dynamically. |
| `Segmented` | stacked | yes (2 per row, three options at most) | Selected segment has a 2 px accent line at the bottom. More than three options or long labels: use `Select`. |
| `Color` | stacked | yes (2 or 3 per row) | Swatch plus mono hex, palette in a portal popover. |
| `File` | stacked | no | Read-only path (grows) and a Browse button, 6 px apart. |
| `Variable` | stacked | no | Picker button (grows) and a 28x28 clear button at the end of the row. |
| `List` | stacked | no | Row header 32 px (chevron, title, remove); an open row has 8 px padding, 12 px between its fields, and is indented 28 px on the left. "Add row" is a ghost button. |
| `Button` | block | no | Full width, 28 px. The result text goes below as a hint; errors in `--ms-danger`. |
| `Notice` | block | no | Icon and text, 11.5 px, warning tone, no label. At most two notices at a time. |

Consecutive fields that can share a row fill a grid in order. A short field never sits alone in a half-empty row unless the next field cannot
share one.

### Responsive behavior

The panel is resizable, so it never assumes a width. The panel root sets `container-type: inline-size`, and layout reacts to the panel width, not
the window width (use `@container`, never `@media`).

| Panel width | Behavior |
|---|---|
| under 240 | Compact: every grid is one column, section padding stays 12. |
| 240 to 299 | Three-column grids become two columns; two-column grids stay. |
| 300 to 399 | Default (the 320 reference): grids as defined above. |
| 400 and wider | Wide: single-value fields (`Number`, `Select`, `Color`, `Segmented`) switch to `Label | Value` rows: label in a fixed 120 px column, control fills the rest, 28 px row. Grids collapse to one column of such rows. |

Rules that keep every width working: grid children get `min-width: 0`; labels are one line and cut with an ellipsis (set `title` to the full
text, never wrap to two lines); controls fill their cell (`width: 100%`), never a fixed width, except the dependent-field input (96 px) and the
slider value box (56 px); the panel scrolls vertically on its own and never horizontally; popovers are portaled to `<body>`, positioned from the
trigger rect and flipped to stay inside the window.

### Sections and actions

- Order for every widget: Name, identity and notices, Appearance, Content or widget settings, Data, Actions, Custom CSS (collapsed).
- Notices sit in the identity section or directly above the field they explain.
- The make-dynamic bolt icon is the last item of the label row; filled accent when bound.
- Event cells are always four columns, square, 6 px apart; more events than four wrap to the next row.
- An action card has a 32 px header (number, name, remove), an 8 px padded body and 12 px between its fields; cards are 8 px apart. "Add action" is a
  24 px ghost button, left aligned with a negative 6 px margin so its text lines up with the fields.
- Disabled controls use 50 percent opacity and explain why in the hint. An invalid value gets a `--ms-danger` border and a hint line in
  `--ms-danger`. Focus keeps the accent border.

### Local tokens and rows inside a container

- A window may define a local layout token when it has a column no other screen has (for example the keyword column in the Dynamize window,
  `--dz-keyword-w`). Prefix it with the window's short name, define it in that window's own stylesheet and list it in that window's design doc. Heights, radii,
  gaps and paddings are never local: use the `--pf-*` tokens.
- Cards (32 px header, 8 px body padding) are for standalone items such as an action. Repeated rows inside one container (conditions in a
  rule, rows in a table) are hairline-separated rows with no card of their own, so there are never cards inside cards.

### Building fields

Use the shared components in `editor/src/panels/fields/controls.tsx`: `Field` (label row, action slot, hint, error), `FieldGrid` (`cols` 2 or 3),
`Switch`, `NumberInput` (with `unit`), `RangeInput`, `ColorField`, `Seg`, `SectionLabel`, `CollapsibleSection`. Do not write
`display: grid`, `gap`, `padding` or heights inline in a panel component. If a layout is missing, add it to the shared set first.

## The phone app and browser deck

The touch deck is not a desktop application: people touch it with a finger, so widgets must be large enough to tap (the grid cells already
guarantee this) and the compact-control rule does not apply. The same restraint applies otherwise:

- A widget's own colors and style belong to the user. The **chrome** (top bar, connection indicator, profile drawer, settings screen) stays plain
  and neutral: no gradients, glows or decorative icons.
- Connection state is a small dot or label, not a large colored banner.
- The profile drawer and settings are lists, not card grids.
- Nothing decorative besides widget content: the whole screen belongs to the functional grid.
- Do not shrink touch targets when adding widget types.

## Language

User-visible text in the editor goes through the i18n files (`editor/src/i18n/tr.ts` and `en.ts`), never hard-coded in components. Never build a sentence by joining translated pieces; use a text with `{name}` placeholders, or `.one`/`.other` keys for a count.
