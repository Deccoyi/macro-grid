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

## Sliders and knobs

Set a slider's **variable driving its position** to `system.audio.master` and it shows the volume and moves when the volume changes elsewhere. See [the volume slider tutorial](/tutorials/volume-slider).
