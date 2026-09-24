# Changelog

New features and fixes in Macro Station. For technical details, see [CHANGELOG-developer.md](CHANGELOG-developer.md).

## Unreleased
### New
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
