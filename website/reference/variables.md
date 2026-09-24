# Variables

Use them in widget text as `{name}` or `{name|format}`, and in [dynamic rules](/guide/dynamic). See [Variables and text](/guide/variables) for formats.

## System

| Variable | Meaning |
|---|---|
| `system.time` | Current time |
| `system.cpu` | CPU load (%) |
| `system.ram` | RAM use (%) |
| `system.ram.used` | RAM used (GB) |
| `system.ram.total` | RAM installed (GB) |
| `system.uptime` | Time since Windows started |
| `system.audio.master` | Master volume (0 to 100) |
| `system.audio.muted` | `true` when muted |

## OBS plugin

All start with `obs.`.

| Group | Variables |
|---|---|
| Connection | `obs.connected`, `obs.status`, `obs.ws.in`, `obs.ws.out` |
| Scenes and state | `obs.scene.current`, `obs.scene.preview`, `obs.studioMode`, `obs.transition.current`, `obs.profile.current`, `obs.sceneCollection.current` |
| Stream | `obs.streaming`, `obs.stream.reconnecting`, `obs.stream.duration`, `obs.stream.timecode`, `obs.stream.congestion`, `obs.stream.bytes`, `obs.stream.kbps`, `obs.stream.frames.dropped`, `obs.stream.frames.total`, `obs.stream.frames.droppedPercent` |
| Recording | `obs.recording`, `obs.record.paused`, `obs.record.duration`, `obs.record.timecode`, `obs.record.bytes`, `obs.record.kbps` |
| Outputs | `obs.virtualcam`, `obs.replayBuffer` |
| Statistics | `obs.stats.fps`, `obs.stats.cpu`, `obs.stats.memory`, `obs.stats.disk`, `obs.stats.renderTime`, `obs.stats.render.skipped`, `obs.stats.render.total`, `obs.stats.render.skippedPercent`, `obs.stats.output.skipped`, `obs.stats.output.total`, `obs.stats.output.skippedPercent` |
| Per input and item | `obs.input.<slug>.muted`, `obs.input.<slug>.volumeDb`, `obs.item.<scene>.<source>.visible` |

A **slug** is the name in lower case with every run of characters other than `a-z` and `0-9` replaced by `_`. For example the input `Mic/Aux` becomes `mic_aux`.

Other plugins publish their own variables; the picker lists everything currently available.
