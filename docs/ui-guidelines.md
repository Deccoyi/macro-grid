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
3. Does it need a separate work area (preferences, plugins, pairing, help)? Open a real **desktop window** (`ToolWindow.cs`, one native window per
   tool, non-modal). The Plugins, Preferences, Pairing, Help and plugin settings screens work this way.
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
- **Drawers** are not a default: use an existing persistent panel instead. When one is needed it does not cover the screen and behaves like part
  of the desktop panel.
- **Forms:** compact, aligned, inline validation and inline editing, no giant inputs.
- **Animation:** minimal and functional. No decorative micro-interactions.

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

User-visible text in the editor goes through the i18n files (`editor/src/i18n/tr.ts` and `en.ts`), never hard-coded in components.
