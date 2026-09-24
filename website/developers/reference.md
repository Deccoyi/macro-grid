# Technical reference

## Action types

Built-in types are `core.*`; plugin actions are `<plugin id>.*`.

| Type | Settings |
|---|---|
| `core.hotkey` | `keys`, for example `ctrl+shift+s` |
| `core.typeText` | text |
| `core.open` | `target`, arguments |
| `core.openUrl` | `url` (`http://` or `https://`) |
| `core.delay` | `ms`, up to 60000 |
| `core.page` | `mode`: go to a page, `next`, `prev`, `back` |
| `core.profile` | profile |
| `core.setVolume` | uses the slider or knob value |
| `core.setMute` | `mute` true or false |
| `core.toggleMute` | none |

OBS plugin: `obs.setScene`, `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode`, `obs.setTransition`, `obs.setProfile`, `obs.startStream`, `obs.stopStream`, `obs.toggleStream`, `obs.startRecord`, `obs.stopRecord`, `obs.toggleRecord`, `obs.pauseRecord`, `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay`, `obs.setMute`, `obs.toggleMute`, `obs.setVolume`, `obs.adjustVolume`, `obs.setItemVisibility`, `obs.setText`.

## Widget types and events

Types in profile files: `button`, `toggle`, `slider`, `knob`, `label`, `image`, `web`, `plugin-html` (the last two are placeholders).

| Widget | Events |
|---|---|
| Button | `press`, `release`, `longPress`, `doubleTap` |
| Toggle | `toggleOn`, `toggleOff` |
| Slider, Knob | `valueChange` |

## Network

| What | Where |
|---|---|
| Server | TCP port 9820, all interfaces |
| Devices (WebSocket) | `ws://<PC address>:9820/ws` |
| Browser deck | `http://<PC address>:9820/deck/` |
| Editor | `/editor/`; the `/api` endpoints answer only requests from the PC itself |
| QR code content | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

The protocol, the data model and the security model are in the [architecture document](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md).
