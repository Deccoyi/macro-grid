# Architecture

How Macro Grid is put together. For what is done and what is planned see [roadmap.md](roadmap.md); for building and working on the code see
[development.md](guides/development.md).

## Big picture

```
┌────────────── Windows PC ──────────────────────────────────────────────┐
│  MacroGrid.exe (tray app)                                           │
│   ├─ Kestrel, port 9820 ── /ws       WebSocket for phones and decks    │
│   │                     ── /api      editor API (this computer only)   │
│   │                     ── /editor/  the editor (React bundle)         │
│   │                     ── /deck/    the browser deck (React bundle)   │
│   ├─ WebView2 windows: the editor and its tool windows                 │
│   ├─ Core: profiles, actions, variables, sessions, plugins             │
│   └─ Windows: key input, audio, system metrics, foreground window      │
└───────────────▲────────────────────────────────────────────────────────┘
                │ WebSocket, JSON, local network only
     phone / tablet app (macro-grid-client)  ·  any browser at /deck/
```

The server is a Windows tray application. Profiles are designed in the editor, which runs inside the server's own WebView2 window. Phones and
tablets (the separate client app) and browsers (the deck at `/deck/`) connect over the local network, draw the layout and report touches; the
server runs the actions on the PC and pushes live values back.

## Projects in this repository

| Project | What it is |
|---|---|
| `src/MacroGrid.Host` | The executable: tray icon, WebView2 windows, Kestrel and the HTTP/WebSocket endpoints (`ServerApp.cs`, `Api/`). |
| `src/MacroGrid.Core` | The platform-independent logic: profile model and storage, actions, variables and templates, sessions and layout sending, pairing, plugin loading. |
| `src/MacroGrid.Protocol` | The WebSocket message types and payloads. |
| `src/MacroGrid.Windows` | Windows-specific parts: `SendInput` key presses, audio through NAudio (`WASAPI`), CPU and RAM, the foreground-window hook. |
| `src/MacroGrid.Plugin.Abstractions` | The plugin SDK: the interfaces plugins implement. |
| `editor/` | The editor (React and Vite). Built into `src/MacroGrid.Host/wwwroot/editor`. |
| `webclient/` | The browser deck (React and Vite). Built into `wwwroot/deck`. |
| `packages/renderer/` | The grid and widget renderer the editor and the deck use. The phone app has its own independent copy in the client repository (deliberately not kept in sync). |
| `tests/` | xUnit tests and a stub plugin used to test plugin loading. |

## Data model

Stored as JSON, one file per profile in `%AppData%\MacroGrid\profiles\` (written to a temp file and renamed, so a crash never leaves a
half-written profile).

- **Profile** `{ id, name, pages[], appMatches[], previewDeviceId? }`
- **Page** `{ id, name, cols, rows, gap, padding, alignment, widgets[] }`: a grid; a profile can have several pages.
- **Widget** `{ id, type, x, y, w, h, text, style, customCss, props, actions, dynamic }`
  - `type` is `button`, `toggle`, `slider`, `knob`, `label`, `image`, `web` or `plugin-html`. **The `web` and `plugin-html` types currently render a
    placeholder only;** the web widget (for example a live chat) and the plugin HTML bridge are not implemented yet.
  - `style`: `background`, `foreground`, `align`, `vAlign`, `fontSize`, `borderColor`, `borderWidth`, `radius`, `icon` (an image URL, normally
    a `data:` SVG baked by the icon picker), `iconSize`, `iconPosition`, `animation` (`none`, `blink`, `pulse`).
  - `props`: type-specific settings: `min`, `max`, `step` and `valueVariable` for a slider or knob, `src` for an image, `url` for a web widget.
  - `actions`: event name to an ordered list of `{ type, settings }`. Events: `press`, `release`, `longPress`, `doubleTap` for buttons,
    `toggleOn` and `toggleOff` for a toggle (a press flips the toggle instead of firing `press`), `valueChange` for a slider or knob.
  - `dynamic`: property path to a rule (see [Dynamic values](#dynamic-values)).
  - `customCss`: the user's own CSS for the widget (see [Custom CSS](#custom-css)).

Other data in `%AppData%\MacroGrid\`: `devices.json` (paired devices and their tokens, in plain text), `preferences.json`,
`plugins\<id>\` (installed plugins), `plugin-permissions.json` (approved permissions of JavaScript plugins), `logs\`.

## The WebSocket protocol

Every frame is `{ "type": "...", "data": { ... } }` in camelCase JSON (`Envelope`, `ProtocolJson.Options`). The types are in
`src/MacroGrid.Protocol/MessageTypes.cs`.

**Client to server:** `hello { deviceId, deviceName, token?, clientVersion, pin?, capabilities? }`, `widget.down`, `widget.up`, `widget.longPress`,
`widget.doubleTap`, `widget.value`, `page.change`, `page.next`, `page.prev`, `profile.change`, `profile.lock`, `asset.get`.

**Server to client:** `welcome` (with a new `token` right after a pairing), `layout.full`, `layout.patch`, `asset`, `page.show`, `widget.state`,
`profiles.list`, `error`.

- **Pairing.** The first time, a device sends `hello` with the PIN shown in the editor's Pairing window (six digits, valid for five minutes, kept
  in memory only). The server answers with a per-device token, which the device keeps and sends instead of the PIN from then on. The editor also
  shows a QR code that encodes `macrogrid://pair?host=<ip>&port=9820&pin=<pin>`. Devices can be revoked in the editor.
- **Layout.** After `hello` the server sends `layout.full` (the profile and the current page), then the live state of the page. A client that
  announces `assets` in `hello.capabilities` gets large `data:` values (icons, images) as `asset:<hash>` references and fetches each one once
  with `asset.get`; a client that announces `layout.patch` gets an editor save as a `layout.patch` with only the changed widgets. A client that
  announces neither gets the full layout with everything inline. Details: [design/layout-patch-and-assets.md](design/layout-patch-and-assets.md).
- **Live state.** `widget.state { widgetId, text?, value?, active?, style? }` pushes a rendered text, a slider position, a toggle state or
  dynamic style values. It is batched at 100 ms (at most about 10 updates a second) and only sent when the value for that client changed.
- **Actions run in order** per device, off the receive loop, so a slow action never delays reading the next message.
- **Navigation.** Page and profile changes belong to one device: `core.page` and `core.profile` actions and the phone's own swipes affect only
  the phone that triggered them, through its `SessionDeviceController`.

## Actions

Built-in actions and plugin actions implement the same interface, `IActionHandler` (`Type`, `DisplayName`, `ExecuteAsync`). Built-in types are
`core.*`, plugin types `<plugin id>.*`. Built in:

| Type | What it does |
|---|---|
| `core.hotkey` | Presses a key combination (`ctrl+shift+s`; `plus` is the `+` key), through `SendInput`. |
| `core.typeText` | Types a text. |
| `core.open` | Starts an application or opens a file. |
| `core.openUrl` | Opens a URL in the default browser. |
| `core.page` | Goes to a page, the next or previous page (wrapping around), or back. |
| `core.profile` | Switches the device to a profile. |
| `core.delay` | Waits (up to 60 seconds); use it between actions of a sequence. |
| `core.setVolume`, `core.setMute`, `core.toggleMute` | Master volume and mute (Windows core audio). |

A **macro** is simply several actions bound to the same event; they run one after the other, and a failing action is logged and reported without
stopping the ones after it. A failure is shown as a toast on the phone that pressed the widget and in the editor's status bar, so a stale binding
(a button pointed at a deleted OBS scene) is never silent. If an action handler also implements `IActionDescriptor`, the editor draws its settings
form from its `Fields`; text fields marked `AllowVariables` have their `{variables}` resolved by the server before the action runs.

## Variables and text

`VariableStore` holds live values (`system.time`, `system.cpu`, `system.ram`, `system.ram.used`, `system.ram.total`, `system.uptime`,
`system.audio.master`, `system.audio.muted`, and whatever plugins publish). Providers (`IVariableProvider`) run in the background and write to it;
setting a value equal to the current one does nothing.

A widget's text is a template: `Live: {obs.stream.duration}`, with optional formats: `{system.cpu|0}%`, `{system.time|HH:mm}`. `{{` and `}}`
produce literal braces. Numbers default to `0.##`, dates to `HH:mm`, durations to `hh:mm:ss`, and a boolean renders as `On` / `Off` (`Açık` / `Kapalı` when the language is Turkish, see `AppLanguage`) unless you give
it a format such as `{obs.streaming|ON/OFF}`. A missing variable renders as an empty string. `WidgetStateService` remembers which widget uses which variable, and when a value changes it renders
only the affected widgets and sends the ones whose text really changed.

A slider or knob with `props.valueVariable` shows that variable's value and, when dragged, sends `widget.value`, so a slider can control the
Windows volume and follow it when it is changed elsewhere.

## Dynamic values

A widget property can depend on a variable through rules made in the editor (the lightning-bolt button next to a field): "if `system.cpu` is
above 80 make the background red". The rules are plain data: conditions are `compare` (`>`, `>=`, `<`, `<=`, `==`, `!=`, `between`) combined
with `and`, `or`, `xor` and `not`; the first matching case wins, then the binding's default, then the widget's own static value. There is no
expression language and no code execution; the evaluator can only read a named variable and compare it (`DynamicRuleEvaluator`).

Properties that can be dynamic: `style.background`, `style.foreground`, `style.borderColor`, `style.animation`, `style.icon` and `text` (the result
may contain `{variables}` too). The server re-evaluates the affected rules when a variable changes and pushes the result in `widget.state`. The
editor has a small copy of the evaluator for its live preview; **the server's `DynamicRuleEvaluator` is what actually runs.**

### Variable types

Each catalog entry (`VariableInfo`) carries a `Type`: `text` (the default, also for plugins that declare nothing), `number` (with an optional
`Unit` such as `%` or `GB`), `boolean`, `duration` or `dateTime`. A text variable may list its allowed `Values`. The editor shows the type in
the variable picker and picks the value input of a condition from it: a true/false choice for a boolean, a list for `Values`, free input
otherwise (with the unit as a suffix for a number). Booleans and fixed values only offer `==` and `!=`.

In a condition a boolean matches `true` / `false` and `1` / `0` alike, case-insensitively; other operators never match it. The template words
(`On` / `Off`, `Açık` / `Kapalı`) are display only and do not match. A number is compared numerically and anything else as text, case-insensitively.

## Custom CSS

Each widget renders inside its own Shadow DOM. Its custom CSS is parsed with PostCSS and sanitized: properties that would let a widget change
its size or position are removed (`width`, `height`, `position`, `inset*`, `top`, `right`, `bottom`, `left`, `margin*`, `transform*`, `zoom`,
`display`), `@import` is removed, and `url()` may only point to `data:` URIs. Gradients, shadows and animations are allowed. The editor shows
warnings for what was removed.

## Plugins

Plugins add actions, variables, settings pages, status items and icon packs. They can be C# (full trust, in an isolated assembly load context)
or JavaScript (a Jint sandbox with approved permissions). They are installed, reloaded and removed while the server runs. The plugin SDK is
`MacroGrid.Plugin.Abstractions`; the guide for writing plugins is `docs/plugin-authoring.md` in the plugin repository. How it is built:
[design/js-plugin-runtime.md](design/js-plugin-runtime.md) and [design/layout-patch-and-assets.md](design/layout-patch-and-assets.md) (plugin hot loading).

## Automatic profile switching

A device can follow the foreground window on the PC: [design/auto-profile-switch.md](design/auto-profile-switch.md).

## Security model

Macro Grid is meant for a home or office network you trust. It is not hardened for the open internet.

- **Nothing is encrypted.** Traffic is plain `ws://` and `http://` on the local network. The phone app allows cleartext for this reason.
- **The server listens on all network interfaces** on port 9820. Do not forward the port to the internet, and allow it in the firewall only for
  private networks (the installer does this).
- **Pairing protects the WebSocket.** A device must present the PIN once, then a token. Tokens are stored in plain text in `devices.json`.
  Anyone who can read your `%AppData%` folder can read them.
- **The editor API (`/api`) is only for this computer.** It can read the pairing PIN, install and approve plugins and change profiles, so the
  server answers `403` to any `/api` request that does not come from the machine it runs on. The static editor and deck pages themselves are
  served to the network but do nothing without the API and the WebSocket.
- **Actions run as you.** A paired device can press keys, type text and start programs on the PC, so pair only devices you trust and revoke the
  ones you do not.
- **C# plugins have full trust** and can do anything the server can. Install only ones you trust. JavaScript plugins are sandboxed and need
  approved permissions.
- **The server checks for updates on its own, once every few hours.** This is the one connection it makes without being asked, and it is on by
  default (Preferences, General, "Check for updates automatically"; off stops the schedule, "Check for updates" still works by hand). It reads
  the public releases list of the project on `api.github.com` (HTTPS, an ETag so an unchanged list costs nothing, a `User-Agent` with the app
  version and nothing else) and sends nothing about the person or the PC. Nothing is downloaded or installed until the person clicks
  "Install now". The installer is fetched only from HTTPS addresses on GitHub (every redirect is checked against the same list), must match the
  SHA-256 GitHub reports for the release file, and runs only after Windows asks for administrator permission. The installer is not code-signed,
  so this protects against a broken download but **not against a compromised GitHub account** or release. Release notes are shown as text,
  never as HTML. Design: [design/auto-update.md](design/auto-update.md).
- **The user agreement has to be accepted, per Windows user.** The setup shows it (an update only when its text changed) and records its SHA-256 in `HKCU`. At start, before the server exists, the app compares that record with the agreement it ships and asks once when they differ (another Windows user of the same PC, or a changed text); declining exits. Until then nothing listens and nothing runs. Design: [design/agreement-acceptance.md](design/agreement-acceptance.md).
