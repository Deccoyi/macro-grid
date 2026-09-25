# Roadmap

Where the project stands. The project is before 1.0.0 and under active development. For how things work see [architecture.md](architecture.md).

## Done

- **Server:** the tray application, WebSocket and editor API, JSON profile storage, the built-in actions (key presses, typing, opening programs and
  URLs, page and profile switching, delays, volume), live variables (`system.*`), text templates with formats, toggles, sliders and knobs with a
  two-way value, dynamic rules for colors, animation, icon and text.
- **Editor:** profiles and pages, drag-and-drop widget design with snapping and overlap checks, a style panel, custom CSS with sanitizer warnings, action
  assignment (press, long press, double tap), variable insertion, live preview, device list, plugin list, `.msprofile` export and import, preferences (language,
  theme, default profile, start with Windows and what a start does: tray only or open the editor window), automatic profile switching rules, settings windows
  that block the editor while open, a Help window with the disclaimer, the user agreement and all bundled license texts.
- **Pairing and devices:** PIN and QR pairing, per-device tokens, a device list with revoke, a profile per device.
- **Automatic profile switching** by the active window, with a lock on the phone ([design/auto-profile-switch.md](design/auto-profile-switch.md)).
- **Layout patches and cached assets:** an editor save sends only what changed, icons cross the wire once
  ([design/layout-patch-and-assets.md](design/layout-patch-and-assets.md)).
- **Plugins:** a loader with isolated assemblies, hot loading (install, reload and remove without a restart), schema-driven settings forms, status bar
  items, icon packs, and JavaScript plugins in a sandbox with user-approved permissions ([design/js-plugin-runtime.md](design/js-plugin-runtime.md)).
  Official plugins live in the plugin repository: OBS, an icon pack and a JavaScript example.
- **Browser deck** served by the server at `/deck/`.
- **Packaging:** a single-file release build and a Windows installer with a user agreement, tested on a clean company PC (install, WebView2 setup, start with
  Windows, upgrade, uninstall; [release.md](release.md)); a signed release build of the phone app; the plugin SDK is published on NuGet.
- **Phone app** (its own repository): connection with saved servers and QR pairing, the profile drawer, page swipes, kiosk mode and orientation lock,
  keep-awake, automatic reconnection and an offline layout cache.

## Next

- **The `plugin-html` widget:** a plugin ships its own HTML and JavaScript widget. It would run in a sandboxed iframe on the client and talk to the
  server only through `postMessage`. It needs the widget type in the renderers, a bridge in the client and a message route on the server. Today the
  `plugin-html` type draws a placeholder.
- **The `web` widget** (an embedded page such as a live chat): today it draws a placeholder. The idea is an iframe first and, for pages that refuse to be
  framed, a native WebView positioned over the grid cell by a small Android plugin.
- **A logo (avatar) for plugin packages:** a plugin can ship a small image (SVG or PNG, square) and name it in an optional manifest field, so the Store
  (list and detail page) and the editor's Plugins window show it instead of the generic category glyph. It is an additive manifest field, so older
  hosts ignore it. Do it with the next manifest or SDK change, or earlier if it fits. Plugin packages and the Store catalog need the file rules
  (size limit, format check) and the release zip must include the image. See `plugin-distribution-plan.md`, section 3c.
- **An async host API for JavaScript plugins** (today scripts are synchronous, so `host.http` blocks the plugin's own thread).

## Before a first public release

- Decide on code signing for the installer (it is unsigned, so Windows shows an unknown-publisher warning).
- Publish the first release as an alpha (`server-v0.2.0-alpha`, `client-v0.1.0-alpha`): merge `dev` into `main`, tag, attach the installer and the APK to the draft releases.
- After the repositories are public: turn on private vulnerability reporting, the tag protection for `sdk-v*` and the documentation site.

## Known gaps

- **Localization covers Turkish and English only.** The editor, the tray menu, native dialogs, plugin texts and the phone app follow the language (Windows display language by default, then the preference). Setting field labels of a plugin need an entry in the plugin's `locales/<language>.json` to be translated; texts built at run time (for example a per-item variable description) stay in the plugin's default language.
- **The browser deck** does not announce the `assets` and `layout.patch` capabilities yet, so it receives full layouts with icons inline.
- **The CSS editor** is a plain text box with sanitizer warnings, without syntax highlighting.
- **Windows only.** The server depends on Windows APIs.

## Not planned

- **Automatic discovery of the server (mDNS).** Connect by entering the address or scanning the QR code in the editor's Pairing window.
- **Internet access or encryption.** The system is designed for a trusted local network (see the security model in [architecture.md](architecture.md#security-model)).
