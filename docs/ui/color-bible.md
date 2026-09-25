# Color system

The concrete side of [ui-guidelines.md](ui-guidelines.md). There are two color systems and they must not be mixed:

1. **Editor chrome palette:** window, sidebar, toolbar, panels, forms, icon color. Almost entirely grays plus a single accent color. This is
   what this document defines. The source of truth is `editor/src/theme.css` (the tokens below are copied from it).
2. **Widget and icon content palette:** the colors a user picks for their buttons and icons. These belong to the user, so they can be wide and
   colorful. The editor offers 16 starting swatches (below); the user can always enter any hex value.

Why amber for the accent: it evokes industrial instruments (indicator lights, dials), it stays out of the purple/blue/cyan gradient cliche, and it
stays restrained when used alone. Blue, purple, cyan and neon do not appear in the editor chrome.

The phone app and the renderer never use the chrome tokens: a widget is drawn only with the colors in its own `style`.

## Editor chrome, dark theme (default)

The CSS custom properties use the `--ms-` prefix.

| Token | Hex | Use |
|---|---|---|
| `--ms-bg-canvas` | `#1c1e22` | Main workspace (grid canvas) background |
| `--ms-bg-surface` | `#232529` | Panel, sidebar and toolbar background |
| `--ms-bg-surface-raised` | `#2b2e33` | Popover, dropdown, dialog |
| `--ms-bg-inset` | `#16171a` | Input fields, code editor (CSS box) background |
| `--ms-border` | `#35383e` | Default divider and border |
| `--ms-border-strong` | `#46494f` | Emphasized border, focus ring base |
| `--ms-text-primary` | `#e6e7ea` | Main text |
| `--ms-text-secondary` | `#9a9ea6` | Secondary text, default icon color |
| `--ms-text-disabled` | `#5b5e64` | Disabled text and icons |
| `--ms-accent` | `#d97706` | Active navigation, selection, primary action, focus, "attention" |
| `--ms-accent-hover` | `#f59e0b` | Hover of an accent element |
| `--ms-accent-bg-muted` | `rgba(217,119,6,.15)` | Low-intensity accent background, such as a selected row |
| `--ms-accent-on` | `#1c1e22` | Text and icons on an accent background |
| `--ms-success` | `#3f9142` | Connected device, active toggle indicator |
| `--ms-danger` | `#c0392b` | Error, lost connection, delete action |

**Rule:** apart from `--ms-accent`, `--ms-success` and `--ms-danger` there is no saturated color in the chrome. Do not color every button, icon or
panel: those three colors only express state and interaction.

## Editor chrome, light theme

The same roles on an inverted gray scale, with the three meaningful colors slightly darkened for contrast:

| Token | Hex |
|---|---|
| `--ms-bg-canvas` | `#f3f4f6` |
| `--ms-bg-surface` | `#ffffff` |
| `--ms-bg-surface-raised` | `#eceef1` |
| `--ms-bg-inset` | `#e5e7eb` |
| `--ms-border` | `#d4d7dc` |
| `--ms-border-strong` | `#b7bbc2` |
| `--ms-text-primary` | `#1c1e22` |
| `--ms-text-secondary` | `#565b63` |
| `--ms-text-disabled` | `#9a9ea6` |
| `--ms-accent` | `#b45f04` |
| `--ms-accent-hover` | `#d97706` |
| `--ms-accent-bg-muted` | `rgba(180,95,4,.12)` |
| `--ms-accent-on` | `#ffffff` |
| `--ms-success` | `#2f7a33` |
| `--ms-danger` | `#b02e21` |

## Icons

- **Chrome icons** (menus, toolbar, panel headers): one color, `currentColor` or `--ms-text-secondary`, and `--ms-accent` when active or selected.
  A constant stroke width, an outline icon set (Lucide), no filled, colored, 3D or shadowed icons.
- **Widget icons** (the icon a user puts on a button, including plugin icon packs): free. Flat colored SVGs are fine and matching the widget's
  color is the user's choice. Gradients, 3D bevels, shadows and glow effects are discouraged.

## Default widget swatches (16)

Offered in the editor's color picker as a ready-made palette: flat colors with enough contrast against white text, in the spirit of label colors
but not neon. The list is `SWATCHES` in `editor/src/panels/fields/controls.tsx`.

| Name | Hex | | Name | Hex |
|---|---|---|---|---|
| Graphite | `#374151` | | Slate | `#475569` |
| Red | `#b91c1c` | | Orange | `#c2410c` |
| Amber | `#b45309` | | Olive | `#84761f` |
| Green | `#15803d` | | Teal | `#0f766e` |
| Cyan | `#0e7490` | | Blue | `#1d4ed8` |
| Indigo | `#4338ca` | | Violet | `#6d28d9` |
| Magenta | `#a21caf` | | Pink | `#be185d` |
| Brown | `#78350f` | | Charcoal | `#111827` |
