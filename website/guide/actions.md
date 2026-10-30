# Actions and macros

An action is something the PC does when a widget event fires. You add them in the **Actions** section of the Properties panel with **+ Add action**, which opens a searchable, categorized picker.

## Built-in actions

![The action picker, grouped by category and searchable](/img/editor-action-picker.png)

| Action | What it does |
|---|---|
| **Shortcut** | Presses a key combination such as `ctrl+shift+s`. Click the field and press the keys. |
| **Type text** | Types a text. |
| **Open application** | Starts a program or opens a file. Optional arguments. |
| **Open URL** | Opens an `http://` or `https://` address in your default browser. |
| **Change page** | Go to a page, next, previous (wraps around) or back. |
| **Change profile** | Switches this device to another profile. |
| **Wait** (Logic) | Waits up to 60 seconds. Use between actions. |
| **Master volume**, **Mute/unmute**, **Toggle mute** | Windows volume and mute. |
| **Set variable**, **Add to or subtract from number**, **Toggle variable**, **Reset to start value** (Variable) | Change one of your [global variables](/guide/variables#your-own-variables). The older all-in-one **Set variable (old)** still works. |
| **If**, **Else**, **End**, **Stop** (Logic) | Decide which steps run. See [Logic](#logic-if-else-end-and-stop) below. |

Plugins add more. The [OBS plugin](/guide/plugins#obs) adds 22.

The action names in the picker follow the editor language. Full list: [Actions reference](/reference/actions).

## Logic: If, Else, End and Stop

Logic steps are in the **Logic** group of the picker. They let an action list decide what to run:

- **If** starts a block. Its steps run only when its test is met. The test is either a **condition** on variables (the same editor as [dynamic rules](/guide/dynamic)), or **the step before failed** / **the step before succeeded**.
- **Else** (optional) holds the steps that run when the If is not met. Each If has at most one Else. Use **Add else** to add one.
- **End** closes the block. Adding an If adds its End with it.
- **Stop** ends the whole list at that point. It is not a failure.

Blocks can be nested up to three levels deep. Collapse an If to see one line for it. Moving an If moves its whole block, and removing an If keeps the steps inside. A condition is read when the If is reached, so an earlier step can change the answer. Skipped steps do not count as the step before. A condition that is missing or cannot be read counts as not met.

**Wait** is in the same group and is set in seconds. Once a Wait has started it cannot be cancelled.

## Macros

Bind several actions to the same event. They run **in order**, one after another. A failing action is logged and reported without stopping the ones after it.

Example, "open my editor and start a recording":

1. **Open application** → your editor
2. **Wait** → 2 seconds
3. an OBS **Start recording** action

Use the arrows to reorder and **Remove** to delete.

## Variables inside actions

Plugin action fields that allow variables (for instance the OBS text source action) resolve `{variable}` before the action runs. Example: `Now streaming for {obs.stream.duration}`.

## When an action fails

If something goes wrong (a scene that no longer exists, a program that cannot start), the phone shows a red toast. The problem is also listed in **Diagnostic Messages** in the editor: one line per button, with a count. The line goes away when the button next works. Actions never fail silently.

## Order matters per device

Actions from one device run in order and off the receive loop, so a slow action such as a long delay never blocks the next touch from being read.
