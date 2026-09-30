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
  Windows, upgrade, uninstall; [release.md](guides/release.md)); a signed release build of the phone app. (The plugin SDK was published on NuGet up to 1.2.0 and is not any more; see [guides/release.md](guides/release.md).)
- **Phone app** (its own repository): connection with saved servers and QR pairing, the profile drawer, page swipes, kiosk mode and orientation lock,
  keep-awake, automatic reconnection, an offline layout cache, and auto-update (checks GitHub, downloads and verifies a release-signed APK, hands it
  to Android's installer).
- **Plugin distribution:** a Discover tab in the editor's Plugins window — browse the official catalog, add a third-party multi-plugin source, install
  a single plugin from a pasted repository link, badges (Official / Third-party / Local) and "update available"
  ([design/plugin-distribution.md](design/plugin-distribution.md)).
- **Plugin SDK 0.4.0:** file, list, button and notice setting fields, an action that learns when its button is released, a settings page that runs a
  host command (used for a sound preview), and an optional plugin icon; first used by the official **SoundBoard** plugin
  ([design/sdk-0.4.0-and-soundboard.md](design/sdk-0.4.0-and-soundboard.md)).
- **Custom plugin widgets:** a plugin ships its own widget (server, SDK, shared renderer, browser deck, phone app and editor preview all built) that
  runs in a sandboxed Web Worker with no network access, so a slow, looping or broken widget cannot freeze the deck; a per-device live-widget
  recommendation (not a hard limit), a crash guard, a `storage` option, per-widget option switches, plugin SVG icons, a `Color` setting kind, a
  Toolbox search and grouping menu, and Diagnostic Messages in the editor. Example plugins Hello Gauge and Hello Weather
  ([design/plugin-widgets.md](design/plugin-widgets.md)).
- **The `web` widget:** an embedded page (a live chat, an alerts panel) in an iframe with popups, downloads and navigation blocked, a "Change web page" button
  action, and on the phone a crash guard, a recommended number of live pages and a "Keep loaded" option. Still marked experimental in the editor.
- **Security hardening:** an encrypted connection (TLS, see "Done" above), device tokens and plugin secrets encrypted at rest, pairing rate limits and
  a security log, a security event log, dependency vulnerability scanning and an SBOM in CI, and only official signed C# plugins load
  ([design/security-hardening.md](design/security-hardening.md)).

## Next

The order of the bigger pieces of work, and their plans, are in [plans/README.md](plans/README.md). The items below have no plan file yet.

- **The "Allow unencrypted connections" preference:** now that the server and the phone app both support `wss://` (see "Done"
  above), plain `ws://`/`http://` (port 9820) stay open unconditionally; a preference to turn them off is not built yet. Default
  on for now (the browser deck cannot use `wss://` at all, and older paired phones have no TLS support), default off starting
  the next MAJOR version once both are settled. See [design/security-hardening.md](design/security-hardening.md), part A.
- **A live-stream chat plugin** (one plugin per streaming platform, sharing one chat view): a custom widget that shows the channel's live chat, with
  a per-chatter menu (ban, or one of a few preset timeouts), plus actions for ad breaks, switching the stream category between saved favorites, and
  chat modes where the platform's API offers them. Needs deciding first, per platform: how chat is received without a server of our own (a platform
  that only delivers chat events to a public web address cannot be reached by an offline app, so it needs either a small relay we run or an
  unofficial route; a platform with a WebSocket event stream needs neither), and how the account is linked without shipping a secret in the plugin
  (a device-code sign-in with no client secret is the preferred shape; a platform that requires a secret for every token exchange needs each
  person to register their own app, or a relay that holds the secret). A plugin that holds a WebSocket needs C#, because a JavaScript plugin has
  no WebSocket and cannot listen for incoming connections; the widget itself only draws on a canvas and gets its messages from the plugin. Emote
  images are a separate problem: a widget has no network, so only bundled assets can be drawn. The actions alone (no live chat) need no server on
  any platform and can ship first. It needs a plan file first (`plans/`).
- **The rest of a store-like Discover tab.** Discover now shows a card grid and a per-plugin detail view (description, author,
  homepage, declared permissions, install/update) built from what the catalog already carries, in a wider Plugins window. Still
  open, and each needs a catalog field first: a real plugin icon (cards show the plugin's initial on a stable color for now),
  screenshots, category/tags to browse by, and (later) install counts or a rating. Needs deciding what's worth adding to
  `macrogrid-index.json` (author-supplied vs. computed by the release workflow) versus what stays editor-only presentation,
  since every new field is something plugin authors have to fill in and the host has to validate and cap. It needs a plan file first (`plans/`).
- **A crash report dialog:** when the server crashes, show the person a native confirmation window (WinForms, not a browser tab or the editor) with what would be sent — the log files and basic diagnostic info — and a choice to send it or not. On "send", the app itself emails the report to `macrogrid.app@gmail.com`; it must not open the person's own mail client or send them to a webmail site. Needs deciding how a crashed process reliably shows this window and mails from it (a small always-present watchdog process, or a next-start check for a previous crash marker), what "basic diagnostic info" contains, and the sending mechanism (SMTP with a project-owned account vs. a small backend). It needs a plan file first (`plans/`).
- **A phone-app style permission system for every plugin that is not C#** (JavaScript plugins today, and plugin widgets once they
  exist): C# plugins are left out because only official, signed ones load. Today a JavaScript plugin declares its permissions and the person
  approves the whole set once in the Plugins window; an update that asks for more waits again
  ([design/js-plugin-runtime.md](design/js-plugin-runtime.md#approval)). Still missing, compared with phone app permissions: the approval
  shown as a step of installing (and of updating) the plugin, not only afterwards in the Plugins window; each permission explained in
  plain words with what it allows; switching a single permission off later, per plugin, without uninstalling, with the plugin kept running
  without it (the API answers "not allowed"); a clear list of what every installed plugin may do; and the same model for the abilities a plugin
  widget declares (keep loaded, data kept on the device). It needs a plan file first (`plans/`).
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
