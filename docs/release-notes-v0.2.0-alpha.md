# Macro Grid server 0.2.0 (alpha)

Notes as published on the GitHub Release `server-v0.2.0-alpha`. Derived from [CHANGELOG.md](CHANGELOG.md); the text between the lines is the release body.

---

**Alpha software, AI-generated, use at your own risk.** All code, documentation and artwork of Macro Grid were created by artificial intelligence. Nothing has been
reviewed line by line by a human or security-audited. It is provided "as is", without warranty of any kind, and the authors accept no responsibility or
liability for it: all risk is yours. The installer asks you to accept a user agreement that says so. It is meant for a home or office network you trust, not for
the internet. Expect rough edges and changes between versions. The installer is not code-signed, so Windows may warn about an unknown publisher.

Macro Grid turns a phone or tablet on your network into a customizable macro deck for your Windows PC. This is the server and editor; get the phone app
from [macro-grid-client](https://github.com/Deccoyi/macro-grid-client) and plugins from [macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin)
(guide: https://deccoyi.github.io/macro-grid-plugin/).

## What is new

- **Windows installer:** a normal installer with a user agreement, a desktop shortcut and an option to start with Windows. It also sets up the web component the editor window needs if your PC lacks it (that needs an internet connection).
- **Start with Windows:** Preferences has a General section: choose whether Macro Grid starts with Windows, and whether a start opens the editor window or only the tray icon.
- **Settings windows block the editor** while they are open, like in other desktop programs.
- **Help window:** shows the disclaimer, the user agreement and the license texts of all bundled libraries inside the app.
- **Automatic profile switching:** when an app comes to the front, your phone switches to that app's profile, and goes back when you leave it.
- **Profile lock and default profile:** a lock switch on the phone pauses automatic switching; a default profile can be chosen in Preferences.
- **JavaScript plugins:** write plugins in JavaScript with no build step. The editor shows what a plugin wants to do and it only runs after you allow it.
- **Plugins without a restart:** installing, reloading or removing a plugin takes effect right away.
- **Plugin settings, icon packs and a categorized action picker,** a status bar with version, connected devices and plugin status.
- **Profile files:** export a profile as one `.msprofile` file and import it again.
- **Dynamic text and icons:** buttons, toggles and labels can change their text or icon depending on a value.
- **Error alerts:** a failing button shows an alert on the phone and in the editor.

## Install

1. Download `MacroGrid-Setup-0.2.0.exe` below, run it and accept the user agreement (Windows 10 or 11). Windows SmartScreen may warn because the installer is not code-signed.
   Or download the zip, unpack it and run `MacroGrid.exe`.
2. Open the editor from the tray icon, open **Pairing**, and pair the phone app with the PIN or QR code.

The server opens port 9820 for private and company networks only, never public ones.

## Known limitations

- The phone app's screens and the browser deck are Turkish only; the editor is available in Turkish and English.
- The installer is not code-signed and there is no update check inside the app; upgrade by running a newer installer over the old one.
- Traffic is not encrypted; see [SECURITY.md](https://github.com/Deccoyi/macro-grid/blob/main/SECURITY.md). Use it only on a network you trust.

Full technical history: [CHANGELOG-developer.md](https://github.com/Deccoyi/macro-grid/blob/main/docs/CHANGELOG-developer.md).

## Checksums (SHA-256)

- `MacroGrid-Setup-0.2.0.exe`: `3e1b469fa26f28b7219e680916c62ad76a53c0f2f079a4ebe47e94569ebaa467`


---
