# Actions and macros

An action is something the PC does when a widget event fires. You add them in the **Actions** section of the inspector with **+ Add action**, which opens a searchable, categorized picker.

## Built-in actions

![The action picker, grouped by category and searchable](/img/editor-action-picker.png)

| Action | What it does |
|---|---|
| **Shortcut** (`core.hotkey`) | Presses a key combination such as `ctrl+shift+s`. Click the field and press the keys. |
| **Type text** (`core.typeText`) | Types a text. |
| **Open application** (`core.open`) | Starts a program or opens a file. Optional arguments. |
| **Open URL** (`core.openUrl`) | Opens an `http://` or `https://` address in your default browser. |
| **Change page** (`core.page`) | Go to a page, next, previous (wraps around) or back. |
| **Change profile** (`core.profile`) | Switches this device to another profile. |
| **Delay** (`core.delay`) | Waits up to 60000 ms. Use between actions. |
| **Master volume**, **Mute/unmute**, **Toggle mute** | Windows volume and mute. |

Plugins add more. The [OBS plugin](/guide/plugins#obs) adds 22.

The action names in the picker follow the editor language. Full list: [Actions reference](/reference/actions).

## Macros

Bind several actions to the same event. They run **in order**, one after another. A failing action is logged and reported without stopping the ones after it.

Example, "open my editor and start a recording":

1. **Open application** → your editor
2. **Delay** → 1500 ms
3. an OBS **Start recording** action

Use the arrows to reorder and **Remove** to delete.

## Variables inside actions

Plugin action fields that allow variables (for instance the OBS text source action) resolve `{variable}` before the action runs. Example: `Now streaming for {obs.stream.duration}`.

## When an action fails

If something goes wrong (a scene that no longer exists, a program that cannot start), the phone shows a red toast and the editor's status bar shows the error. Actions never fail silently.

## Order matters per device

Actions from one device run in order and off the receive loop, so a slow action such as a long delay never blocks the next touch from being read.
