# Changelog

New features and fixes in Macro Grid. For technical details, see [CHANGELOG-developer.md](CHANGELOG-developer.md).

## Unreleased
### New
- **Plugins can add their own widgets:** a plugin can draw a widget of its own, such as a gauge, and it appears in the Toolbox under the plugin's name. Plugin widgets run in a closed box with no network, so a broken one cannot harm the rest of the deck. If one keeps crashing the app, it is switched off and you can turn it back on.
### Changed
- **The Error List is now "Diagnostic Messages":** it shows errors, warnings and information. After every save it tells you how many plugin widgets the profile has and whether a page has more than is recommended (the right number depends on the device), and "No free cell left on this page" is listed there as a warning too.
- **Hints can be closed:** the hint boxes under a web widget's address now have a close button and stay closed. "Show closed notices again" in Preferences brings them back. A warning about the address you typed (not encrypted, not allowed) cannot be closed.
- **The Toolbox marks the web widget with a small flask icon** instead of the word "Experimental".

## 1.3.1 - 2026-09-29
### New
- **Plugin updates are shown:** when installed plugins have an update, the status bar says how many need one and the Plugins menu shows a dot. Click the status bar entry to open the Plugins window.

- **The Error List shows why a plugin does not work:** a refused key press (and the reason), a plugin that did not load, was switched off or failed now appear there, with a Source column. The same message is one line with a count (x5), not five lines. "Clear" empties the list.

### Changed
- **Web widget is marked experimental:** the Toolbox tile and the widget's properties say that a heavy page can slow down or freeze the deck on the phone, so use few web widgets and light pages.

### Fixed
- **Properties panel follows what you click:** after opening a plugin in the Plugins panel, clicking a button on the page now shows the button's properties right away, without going through the Hierarchy first.
- **Collapsing a section:** a section in the properties panel no longer opens again on its own the first time you collapse it, or closes again the first time you open it.
- **Deck in a phone browser:** the deck opened over plain http on your network works again (it stopped at start on some phones).

## 1.3.0 - 2026-09-29
### New
- **The setup wizard now looks like Macro Grid:** the Macro Grid logo and colors on the welcome and finished pages, a small logo in the corner
  of the other pages, and the Macro Grid icon on the setup and uninstall files.
- **Undo, redo and the usual clipboard shortcuts in the editor:** Ctrl+Z/Ctrl+Y, Ctrl+X/C/V, Ctrl+D and Delete now work everywhere in
  the editor, with an Edit menu, right-click menu entries and toolbar buttons for the same commands.
- **A new Plugins panel:** installed plugins now have their own panel in the editor, next to Hierarchy. A plugin can list its own items there (for example a sound board's sounds), and selecting one shows its settings on the right, the same way a page or a profile does.
- **A plugin's own settings window also supports Ctrl+Z/Ctrl+Y**, and a long list inside it (a sound board's sounds, for example) now
  collapses each row to its name, with "Expand all"/"Collapse all" buttons, so it takes far less room.
- **File menu gets a Save entry**, next to the existing Save button and its Ctrl+S shortcut.
- **A plugin that asks for permissions shows them before it is installed,** not after, and starts right away once you agree.
- **A store-like Discover tab:** search, filters, and plugin cards with a Get button. Clicking a card opens a page with its
  description, author, version, homepage and what it asks for. The Plugins window is bigger to fit.
- **A refresh button next to "Pick from running apps":** a program opened after the profile was selected now shows up.
- **The web widget shows real pages:** put the link of a live chat or an alerts panel in a cell and the phone shows it. Pages cannot open
  windows, download files or use the phone's camera or location, and a warning reminds you to use only sites you trust.
- **A "Change web page" button action:** give a web widget a name, then a button can show another site in it, go back to the first one or reload it, on
  the phone that pressed the button.
- **Sites in an imported profile are listed first:** when a profile you import opens web pages, you see which sites and choose whether to keep them.
- **Safer connections:** the editor's address and the phone connection now refuse requests that come from a web page pretending to be this computer.
### Changed
- **Only official plugins may use C#:** a C# plugin runs only when it is officially signed, and this is checked every time it loads. Plugins from other authors must be JavaScript. A C# plugin that is not allowed stays in the list with the reason and can be removed.
- **Plugins that type on your PC are limited to button presses:** a plugin you allowed to press keys can do so only while you press one of its buttons, and only a little (at most 200 characters and 10 key combinations per press). It cannot type into a terminal, a system tool or Macro Grid itself, cannot use the Windows key, and is switched off if it tries to type a harmful command. The Plugins window shows how often it used the keyboard today.

### Fixed
- **Auto-switch settings surviving a re-pair:** If a device had to pair again (a lost token, or one that could not be decrypted after 1.2.0's encrypted pairings), "Follow active window" and its assigned profile were silently turned back off. They now carry over.

## 1.2.1 - 2026-09-28
### Fixed
- **Auto-switch settings surviving a re-pair:** If a device had to pair again (a lost token, or one that could not be decrypted after 1.2.0's encrypted pairings), "Follow active window" and its assigned profile were silently turned back off. They now carry over.
### Security
- **Hardened logging and the browser deck's icon handling:** Closed findings from an automated code scan around what gets written to the log files and which icon links the browser deck will load. No known impact on anyone using Macro Grid.

## 1.2.0 - 2026-09-28
### New
- **A new editor layout:** The editor is now a workspace you can arrange. Panels dock, tab, split and auto-hide, and pages open as tabs.
- **One tree for profiles and pages:** The Hierarchy panel shows every profile and its pages in one tree. Open a profile to load its pages.
- **Folders:** Group pages and profiles into folders, and drag them in and out to reorder.
- **Copy and paste:** Copy pages, profiles or whole folders with Ctrl+C and Ctrl+V, including into another profile.
- **Safer pairing:** A new device can only be paired while the Pairing window is open. Each code pairs one device, and a device that enters too many wrong codes has to wait before it can try again.
- **Encrypted pairings:** The tokens of paired devices are now stored encrypted for your Windows account. Copying the data folder to another PC or account means pairing those devices again.
- **Security log:** Pairings, removed devices and plugin installs and permissions are now written to the log files. Log files are kept for 14 days and no longer pile up on the PC.
- **Component lists:** Every release now comes with a list of the components it contains, and a release is only built when none of them has a known security problem.
- **Clearer plugin warnings:** When a plugin asks to send web requests, the permission now says whether that is this computer, your local network or the internet. The warning for other authors' plugins now says they can connect to the internet and send data.
- **Blocked other web pages from reaching Macro Grid:** A web page open in your regular browser could otherwise poke at the editor's internal connection. It cannot anymore.
- **Plugin passwords stay hidden:** Reopening a plugin's settings (for example OBS's) no longer shows its saved password. Leave the field empty to keep it, or type a new one to change it.
- **The browser deck tells you when a PIN is wrong:** Entering the wrong pairing PIN, or too many of them, used to look like nothing happened. It now shows why, with a live countdown when you have to wait before trying again.
- **Choose to delete your data on uninstall:** Removing Macro Grid now asks whether to also delete your profiles, paired devices, plugins and logs. Say no to keep them for next time.

## 1.1.0 - 2026-09-28
No user-visible changes in this version — an addition to the plugin SDK only (see CHANGELOG-developer.md).

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
