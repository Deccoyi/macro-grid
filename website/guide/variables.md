# Variables and text

A **variable** is a live value the server keeps up to date, such as the CPU load. Widget text is a template that can show variables.

## Using a variable

In a widget's **Text** field, type `{name}` or click **+ Add variable** to pick from a searchable list:

```text
CPU {system.cpu|0}%
Time {system.time|HH:mm}
Live: {obs.stream.duration}
```

Only widgets that use a changed variable are re-rendered, and only text that really changed is sent to the device (at most about ten updates a second).

## Formats

Add a format after a `|`:

| Kind | Default | Example |
|---|---|---|
| Number | `0.##` | `{system.cpu\|0}` shows `23` |
| Date/time | `HH:mm` | `{system.time\|HH:mm:ss}` |
| Duration | `hh:mm:ss` | `{system.uptime}` |
| Boolean | `Açık` / `Kapalı` | `{obs.streaming\|ON/OFF}` |

Write two opening braces or two closing braces in a row to get literal braces. A missing variable renders as empty text.

While a variable has no value, a text can show a placeholder instead: `{name|format|placeholder}`, for example `CPU {system.cpu|0|--} %`. `{name||n/a}` keeps the default format. Without a placeholder, a variable with no value shows nothing.

::: info Booleans
Without a format, a boolean shows the Turkish words *Açık* (on) and *Kapalı* (off). Give it your own format such as `{system.audio.muted|MUTED/LIVE}` to control the wording.
:::

## Built-in variables

| Variable | Meaning |
|---|---|
| `system.time` | Current time |
| `system.cpu` | CPU load, percent |
| `system.ram` | RAM use, percent |
| `system.ram.used`, `system.ram.total` | RAM in GB |
| `system.uptime` | Time since Windows started |
| `system.audio.master` | Master volume, 0 to 100 |
| `system.audio.muted` | `true` when muted |

Plugins add many more (the OBS plugin adds about 45). See [Variables reference](/reference/variables). Click **Refresh variables** in the editor header after installing a plugin.

## Your own variables

Define your own in **Preferences → Global Variable List**: a text, a number or a True / False value. Use it anywhere as `{user.name}`: in a text, in a rule or in an action. The **Set variable** action changes it (see [Actions and macros](/guide/actions)).

- The name and type cannot be changed later. To change either, delete the variable and add a new one.
- After a restart it starts again from its start value, unless you switch **Keep** on for it.
- Deleting a variable that is still in use first lists where it is used. Those places show nothing until you fix them.

## Unavailable variables

A variable with no value is **unavailable**. For example, its plugin is switched off, or a number variable has no start value. A text shows nothing for it (or its placeholder). A rule can test for it with `unavailable` or `available`. A button whose action reads an unavailable variable looks grey on the phone with a small warning mark. See [The phone app](/guide/phone-app).

## This button

Five variables under **This button** describe the button itself. They work in its own text and rules:

- `self.toggled`: on while a toggle is on.
- `self.busy`: on while the action list of this button has been running for more than 150 ms.
- `self.pressed`: on while your finger is down, for at least 150 ms.
- `self.lastResult`: `Success` or `Failed`, how the last run ended.
- `self.lastError`: the code of the first failure of the last run.

They have no value in an action or in a slider's variable.

## Sliders and knobs

Set a slider's **variable driving its position** to `system.audio.master` and it shows the volume and moves when the volume changes elsewhere. See [the volume slider tutorial](/tutorials/volume-slider).
