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
- **Pairing and devices:** PIN and QR pairing, per-device tokens, a device list with revoke, a profile per device.
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
- **An encrypted connection:** the phone and the server talk over plain `ws://` and `http://` on the local network, so anyone on the same Wi-Fi
  can read the traffic and take the pairing PIN or a device token, and then press the buttons of your profiles. Fine on a home network you trust,
  a real gap on a shared one (cafe, school, office). Wanted: the server makes its own certificate and puts its fingerprint into the pairing QR
  code, so the phone accepts only that certificate (no certificate authority needed). That covers listening in and impersonating the server. To
  settle in its plan: the browser deck (a self-signed certificate makes browsers warn, so it may keep a plain option, switched off by default), the
  phone app's setting that allows plain traffic, a token that never crosses the wire (a signed challenge instead), moving already paired devices
  over, and keeping older phone apps working while both connections exist (announced as a capability). It needs a plan file first (`plans/`).
- **Keyboard shortcuts and undo/redo in the editor:** today the editor has no shortcuts beyond Enter and Escape in windows and menus, no undo, and
  duplicating or copying a widget goes through the right-click menu. Wanted: **Delete**, **Ctrl+C / Ctrl+X / Ctrl+V** and **Ctrl+D** on the selected
  widgets, with paste working across pages and across profiles (a copy carries its actions and dynamic rules), and **Ctrl+Z / Ctrl+Y** to step back
  and forward through every change the person makes: moving or resizing a widget, adding, deleting or pasting, renaming, dynamization rules, action
  and event bindings, and any value in the properties panel (text fields, dropdowns, switches, modes, colors). All of them go on **one shared history
  stack**, in the order they happened, so Ctrl+Z always undoes the latest change whatever kind it was and Ctrl+Y redoes it. Shortcuts must not fire while a text field has focus, apart from the field's own text undo. They
  belong in the Edit menu with their key hints too. It needs a plan file before it is built (`plans/`).
- **A branded installer:** today the setup uses the plain modern wizard style with Inno Setup's default pictures and no icon of its own. Wanted: the
  Macro Grid logo and the product colors. What the setup tool can do natively: an icon for the setup file and the uninstaller (`SetupIconFile`, from
  `src/MacroGrid.Host/app.ico`), the large picture on the welcome and finished pages (`WizardImageFile`), the small logo in the corner of the
  other pages (`WizardSmallImageFile`, several sizes for high-DPI screens), the color behind the large picture, and the texts on each page. What it
  cannot do natively: restyle the buttons, fonts and page background in our accent color; that needs a third-party skin library, which is not
  worth its size, its licence and the antivirus false alarms it can bring. So the plan is the native part, drawn from `docs/ui/color-bible.md`
  and `website/public/logo.png`. The automatic update shows few pages (see `design/agreement-acceptance.md`), so the logo appears mostly
  in its progress window. The exact picture sizes are checked against the Inno Setup version the release workflow installs. It needs a small
  plan file first (`plans/`).
- **A much faster install and update (fewer files):** the editor bundle installs as about 6,000 tiny files (one per icon in `wwwroot\editor\assets`,
  because the editor loads each icon on demand), and copying them one by one made a setup on a fast PC take over a minute; an update shows this
  in its progress window, and antivirus scanning makes it slower on other PCs. An upgrade also leaves the previous version's hashed editor files behind (`wwwroot\editor\assets` collects several `index-*.js`), so the setup should clear the old `wwwroot` first (an `[InstallDelete]` entry) or, better, install far fewer files. Wanted: a setup of a few seconds. Ideas to weigh: bundle the
  icons into a few chunks or one file (the editor still loads only what it draws), or ship the editor as one archive that the app unpacks or
  serves from; the setup itself gets faster with fewer, larger files. Measure the file count and the setup time before and after. It needs a
  small plan file first (`plans/`).
- **The `plugin-html` widget:** a plugin ships its own HTML and JavaScript widget. It would run in a sandboxed iframe on the client and talk to the
  server only through `postMessage`. It needs the widget type in the renderers, a bridge in the client and a message route on the server. Today the
  `plugin-html` type draws a placeholder.
- **The `web` widget** (an embedded page such as a live chat): today it draws a placeholder. The idea is an iframe first and, for pages that refuse to be
  framed, a native WebView positioned over the grid cell by a small Android plugin.
- **An async host API for JavaScript plugins** (today scripts are synchronous, so `host.http` blocks the plugin's own thread).
- **A richer Discover tab, closer to a store page.** Today `editor/src/windows/PluginsWindow.tsx`'s Discover tab is a plain
  list: name, version, author, one line of description and an Install button (see `plans/plugin-distribution-plan.md`,
  section 7). The catalog (`macrogrid-index.json`, `PluginCatalogEntryInfo`) does not carry much more than that today either.
  Wanted: plugin cards with an icon (the logo/avatar field already planned above), and a detail view per plugin — a longer
  description or README, screenshots, category/tags to browse by, and (later) install counts or a rating if the official
  catalog ever tracks them. Needs deciding what's worth adding to `macrogrid-index.json` (author-supplied vs. computed by the
  release workflow) versus what stays editor-only presentation, since every new field is something plugin authors have to
  fill in and the host has to validate and cap. It needs a plan file first (`plans/`).
- **Plugins feeding the editor's Error List panel:** the docking workspace's Error List (`docs/plans/docking-workspace-plan.md`) is wired up but has
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
