# Roadmap

Where the project stands. The project is before 1.0.0 and under active development. For how things work see [architecture.md](architecture.md).

## Done

- **Server:** the tray application, WebSocket and editor API, JSON profile storage, the built-in actions (key presses, typing, opening programs and
  URLs, page and profile switching, delays, volume), live variables (`system.*`), text templates with formats, toggles, sliders and knobs with a
  two-way value, dynamic rules for colors, animation, icon and text.
- **Editor:** profiles and pages, drag-and-drop widget design with snapping and overlap checks, a style panel, custom CSS with sanitizer warnings, action
  assignment (press, long press, double tap), variable insertion, live preview, device list, plugin list, `.msprofile` export and import, preferences (language,
  theme, default profile), automatic profile switching rules.
- **Pairing and devices:** PIN and QR pairing, per-device tokens, a device list with revoke, a profile per device.
- **Automatic profile switching** by the active window, with a lock on the phone ([design/auto-profile-switch.md](design/auto-profile-switch.md)).
- **Layout patches and cached assets:** an editor save sends only what changed, icons cross the wire once
  ([design/layout-patch-and-assets.md](design/layout-patch-and-assets.md)).
- **Plugins:** a loader with isolated assemblies, hot loading (install, reload and remove without a restart), schema-driven settings forms, status bar
  items, icon packs, and JavaScript plugins in a sandbox with user-approved permissions ([design/js-plugin-runtime.md](design/js-plugin-runtime.md)).
  Official plugins live in the plugin repository: OBS, an icon pack and a JavaScript example.
- **Browser deck** served by the server at `/deck/`.
- **Packaging:** a single-file release build and an installer definition ([release.md](release.md)), a signed release build of the phone app.
- **Phone app** (its own repository): connection with saved servers and QR pairing, the profile drawer, page swipes, kiosk mode and orientation lock,
  keep-awake, automatic reconnection and an offline layout cache.

## Next

- **The `plugin-html` widget:** a plugin ships its own HTML and JavaScript widget. It would run in a sandboxed iframe on the client and talk to the
  server only through `postMessage`. It needs the widget type in the renderers, a bridge in the client and a message route on the server. Today the
  `plugin-html` type draws a placeholder.
- **The `web` widget** (an embedded page such as a live chat): today it draws a placeholder. The idea is an iframe first and, for pages that refuse to be
  framed, a native WebView positioned over the grid cell by a small Android plugin.
- **An async host API for JavaScript plugins** (today scripts are synchronous, so `host.http` blocks the plugin's own thread).

## Before a first public release

- Compile the installer with Inno Setup and test install, upgrade and uninstall on a clean PC (the script has not been compiled yet).
- Create the real signing key for the phone app (see the client repository's `docs/release.md`) and decide on code signing for the installer.
- Decide the version: the server is at `0.2.0` while several features since that release are new (see the changelogs).

## Known gaps

- **Localization is incomplete.** The editor is available in Turkish (default) and English, but the tray menu, some native dialogs and the default text of a
  boolean in a template (`Açık` / `Kapalı`) are Turkish only, and the phone app's screens are Turkish only.
- **The browser deck** does not announce the `assets` and `layout.patch` capabilities yet, so it receives full layouts with icons inline.
- **The CSS editor** is a plain text box with sanitizer warnings, without syntax highlighting.
- **Windows only.** The server depends on Windows APIs.

## Not planned

- **Automatic discovery of the server (mDNS).** Connect by entering the address or scanning the QR code in the editor's Pairing window.
- **Internet access or encryption.** The system is designed for a trusted local network (see the security model in [architecture.md](architecture.md#security-model)).
