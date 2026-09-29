# Roadmap

Where the project stands. The server and the SDK are at 1.0.0 and under active development. For how things work see [architecture.md](architecture.md).

## Done

- **Server:** the tray application, WebSocket and editor API, JSON profile storage, the built-in actions (key presses, typing, opening programs and
  URLs, page and profile switching, delays, volume), live variables (`system.*`), text templates with formats, toggles, sliders and knobs with a
  two-way value, dynamic rules for colors, animation, icon and text.
- **Editor:** profiles and pages, drag-and-drop widget design with snapping and overlap checks, a style panel, custom CSS with sanitizer warnings, action
  assignment (press, long press, double tap), variable insertion, live preview, device list, plugin list, `.msprofile` export and import, preferences (language,
  theme, default profile, start with Windows and what a start does: tray only or open the editor window), automatic profile switching rules, settings windows
  that block the editor while open, a Help window with the disclaimer, the user agreement and all bundled license texts.
- **Docking workspace and hierarchy tree:** dockable, tabbed, floating and auto-hide tool windows, page tabs, a remembered layout
  ([design/docking-workspace.md](design/docking-workspace.md)); one tree for every profile and page, with page and profile folders,
  drag-and-drop, copy/paste and lazily loaded profiles ([design/hierarchy-tree-and-folders.md](design/hierarchy-tree-and-folders.md)).
  The Error List has no producer yet and plugin-provided tree entries (phase 6) are not started, see "Next" below.
- **Editor edit commands:** right-click Undo/Redo/Cut/Copy/Paste/Duplicate/Delete/Select All, an Edit menu, one shared undo/redo
  history stack, standard Windows shortcuts, and a header toolbar ([design/editor-edit-commands.md](design/editor-edit-commands.md)).
- **Pairing and devices:** PIN and QR pairing, per-device tokens, a device list with revoke, a profile per device.
- **Encrypted connection:** the server makes its own certificate and serves `wss://`/`https://` on a second port next to the
  plain one; the pairing QR carries its fingerprint, so the phone app pins it with no certificate authority needed. Server side
  in Macro Grid 1.1.0/1.2.0, phone app side (a native WebSocket that checks the fingerprint) in `client-v0.3.0`. The browser
  deck stays on the plain port, a browser cannot pin a fingerprint.
- **Plugin secret storage:** `IPluginHost.Secrets` lets a plugin protect a value (DPAPI-backed), so a setting like a password
  is not stored in plain text; the OBS plugin uses it for its own password.
- **"Remove my data" on uninstall:** the installer offers to delete `%AppData%\MacroGrid` (profiles, paired devices, plugins,
  logs), off by default, never asked on a silent uninstall.
- **Automatic profile switching** by the active window, with a lock on the phone ([design/auto-profile-switch.md](design/auto-profile-switch.md)).
- **Layout patches and cached assets:** an editor save sends only what changed, icons cross the wire once
  ([design/layout-patch-and-assets.md](design/layout-patch-and-assets.md)).
- **Plugins:** a loader with isolated assemblies, hot loading (install, reload and remove without a restart), schema-driven settings forms, status bar
  items, icon packs, and JavaScript plugins in a sandbox with user-approved permissions ([design/js-plugin-runtime.md](design/js-plugin-runtime.md)).
  Official plugins live in the plugin repository: OBS, an icon pack and a JavaScript example.
- **Browser deck** served by the server at `/deck/`.
- **Packaging:** a single-file release build and a Windows installer with a user agreement, tested on a clean company PC (install, WebView2 setup, start with
  Windows, upgrade, uninstall; [release.md](guides/release.md)); a signed release build of the phone app; the plugin SDK is published on NuGet.
- **Phone app** (its own repository): connection with saved servers and QR pairing, the profile drawer, page swipes, kiosk mode and orientation lock,
  keep-awake, automatic reconnection, an offline layout cache, and auto-update (checks GitHub, downloads and verifies a release-signed APK, hands it
  to Android's installer; [design/phone-app-auto-update.md](design/phone-app-auto-update.md)).
- **Plugin distribution:** a Discover tab in the editor's Plugins window — browse the official catalog, add a third-party multi-plugin source, install
  a single plugin from a pasted repository link, badges (Official / Third-party / Local) and "update available"
  ([design/plugin-distribution.md](design/plugin-distribution.md)).
- **Plugin SDK 0.4.0:** file, list, button and notice setting fields, an action that learns when its button is released, a settings page that runs a
  host command (used for a sound preview), and an optional plugin icon; first used by the official **SoundBoard** plugin
  ([design/sdk-0.4.0-and-soundboard.md](design/sdk-0.4.0-and-soundboard.md)).

## Next

The order of the bigger pieces of work, and their plans, are in [plans/README.md](plans/README.md). The items below have no plan file yet.

- **Rename the plugin manifest's `macroGrid` field (priority, name not chosen):** it means "the oldest Macro Grid this plugin runs on", but the name alone
  does not say "minimum", which was not obvious on a first read. Wanted: a clearer name (`minMacroGrid`, `requiresMacroGrid`, or better), with the old
  name still read for at least one MAJOR so existing plugins keep working unchanged. Planned for the version after 1.0.x; no name decided yet.
- **The "Allow unencrypted connections" preference:** now that the server and the phone app both support `wss://` (see "Done"
  above), plain `ws://`/`http://` (port 9820) stay open unconditionally; a preference to turn them off is not built yet. Default
  on for now (the browser deck cannot use `wss://` at all, and older paired phones have no TLS support), default off starting
  the next MAJOR version once both are settled. See [plans/security-hardening-plan.md](plans/security-hardening-plan.md), part A.
- **A branded installer:** today the setup uses the plain modern wizard style with Inno Setup's default pictures and no icon of its own. Wanted: the
  Macro Grid logo and the product colors. What the setup tool can do natively: an icon for the setup file and the uninstaller (`SetupIconFile`, from
  `src/MacroGrid.Host/app.ico`), the large picture on the welcome and finished pages (`WizardImageFile`), the small logo in the corner of the
  other pages (`WizardSmallImageFile`, several sizes for high-DPI screens), the color behind the large picture, and the texts on each page. What it
  cannot do natively: restyle the buttons, fonts and page background in our accent color; that needs a third-party skin library, which is not
  worth its size, its licence and the antivirus false alarms it can bring. So the plan is the native part, drawn from `docs/ui/color-bible.md`
  and `website/public/logo.png`. The automatic update shows few pages (see `design/agreement-acceptance.md`), so the logo appears mostly
  in its progress window. The exact picture sizes are checked against the Inno Setup version the release workflow installs. It needs a small
  plan file first (`plans/`).
- **A much faster install and update (fewer files):** done, see [done/faster-install-plan.md](done/faster-install-plan.md).
- **The `plugin-html` widget:** a plugin ships its own HTML and JavaScript widget. It would run in a sandboxed iframe on the client and talk to the
  server only through `postMessage`. It needs the widget type in the renderers, a bridge in the client and a message route on the server. Today the
  `plugin-html` type draws a placeholder.
- **The `web` widget** (an embedded page such as a live chat): today it draws a placeholder. The idea is an iframe first and, for pages that refuse to be
  framed, a native WebView positioned over the grid cell by a small Android plugin.
- **An async host API for JavaScript plugins** (today scripts are synchronous, so `host.http` blocks the plugin's own thread).
- **The rest of a store-like Discover tab.** Discover now shows a card grid and a per-plugin detail view (description, author,
  homepage, declared permissions, install/update) built from what the catalog already carries, in a wider Plugins window. Still
  open, and each needs a catalog field first: a real plugin icon (cards show the plugin's initial on a stable color for now),
  screenshots, category/tags to browse by, and (later) install counts or a rating. Needs deciding what's worth adding to
  `macrogrid-index.json` (author-supplied vs. computed by the release workflow) versus what stays editor-only presentation,
  since every new field is something plugin authors have to fill in and the host has to validate and cap. It needs a plan file first (`plans/`).
- **Plugins feeding the editor's Error List panel:** the docking workspace's Error List (`docs/design/docking-workspace.md`) is wired up but has
  no producer yet — it only ever shows "no problems". The status bar used to be where a plugin's own errors/warnings surfaced (`StatusEntry.level`
  `Warning`/`Error`); the Error List should take over that role instead, since it's a proper list with filtering and severity counts rather than a
  single status-bar item. Needs an SDK addition (a way for a plugin to report a diagnostic, not just a status-bar entry) — planned for whenever the
  SDK's next version bumps, not before.

## Release status

- The repositories are public and the first alpha is published: the server (`server-v0.2.0-alpha`, installer and zip), the phone app and the
  official plugins.
- The installer and the APK are **not code-signed**, by decision: no certificate will be bought, so Windows SmartScreen and Play Protect warn
  on first run.
- Private vulnerability reporting, the tag protection for `sdk-v*` and the documentation sites are on.

## Known gaps

- **Localization covers Turkish and English only.** The editor, the tray menu, native dialogs, plugin texts and the phone app follow the language (Windows display language by default, then the preference). Setting field labels of a plugin need an entry in the plugin's `locales/<language>.json` to be translated; texts built at run time (for example a per-item variable description) stay in the plugin's default language.
- **The browser deck** does not announce the `assets` and `layout.patch` capabilities yet, so it receives full layouts with icons inline.
- **The CSS editor** is a plain text box with sanitizer warnings, without syntax highlighting.
- **Windows only.** The server depends on Windows APIs.
- **The SoundBoard plugin is pinned to NAudio 2.2.1.** NAudio 3.0 changed `ISampleProvider.Read` from `(float[] buffer, int offset, int count)` to a single `Span<float>` parameter and marked `WasapiOut` obsolete, so `SoundBoard/src/SoundVoice.cs`'s custom `ISampleProvider` implementations no longer compile against it. Dependabot's bump to 3.1.0 was closed for this reason (checked 2026-09-28); upgrading needs `SoundVoice`/`LoopingSampleProvider` rewritten for the new interface (and likely a `WasapiOut` → `WasapiPlayer` move), not a version bump alone.

## Not planned

- **Automatic discovery of the server (mDNS).** Connect by entering the address or scanning the QR code in the editor's Pairing window.
- **Internet access or encryption.** The system is designed for a trusted local network (see the security model in [architecture.md](architecture.md#security-model)).
