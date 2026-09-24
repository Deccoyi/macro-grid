# Actions

Every action you can bind to a widget event. The names below are how they appear in the action picker. Built-in and OBS action names currently show in Turkish (the second column), whatever the editor language.

## Built-in

| Action | Picker name | Category | Settings |
|---|---|---|---|
| Shortcut | Kısayol tuşu | Keyboard | Key combination |
| Type text | Metin yaz | Keyboard | Text to type |
| Open application | Uygulama aç | System | Application, arguments (optional) |
| Open URL | URL aç | System | Address starting with `http://` or `https://` |
| Delay | Bekle | System | Milliseconds, up to 60000 |
| Change page | Sayfa değiştir | Page and profile | Go to a page, next, previous or back |
| Change profile | Profil değiştir | Page and profile | Profile |
| Master volume | Ana ses seviyesi | Audio | Uses the slider or knob value |
| Mute / unmute | Sesi kapat/aç | Audio | Mute or unmute |
| Toggle mute | Sesi sessize al/aç | Audio | None |

## OBS plugin

| Group | What you can do |
|---|---|
| Scenes | Change scene, preview scene, studio mode and transition, transition type, OBS profile |
| Stream and recording | Start, stop or toggle the stream and the recording, pause or resume recording |
| Outputs | Virtual camera, replay buffer, save replay |
| Audio | Mute, unmute or toggle an input, set its volume, adjust it by a step in dB |
| Items and text | Show, hide or toggle a scene item, set a text source |

## Events

| Widget | Events |
|---|---|
| Button | Press, Release, Long press, Double tap |
| Toggle | Turns on, Turns off |
| Slider, Knob | Value changed |

Actions bound to one event run in order. See [Actions and macros](/guide/actions).
