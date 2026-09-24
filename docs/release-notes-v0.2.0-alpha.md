# Macro Grid server 0.2.0 (alpha)

Draft notes for the GitHub Release `server-v0.2.0-alpha`. Derived from [CHANGELOG.md](CHANGELOG.md); paste the text between the lines into the release.

---

**Alpha software, AI-generated, use at your own risk.** All code, documentation and artwork of Macro Grid were created by artificial intelligence. Nothing has been
reviewed line by line by a human or security-audited. It is provided "as is", without warranty of any kind, and the authors accept no responsibility or
liability for it: all risk is yours. The installer asks you to accept a user agreement that says so. It is meant for a home or office network you trust, not for
the internet. Expect rough edges and changes between versions. The installer is not code-signed, so Windows may warn about an unknown publisher.

Macro Grid turns a phone or tablet on your network into a customizable macro deck for your Windows PC. This is the server and editor; get the phone app
from [macro-grid-client](https://github.com/Deccoyi/macro-grid-client) and plugins from
[macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin).

## What is new

- **Windows installer:** the server now comes as a normal installer, with an option to start with Windows.
- **Automatic profile switching:** when an app comes to the front, your phone switches to that app's profile, and goes back when you leave it.
- **Profile lock and default profile:** a lock switch on the phone pauses automatic switching; a default profile can be chosen in Preferences.
- **JavaScript plugins:** write plugins in JavaScript with no build step. The editor shows what a plugin wants to do and it only runs after you allow it.
- **Plugins without a restart:** installing, reloading or removing a plugin takes effect right away, and plugins can be removed from the editor.
- **Plugin settings, icon packs and a categorized action picker:** ready-made settings forms, plugins that bring their own icons (the first pack is PLC
  icons), and a searchable action list. A bottom status bar shows the version, connected devices and plugin status.
- **Profile files:** export a profile as one `.msprofile` file and import it again; you are told which plugin is missing.
- **Dynamic text and icons:** buttons, toggles and labels can change their text or icon depending on a value.
- **Error alerts:** a failing button shows an alert on the phone and in the editor.

## Changed

- The editor can now only be used on the PC itself, so other devices cannot change your profiles or read your pairing code.
- Saving in the editor updates the phone faster: only what changed is sent, and icons are sent once and kept.
- The Help menu shows the real version; the editor's scrollbar is slimmer.

## Install

1. Download `MacroGrid-Setup-0.2.0.exe` below and run it (Windows 10 or 11). The installer is not code-signed yet, so Windows SmartScreen may warn you.
   Or download the zip, unpack it and run `MacroGrid.exe`.
2. Open the editor from the tray icon, open **Pairing**, and pair the phone app with the PIN or QR code.

Requirements: Windows 10 or 11 and the WebView2 Runtime (part of current Windows). The server opens port 9820 for private networks only.

## Known limitations

- The phone app's screens and the browser deck are Turkish only; the editor is available in Turkish and English.
- The installer is not code-signed and there is no update check inside the app; upgrade by running a newer installer over the old one.
- Traffic is not encrypted; see [SECURITY.md](../SECURITY.md).

---

Full technical history: [CHANGELOG-developer.md](CHANGELOG-developer.md).
