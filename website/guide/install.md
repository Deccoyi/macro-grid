# Install the server

The server is a Windows tray application. It stores your profiles, talks to the deck over a WebSocket on **port 9820**, runs the actions and hosts the editor.

## Requirements

- Windows 10 or 11.
- The [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), part of current Windows. The installer sets it up if it is missing.

## With the installer

1. Download `MacroGrid-Setup-<version>.exe` (or the zip) from [GitHub Releases](https://github.com/Deccoyi/macro-grid/releases).
2. Run it and accept the user agreement. A desktop shortcut is created by default.
3. Finish and start Macro Grid.

The installer puts the app in `Program Files\Macro Grid` and adds a firewall rule that allows **TCP 9820 on private and domain networks only**, never public ones. Uninstalling removes the rule.

::: info Unsigned installer
The installer is not code-signed yet, so Windows SmartScreen may warn on first run. Choose **More info**, then **Run anyway**, if you trust the download.
:::

## From source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) 20 or newer.

```powershell
cd editor;    npm install; npm run build; cd ..
cd webclient; npm install; npm run build; cd ..
Copy-Item editor\dist\*    src\MacroGrid.Host\wwwroot\editor -Recurse -Force
New-Item -ItemType Directory -Force src\MacroGrid.Host\wwwroot\deck | Out-Null
Copy-Item webclient\dist\* src\MacroGrid.Host\wwwroot\deck -Recurse -Force
dotnet run --project src/MacroGrid.Host
```

## The tray icon

Hover to see the server address. The menu shows the version, the addresses phones can use, how many devices are connected, and lets you open the editor, the data folder or quit. Double-clicking the icon opens the editor.

In [Preferences](/guide/preferences) you can start Macro Grid when you sign in to Windows, either in the tray only or with the editor window open. Starting in the tray means your phone can connect without you opening anything.

## Your data

Everything is stored in `%AppData%\MacroGrid\`. See [Files and ports](/reference/files).
