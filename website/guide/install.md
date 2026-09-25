# Install the server

The server is a Windows tray application. It stores your profiles, talks to the deck over a WebSocket on **port 9820**, runs the actions and hosts the editor.

## Requirements

- Windows 10 or 11.
- The [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), part of current Windows. The installer sets it up if it is missing.

## With the installer

1. Download `MacroGrid-Setup-<version>.exe` (or the zip) from the [Download page](/download).
2. Run it and accept the user agreement. A desktop shortcut is created by default.
3. Finish and start Macro Grid.

The installer puts the app in `Program Files\Macro Grid` and adds a firewall rule that allows **TCP 9820 on private and domain networks only**, never public ones. Uninstalling removes the rule.

::: info Unsigned installer
The installer is not code-signed yet, so Windows SmartScreen may warn on first run. Choose **More info**, then **Run anyway**, if you trust the download.
:::

## Updates

Macro Grid looks for a new version on its own, about a minute after it starts and every six hours after that. It only reads the public list of releases on GitHub and sends nothing about you or your PC. When there is a new version you get a notification and an update window that lists what changed in every version in between. Choose:

- **Install now:** downloads the installer, checks it against the SHA-256 that GitHub reports, asks Windows for administrator permission and updates in place. Macro Grid starts again by itself, and your profiles and paired devices stay.
- **Later:** asks again after a day.
- **Skip this version:** stays quiet about this version; a newer one is announced as usual.

If a new version has a **new user agreement**, the installer shows it and you have to accept it to go on. Cancelling changes nothing and the version you have keeps running. Anyone else who uses the same PC is asked to accept it once, the first time they start Macro Grid.

You can also check by hand from the tray menu or **Help > Check for Updates**. To stop the automatic check, switch it off in [Preferences](/guide/preferences). A copy from the zip file is not updated in place; the update window then opens the release page.

## The tray icon

Hover to see the server address. The menu shows the version, the addresses phones can use, how many devices are connected, and lets you open the editor, the data folder or quit. Double-clicking the icon opens the editor.

In [Preferences](/guide/preferences) you can start Macro Grid when you sign in to Windows, either in the tray only or with the editor window open. Starting in the tray means your phone can connect without you opening anything.

## Your data

Everything is stored in `%AppData%\MacroGrid\`. See [Files and ports](/reference/files).
