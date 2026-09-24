# Actions

Built-in action types are `core.*`; plugin actions are `<plugin id>.*`. In the action picker they are grouped by category. The names shown in the picker are currently in Turkish for the built-in and OBS actions, so the table lists both.

## Built-in

| Type | Picker name | Category | Settings |
|---|---|---|---|
| `core.hotkey` | Kısayol tuşu | Keyboard | Key combination |
| `core.typeText` | Metin yaz | Keyboard | Text to type |
| `core.open` | Uygulama aç | System | Application, arguments (optional) |
| `core.openUrl` | URL aç | System | URL starting with `http://` or `https://` |
| `core.delay` | Bekle | System | Delay in ms, up to 60000 |
| `core.page` | Sayfa değiştir | Page and profile | Mode: go to page, next, previous, back |
| `core.profile` | Profil değiştir | Page and profile | Profile |
| `core.setVolume` | Ana ses seviyesi | Audio | Uses the slider or knob value |
| `core.setMute` | Sesi kapat/aç | Audio | Mute or unmute |
| `core.toggleMute` | Sesi sessize al/aç | Audio | None |

## OBS plugin (`obs.*`)

| Group | Actions |
|---|---|
| Scenes | `obs.setScene`, `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode`, `obs.setTransition`, `obs.setProfile` |
| Stream and recording | `obs.startStream`, `obs.stopStream`, `obs.toggleStream`, `obs.startRecord`, `obs.stopRecord`, `obs.toggleRecord`, `obs.pauseRecord` |
| Outputs | `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay` |
| Audio | `obs.setMute`, `obs.toggleMute`, `obs.setVolume`, `obs.adjustVolume` |
| Items and text | `obs.setItemVisibility`, `obs.setText` |

## Events

| Widget | Events |
|---|---|
| Button | `press`, `release`, `longPress`, `doubleTap` |
| Toggle | `toggleOn`, `toggleOff` |
| Slider, Knob | `valueChange` |

Actions bound to one event run in order. See [Actions and macros](/guide/actions).
