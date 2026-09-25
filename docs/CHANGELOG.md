# Changelog

New features and fixes in Macro Grid. For technical details, see [CHANGELOG-developer.md](CHANGELOG-developer.md).

## Unreleased

## 0.3.0 - 2026-09-25
This version has a new user agreement: you are asked to accept it once, in the installer or, if someone else installed it, the first time you start Macro Grid.

### New
- **Clearer conditions:** The variable list shows what kind of value each variable has (number, yes/no, text, time). For a yes/no value, such as "sound muted", you now pick On or Off instead of guessing what to type.
- **User agreement:** The agreement is shown again when its text changes. An update shows it in the installer only if it is new to you, and Macro Grid asks anyone else who uses the PC once at start.
- **Faster installer:** The editor is now a handful of files instead of about 1,500, so installing and updating take a fraction of the time.
- **Automatic updates:** Macro Grid now looks for a new version on its own, shortly after it starts and every few hours. When there is one, you get a notification and a window with what is new. Choose Install now, Later or Skip this version. You can also check by hand from the tray icon or the Help window, and switch the automatic check off in Preferences.

### Fixed
- **English labels in Turkish capitals:** Section titles such as "Version" no longer show a Turkish dotted capital I when the editor is set to English.
- **Error message in the status bar:** After a button press failed (for example a scene that does not exist), the message stayed at the bottom of the editor for good. It now disappears after 15 seconds, or as soon as the next action works.

## 0.2.1 - 2026-09-24
### New
- **Default language:** Macro Grid opens in Turkish on a Turkish Windows and in English on any other. Once you pick a language in Preferences, that choice is kept.
- **Plugin languages:** Plugins can ship their own translations. The tray menu, window titles and plugin texts now follow the language you choose.
- **Website:** A new website has a quick start, a full guide, tutorials and downloads for Macro Grid.
- **Start with Windows:** Preferences has a new General section. Turn on "Start Macro Grid when I sign in to Windows" and choose what happens at that start: only the tray icon (default) or also the editor window.
- **When you open Macro Grid:** Choose whether it opens the editor window (default) or only starts in the notification area.
- **Settings windows block the editor:** While Preferences, Plugins or another settings window is open, the editor behind it cannot be used, like in other desktop programs.
- **Help window:** It now shows the disclaimer, the user agreement and the license texts of all bundled libraries inside the app.
- **Installer:** Shows a user agreement that has to be accepted, creates a desktop shortcut by default, works on company networks, and removes everything when it is uninstalled, even while Macro Grid is running.
- **Phone app:** The settings panel shows the same notice: AI-generated, alpha, no warranty, use at your own risk.
- **Automatic profile switching:** When an app comes to the front, your phone switches to that app's profile. When you move to another window, it goes back to the previous profile.
- **Profile lock:** The profile drawer on your phone has a lock switch. While it is on, automatic switching pauses. You can still pick a profile by hand.
- **Default profile:** You can choose a default profile in Preferences.
- **Error alerts:** If a button fails, you now see an alert on your phone and in the editor's bottom bar.
- **Remove plugins:** Plugins can now be removed from the editor.
- **JavaScript plugins:** Plugins can now be written in JavaScript, with no build step. The editor shows what a plugin wants to do and it only runs after you allow it.
- **Installer:** The server now comes as a normal Windows installer, with an option to start with Windows.
- **Profile files:** Profiles are exported as a single `.msprofile` file and can be imported again. If the profile needs a plugin you do not have, you are told which one.
- **Dynamic text and icons:** Buttons, toggles and labels can now change their text or icon depending on a value, like the colors already can. Use the lightning-bolt button next to Text and Icon.
- **No restart for plugins:** Installing, reloading or removing a plugin takes effect right away. A new "Reload" button is in the Plugins window.

### Changed
- **Language:** Variable descriptions, action names, categories and window titles now follow the language you choose in Preferences.
- The installer now sets up the web component that the editor window needs, on PCs where it is missing.
- The **Remove** button in the Plugins window works again (its confirmation box was not shown, so nothing happened).
- The editor's screens can now only be used on the PC itself. This keeps other devices on your network from changing your profiles or reading your pairing code.
- Text typed into action fields that allow values (for example an OBS text source) now shows the current value instead of the raw `{name}`.
- The Help menu shows the real version.
- The editor's scrollbar is now slimmer and cleaner.
- Saving in the editor now updates your phone faster and smoother. Only what you changed is sent, and the rest of the deck stays as it is.
- Icons are sent to your phone once and kept there.

### Fixed
- A window closed by mistake when you dragged to select text and released the mouse outside it. This no longer happens.

## 0.2.0 - 2026-09-23
### New
- **Plugin settings:** Plugin settings are edited in the editor with a ready-made form.
- **Action picker with categories:** Actions are grouped and searchable.
- **Bottom status bar:** Shows the server version, the number of connected devices and plugin status.
- **Icon packs:** Plugins can bring their own icons. The first pack is PLC icons.

### Changed
- The OBS plugin's settings now use the same window as other plugins.

### Fixed
- The color picker was hidden behind another window.
- The comparison box in the logic window was too narrow.

## First releases
- Design buttons, sliders, gauges and web widgets on screen. The editor opens on your computer.
- Widgets show live data: time, processor, memory and volume.
- Colors and animations change with conditions. For example, a button blinks when the processor is busy.
- Pair your phone with a 6-digit PIN or a QR code. Each device can have its own profile.
- Change pages from the phone. Change profiles from the drawer.
- Works in a browser too (at the `/deck/` address).
- Added kiosk (full screen) mode and orientation lock.
- Added the plugin system and the first plugin: OBS control.
