# Dynamize window and Presets window: design anatomy

Audience: a coding agent that must extend this design or build it in `editor/` (React, `lucide-react`, tokens in `editor/src/theme.css`).
Status: mockup only, not applied. The current code is `editor/src/panels/dynamic/DynamizeModal.tsx`, `ConditionEditor.tsx`, `quickTemplates.ts`.
Mockups are in [dynamize-window/](dynamize-window/): open [index.html](dynamize-window/index.html) in a browser (`dynamize-window.html`, `presets-window.html`); `Dynamize.dc.html`, `Presets.dc.html` and `canvas.json` are the Design canvas sources.

Read `docs/ui/ui-guidelines.md` and `docs/ui/color-bible.md` first. This design follows them: flat, hairline dividers, no cards in cards, no gradients, no
blur, compact controls, accent only for selection, focus and the primary action.

## 1. One system: the Properties panel field system

This design does not define its own numbers. Spacing, control height, radius and label styles come from
[Properties panel field system](ui-guidelines.md#properties-panel-field-system) in ui-guidelines.md; use its tokens and shared components by name. The
shared components (`Field`, `FieldGrid`, `Switch`, `NumberInput`, `RangeInput`, `SectionLabel`, `ColorField`, `Seg`, the `Select`/`Text`/`Variable`
field kinds in `editor/src/panels/fields/controls.tsx`) may still be in progress; refer to them by these names and do not draw a second control.
Colors are only the `--ms-*` variables from `theme.css` (never new colors).

| Mockup value | Use |
|---|---|
| 28px control height | `--pf-ctl-h` (every single-line control: select, input, color, seg, normal button, variable chip) |
| 4px radius | `--pf-radius` |
| 10.5px small-caps label (`Then`, `Quick`, `Variable`, group headers) | `SectionLabel` (same style; no label above the first group) |
| 12px secondary label | label row, `--pf-label-h` high, 12px `--ms-text-secondary` |
| hint text | 11px, line-height 1.4 (not 12px), 4px below its control |
| vertical gap between fields / side by side | `--pf-gap-field` (12) / `--pf-gap-col` (8); label to control 4 |
| window and section padding | `--pf-pad` (12) |
| divider | 1px `--ms-border`, edge to edge, no `hr.sep` |
| compact in-row ghost action (`+ Condition`, `+ New rule`) | 24px high ghost, `margin-left: -6px` (the "Add action" style) |
| normal buttons (footer, quick chips) | 28px (`--pf-ctl-h`); there is no 26px button |
| nested/dependent indent, if conditions are indented | `.pf-nest`: 6px margin, 2px `--ms-border` line, 10px padding |

Surfaces and text (all `theme.css`): `--ms-bg-surface` window body and footer; `--ms-bg-canvas` recessed lists and the presets side list;
`--ms-bg-inset` inputs, selects, variable chips; `--ms-bg-surface-raised` hover and the active Seg segment; `--ms-border` / `--ms-border-strong`
dividers and the window outline; `--ms-text-primary|secondary|disabled` (the disabled gray only for decorative grips and unavailable items, it fails text
contrast); accent tokens for focus, the selected preset border, the primary button and the title icon tile; `--ms-danger` for remove actions only.
The window itself has no radius (square corners like a native window); rows inside a 1px line list use 3px.

**The one local token:** the keyword column (`If`, `Else if`, `Otherwise`, 58px in the mockup) is `--dz-keyword-w`, defined in the Dynamize window's
stylesheet. It is allowed under the rule in
[Local tokens and rows inside a container](ui-guidelines.md#local-tokens-and-rows-inside-a-container) (do not restate that rule here). Heights, radii,
gaps and paddings are never local; they are always `--pf-*`.

**Rows, not cards:** the rules and their condition rows are hairline-separated rows inside one container. Card anatomy is only for standalone items such
as an action, per the same subsection.

## 2. Type roles

- **Window title** 14/600 with a 12px secondary subtitle (the property name, mono in the Dynamize window).
- **Keyword** (`If`, `Else if`, `Otherwise`): 12px/600, primary, line-height `--pf-ctl-h` so it aligns with the first control row.
- **Small-caps label**: `SectionLabel`.
- **Mono**: variable names, values, hex colors: `ui-monospace, monospace` 12px.
- **Hint**: 11px secondary, one short sentence, never a paragraph.

## 3. Window frame (both windows)

A column flex box with a 1px `--ms-border-strong` outline, no radius, no shadow, no backdrop (it is an owned desktop window, see ui-guidelines
"Windows, dialogs and overlays"; in the app today it is still a modal, keep the overlay minimal if it must stay one).

1. **Title bar** 48px: 26px accent-muted tile with a 14px icon, title + subtitle column, spacer, 24px ghost close button. Bottom hairline.
2. **Body**: the only scrolling region.
3. **Footer** about 48px: top hairline, destructive ghost on the left (danger color), spacer, `Cancel` ghost, `Apply` primary on the right.

The Dynamize window is a separate tool window, so it gets **no responsive tiers** of its own. Decision: a fixed content width (680 in the mockup, never below 400) and the **unwrapped, stacked layout** of the field system (identical to its 320 reference); do not wrap the content in a `container-type: inline-size` element, so single-value fields never switch to the wide Label | Value row. The Dynamize window is 680 wide; the Presets window 720x540 (two columns, 230px | rest).

## 4. Dynamize window anatomy (top to bottom)

1. Title bar (icon = braces/variable, subtitle = property label).
2. **Quick bar** (hairline below): `QUICK` label, up to 3 chips (28px high, `--ms-bg-canvas`, hairline, 12px icon + label), a dashed `All presets...`
   chip that opens the Presets window, spacer, hint "First match wins."
3. **Rule list**: one row group per rule, hairline between rules, no card. Rule grid: `14px | 58px | 1fr`, gap 10, padding 12/10/12/12.
   - col 1: drag grip (14px, disabled color, top padding 7px).
   - col 2: keyword (`If` first, `Else if` after).
   - col 3 (stack, gap 10): condition stack, then the result row.
   - **Condition row** (grid `1.4fr | 128px | 1fr | 24px | 24px`, gap 6, 28px high): variable chip (the `Variable` field kind) | operator (`Select`) | value (`Text`/`NumberInput`, or `Select` for a
     boolean or declared values) | "use a variable as value" ghost icon | remove-condition ghost icon (only when there is more than one condition,
     otherwise an empty cell so the columns stay aligned).
   - Between condition rows: AND / OR / XOR is the shared `Seg` (28px, the selected segment gets the 2px accent line at the bottom); do not build a custom pill group.
   - `+ Condition` ghost button (24px, 12px icon).
   - **Result row**: `THEN` label (36px wide), arrow icon (disabled color), the result control (the shared `ColorField` as is, 170px wide here; select, text or icon picker for the
     other result kinds, same widths as today: 150 narrow, 260 wide), spacer, `Remove rule` ghost in danger color (only when there is more than one rule).
4. `+ New rule` ghost button aligned with the rule content column, in its own row with a hairline below.
5. **Otherwise row**: same grid, no grip, keyword `Otherwise`, arrow, result control (empty is allowed = "no change", with a small clear X), one hint sentence.
6. Footer.

## 5. Presets window anatomy

- **Left list** (230px, `--ms-bg-canvas`, right hairline): a search input (28px, search icon inside), then **groups by the variable type the preset needs**
  (`Boolean variable`, `Number variable`, `Any variable`, small-caps group headers), then rows (30px: 14px icon, name, `N rules` count at 11px secondary).
  Selected row: surface background + 1px accent border + accent icon. A preset with no matching variable is shown disabled (disabled colors, tooltip from
  `dynamic.quick.noVariable`), never hidden.
- **Right pane**: preset name 14/600 + a one-line description, `VARIABLE` label + variable chip (opens the shared `VariablePicker`, catalog filtered by
  `variablesFor(kind)`) + type/unit hint, then `ADDS THESE RULES, CHECKED IN THIS ORDER`: a read-only line list (1px gap table, rows 30px: index, mono
  condition, result swatch + value) that is the live output of `buildTemplate`, then a hint ("Added after your existing rules...").
- **Footer**: hint on the left, `Cancel`, primary `Add N rules` (N from the preview).
- The list is meant to grow: it scrolls, searches and groups. Do not turn it into a card grid.

## 6. Mapping to code

| Design part | Code |
|---|---|
| Rule keywords, rule list, Then/Otherwise rows | `DynamizeModal.tsx` (replace the boxed `cases.map` and the dashed else box) |
| Condition row, AND/OR/XOR, `+ Condition` | `ConditionEditor.tsx` (keep the logic, change the layout to the grid in 4.3; `chipStyle` stays) |
| Result control | `ResultInput` in `DynamizeModal.tsx` (behaviour unchanged) |
| Quick chips and the presets list | `quickTemplates.ts`: `TemplateKind`, `templateKinds(resultKind)`, `variablesFor`, `buildTemplate`, `applyTemplate` |
| Preview table in the Presets window | `buildTemplate(kind, variable, resultKind, words)` output, rendered read-only |
| Texts | `i18n/en.ts` and `tr.ts` (`dynamic.*`, `dynamic.quick.*`). New texts need both files. |
| Icons | `lucide-react`: Variable, ToggleRight, Gauge, CircleOff, Layers, Search, GripVertical, ArrowRight, Plus, X, Trash2, ChevronDown |

## 7. How to extend

- **Add a preset**: add the kind to `TemplateKind`; add its case to `buildTemplate`, `variablesFor` and (if it cannot work for some result kinds)
  `templateKinds`; add `dynamic.quick.<kind>` (name) and a description key to both i18n files; add an icon to the icon map; put it in a group of the
  Presets list by the variable type it needs (`Boolean`, `Number`, `Any`, or a new group only when a new type appears). The three most used presets may
  also get a Quick chip. Nothing else in the layout changes. Keep the output a plain `EditCase[]`, ordered so the first match wins.
- **Add a preset with parameters** (for example custom thresholds): add one labeled row per parameter in the right pane between `VARIABLE` and the
  preview, using the same 28px inset inputs; the preview must recompute from them.
- **Add a condition operator**: `OPERATOR_KEYS` in `ConditionEditor.tsx` plus i18n; the grid does not change.
- **Add a result kind**: extend `ResultKind` and `ResultInput`; reuse the 150/260px result widths.
- **Light theme**: nothing to do, only tokens are used.

## 8. Do and do not

- Do keep one hairline-separated list; do not wrap rules, presets or steps in cards or add a border to the rule row.
- Do keep the keyword column (`--dz-keyword-w`) and the `--pf-ctl-h` control height on every row so everything aligns on one vertical grid.
- Do show unavailable items disabled with a reason; do not hide them.
- Do use the accent for selection, focus and the single primary button only. Do not color rules by type; the only color is the user's result swatch.
- Do not add emoji or text symbols as icons, gradients, shadows, blur or a darkening backdrop.
- Do not put long prose in the window; one-sentence hints only.
- Do not stack a second modal on the Dynamize window. Open the Presets window as an owned tool window or as a popover anchored to the `All presets...`
  chip; a modal inside a modal breaks the guidelines.
