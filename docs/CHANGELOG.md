# Changelog

New features and fixes in Macro Grid. For technical details, see [CHANGELOG-developer.md](CHANGELOG-developer.md).

## Unreleased
### New
- **Safer pairing:** A new device can only be paired while the Pairing window is open. Each code pairs one device, and a device that enters too many wrong codes has to wait before it can try again.
- **Security log:** Pairings, removed devices and plugin installs and permissions are now written to the log files. Log files are kept for 14 days and no longer pile up on the PC.
- **Component lists:** Every release now comes with a list of the components it contains, and a release is only built when none of them has a known security problem.
- **Clearer plugin warnings:** When a plugin asks to send web requests, the permission now says whether that is this computer, your local network or the internet. The warning for other authors' plugins now says they can connect to the internet and send data.

## 1.0.1 - 2026-09-26
### Fixed
- **Start-up log:** Macro Grid now writes into its log what it finds in its data folder and what it loads from it, and whether it can write there. If something is missing after a start, the log says why.

## 1.0.0 - 2026-09-26
### Changed
- **One version number:** Macro Grid and the tools plugins are built with now share one version number, so it is easy to tell which plugins fit which version. Plugins that work today keep working.
- **Clearer plugin messages:** When a plugin does not fit, the Plugins window now says which Macro Grid version it needs.

## 0.3.2 - 2026-09-25
### New
- **Discover plugins:** The Plugins window's Discover tab can now browse and install plugins from the official catalog, with compatibility, install and update status shown for each one.
- **Third-party plugin sources:** Discover can add another author's plugin repository as a source and install from it, with a clear warning before installing anything that isn't from the official source.
- **Install from a link:** Discover can also install a single plugin straight from a pasted repository link, with the same compatibility check and third-party warning first.
- **Plugin icons:** A plugin can now show its own icon next to its name in the Plugins window.
- **Richer plugin settings:** Plugins can offer file pickers, lists of items, buttons and notices on their settings page, and can react when a button is released.
- **Where a plugin came from:** Installed Plugins now shows an Official / Third-party / Local badge for each plugin, and flags one with an update available once Discover has been opened.

## 0.3.1 - 2026-09-25
This version has a new user agreement: you are asked to accept it once, in the installer or, if someone else installed it, the first time you start Macro Grid.

It replaces 0.3.0, which was withdrawn. Everything from 0.3.0 is in it: automatic updates, the agreement asked again whenever its text changes, a much faster installer and clearer conditions for yes/no values.

### Fixed
- **After an update:** Macro Grid now starts again as you, not as administrator.
- **Install now:** It no longer fails when an older download was left over on the PC.

## 0.3.0 - 2026-09-25 (withdrawn, replaced by 0.3.1)
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
