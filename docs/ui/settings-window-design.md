# Settings window design

Design for the Settings window (General, Global variable list, Automation, and the other pages around them). It uses the same field system as the Properties panel ([ui-guidelines.md](ui-guidelines.md)): same control height, text size, radius and hairline logic, so the whole program reads as one product.

**Mockups:** open [settings-window-mockups/index.html](settings-window-mockups/index.html) in a browser. Copy the matching mockup instead of inventing spacing. UI copy in the mockups is the real Turkish text from `tr.ts`; the few strings that do not exist yet are listed under "New i18n keys".

| Mockup | Shows |
|---|---|
| [01-general.html](settings-window-mockups/01-general.html) | Window shell, navigation, General page |
| [02-global-variables.html](settings-window-mockups/02-global-variables.html) | Global variable list: table, inline description, invalid value, add bar |
| [03-automation.html](settings-window-mockups/03-automation.html) | Automation: pause switch, rule rows, one rule expanded |
| [04-trigger-kinds.html](settings-window-mockups/04-trigger-kinds.html) | Time trigger (time and day chips) and device trigger |
| [05-states.html](settings-window-mockups/05-states.html) | Empty lists, errors, limit reached |
| [06-spec.html](settings-window-mockups/06-spec.html) | Measurements and implementation notes |

## Principles

1. One design language. Settings controls are the Properties controls: 28 px high, 12 px text, 4 px radius, inset background, `--ms-border` border, accent border on focus. Do not create a settings-only control look.
2. Rows, not cards. Repeated rows inside one container are separated by 1 px hairlines. A card is only for a standalone item (an automation action step).
3. Booleans are switches (28x16), never checkboxes. Two to four exclusive options are a segmented control, longer lists are a select.
4. Every setting has a name (12 px) and an optional hint (11 px, secondary color). The control sits on the right in a fixed 220 px column.
5. Only widths may be window-local tokens (prefix `--st-`). Heights, radii, gaps and paddings come from `--pf-*` tokens.
6. Never invent copy. Text comes from `tr.ts`/`en.ts` keys; new text gets a new key in both files.

## Window shell

| Part | Value |
|---|---|
| Layout | `grid-template-columns: 188px minmax(0, 1fr)` |
| Navigation | panel background, 1 px hairline on the right, padding 12 px 8 px, gap 2 px |
| Nav group label | 16 px high, 10.5 px / 700 uppercase, 12 px above (not the first) |
| Nav item | 28 px, 13 px, radius 4 px, padding 0 10 px, icon 14 px + 8 px gap |
| Nav item selected | raised background + 2 px accent inset line on the left; `aria-current="page"` |
| Nav groups | App: General, Appearance, Language. Devices and profiles: Preview profiles, Profiles. Data and rules: Global variable list, Automation |
| Content | padding 24 px 32 px, page left-aligned |
| Page width | 640 px (General, simple pages), 760 px (Global variable list, Automation) |
| Page title | 16 px / 600; below it a 12 px description in secondary color, at most 640 px wide |
| Group head | 28 px high, 13 px / 600, 24 px between groups |

## Setting row

| Part | Value |
|---|---|
| Row | `grid-template-columns: minmax(0, 1fr) 220px`, column gap 24 px, padding 10 px 0, min height 48 px, bottom hairline; the first row also has a top hairline |
| Name | 12 px, primary text |
| Hint | 11 px, line height 1.4, secondary text, 2 px under the name |
| Control | 28 px, right column; a switch is right-aligned in the column |
| Footer button | 28 px raised button, alone, 24 px below the last group |

## Global variable list

Page width 760 px. Header: title, description (`globalVariables.hint`), count `n / 200` (11 px, secondary; warning color at the limit).

| Column | Width |
|---|---|
| Name | flexible, monospace 12 px, `user.` prefix in the dim color |
| Type | 104 px, text (not editable after creation) |
| Start value | 160 px; number and text use a text control, boolean uses a select (false, true, no value) |
| Keep | 56 px, switch centered |
| Delete | 28 x 28 ghost icon button, hover color danger |

- Column gap 8 px. Header row 24 px, 10.5 px / 700 uppercase, hairline below. Row padding 8 px 0 6 px, hairline below.
- The description is a second line in the row: 20 px high, 11 px, secondary color, borderless. Hover shows the inset background, focus shows the accent border. The placeholder is the dim color.
- Invalid start value: red border on the control and an 11 px red message below the row content (`role="alert"`).
- Add bar, 16 px below the table: name (flexible), type select 160 px, "Add variable" button. The button is disabled while the name is empty.

## Automation

Page width 760 px. Top bar: "Pause all rules" switch on the left, `n / 50` count on the right, hairline below.

- Rule row: `24 | 28 | 1fr | 28 | 28` with 8 px gap, min height 52 px, padding 8 px 0, hairline below. Parts: expand chevron (24 px icon button), enabled switch, summary, run button, delete button.
- Summary: rule name 12 px / 600, below it one 11 px status line. Status color: failed = danger, refused = warning, running = accent dot, otherwise secondary.
- Expanded editor: indented 36 px, at most 560 px wide, 12 px between fields, 16 px bottom padding.
  - Name: text field. Trigger: segmented control (value is true, specific time, device connected).
  - Condition: `grid-template-columns: 1.4fr 128px 1fr` (variable, comparison, value), 8 px gap.
  - Cooldown: number control 140 px wide with a "sec" unit text on the right inside the control (11 px, dim).
  - Actions: standalone step cards (border 1 px, radius 4 px). Card head 32 px with a 16 px numbered circle (accent muted background, accent text), title, and a 24 px delete button; body padding 8 px, 12 px between fields.
  - "Add action": ghost button 24 px high, aligned to the left edge. Below it the step limit hint (11 px).
  - Notes and problem messages: 11.5 px notes in an inset box (1 px border, radius 4 px, padding 6 px 8 px).
- "Add rule starting with" row at the bottom: three 28 px raised buttons, one per trigger.

### Trigger kinds

| Trigger | Fields |
|---|---|
| Value becomes true | condition row, hint |
| Specific time | time control 120 px, seven day chips (each 28 px high, equal width, 4 px gap). Chip on: accent muted background, accent border and text; `aria-pressed`. No selected day means every day |
| Device connected | device select |

## States

- Empty list: bold 12 px line plus a 12 px hint, centered in a bordered box. The add controls stay visible.
- Limit reached: count turns warning color, add controls are disabled, a hint explains which item to remove.
- Add error: red border on the field, 11 px red message below.
- Page error (load or save failed): single line alert at the top of the page, danger border and tinted background.

## Tokens

New global tokens in `editor/src/theme.css` (next to the other `--pf-*` tokens):

```css
--pf-page-pad-y: 24px;
--pf-page-pad-x: 32px;
--pf-group-gap: 24px;
--pf-row-pad: 10px;
--pf-nav-item-h: 28px;
```

Window-local widths (allowed, prefixed):

```css
--st-nav-w: 188px;
--st-ctl-w: 220px;
--st-page-w: 640px;
--st-page-w-wide: 760px;
```

Everything else reuses existing tokens: `--pf-ctl-h` (28), `--pf-radius` (4), `--pf-label-h` (18), `--pf-gap-field` (12), `--pf-gap-col` (8), and the `--ms-*` color tokens. Colors in the mockups map to: `--bg` panel, `--cv` canvas, `--raised`, `--inset`, `--bd`, `--bds`, `--tx`, `--tx2`, `--txd`, `--ac`, `--acm`, `--dg`, `--wr` of the matching `--ms-*` tokens.

## Responsive behavior

- Window width 720 px and up: the shell above.
- Below 720 px: the navigation is replaced by a 28 px select above the content, content padding becomes 16 px.
- Content narrower than 560 px: a setting row becomes one column (control full width under name and hint); a variable row splits in two lines (name + type, then value + keep + delete).

## Implementation notes

Files to change (names as in the current code):

- `editor/src/windows/ToolWindowLayout.tsx`: replace the 170 px flat navigation with the grouped 188 px navigation; page content sits in a `.st-page` wrapper.
- `editor/src/windows/PreferencesWindow.tsx` and `LanguageSection.tsx`: build the General and Language pages from the shared primitives below.
- `GlobalVariablesPage.tsx`, `AutomationPage.tsx`: use the table and rule row patterns above.
- New shared primitives (one place, no per-page styling): `SettingGroup`, `SettingRow`, `PageHeader`, `InlineText` (borderless inline edit), `DayChip`. Pages must not set their own colors or heights.
- Use the existing Properties controls (`controls.tsx`) for text, number, select, switch and segmented; do not add copies.
- Accessibility: switches use `role="switch"` + `aria-checked`, day chips use `aria-pressed`, icon buttons have `aria-label`, navigation items use `aria-current="page"`, errors use `role="alert"`.

### New i18n keys (both tr.ts and en.ts)

- Navigation group labels: App, Devices and profiles, Data and rules.
- General page group heads: Startup, Updates, Connection security.
- Empty-list titles and hints for variables and automation, and the limit-reached hints.
- Page description lines for pages that have none yet.

After adding keys run `UPDATE_DICTIONARY_OUTPUT=1 npx vitest run` and commit `dictionaryOutput.json`.

## Open points for the owner

- The mockups show the General page in three groups. The current page has no group heads; they are new copy.
- Appearance, Language, Preview profiles and Profiles pages follow the same row pattern but are not drawn here.
