# Variable picker design ("Add variable" window)

Redesign of the window that opens from the "+ Add variable" button (`VariablePicker.tsx` inside `PickerShell.tsx`). Same field system as the Properties panel and the Settings window ([ui-guidelines.md](ui-guidelines.md), [settings-window-design.md](settings-window-design.md)).

**Mockups:** [variable-picker-mockups/index.html](variable-picker-mockups/index.html): [01-picker.html](variable-picker-mockups/01-picker.html) (the window), [02-states.html](variable-picker-mockups/02-states.html) (hover, focus, long description, no match), [03-spec.html](variable-picker-mockups/03-spec.html) (measurements). Variable names and descriptions in the mockups are sample data; all UI text uses existing i18n keys.

## Problem

Rows in the list are shown on top of each other and cannot be read. The rows are `button.ghost` elements with two lines of content (name + type badge, then description). A button with a fixed or control height cannot hold two lines, so the content spills into the next row. Verify the exact rule in the running app (computed height of a row) before fixing.

## Window

| Part | Value |
|---|---|
| Frame | 620 x 460, radius 6, 1 px hairline, panel background; backdrop 50 % black |
| Header | 40 px; title 13 px / 600 on the left; Close is a 28 x 28 icon button on the right (`picker.close` becomes its `aria-label`) |
| Category column | 160 px wide, padding 8, item 28 px / 12 px, count 11 px dim; selected: raised background + 2 px accent line on the left (same as Settings navigation) |
| Search | 8 px 12 px around it, hairline below; control 28 px with a 14 px search icon, `padding-left: 28px` |
| List | scrolls; rows separated only by a bottom hairline |

## Row

| Part | Value |
|---|---|
| Row | `grid-template-columns: minmax(0, 1fr) auto`, column gap 12, padding 8 px 12 px, `min-height: 48px`, `height: auto`, `white-space: normal` |
| First line | name (monospace 12 px) + type badge (10.5 px, 14 px line, radius 3, inset background, hairline) |
| Description | 11 px, line height 1.4, secondary color, 2 px under the name; at most 2 lines, then an ellipsis |
| Example token | right side, monospace 11 px, dim, single line (`{system.cpu|0}`) |
| Hover / focus | raised background / 2 px accent outline inside the row |

Rows never have a fixed height. Use one `.picker-row` class in `theme.css` instead of the inline styles and the `ghost` + `display: block` workaround.

## Implementation notes

- `PickerShell.tsx`: header and category column to the values above; Close as an icon button; `role="dialog"`, Esc closes. IconPicker uses the same shell, so the change applies to it too; the row class is specific to VariablePicker.
- `VariablePicker.tsx`: replace inline row styles with `.picker-row`.
- No new i18n keys: `variable.pickTitle`, `variable.searchPlaceholder`, `variable.noMatch`, `variable.values`, `picker.all`, `picker.close`.
- Open owner decision: this design keeps the modal. If it becomes a 280 px popover in the Properties panel, categories move to a segmented control above the list and the row stays the same.
