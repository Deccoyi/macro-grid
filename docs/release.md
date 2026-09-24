# Releasing the server

The server ships as a Windows installer that wraps a self-contained single-file `MacroStation.exe` (the .NET runtime and
ASP.NET are bundled). The target PC needs nothing installed except the WebView2 Runtime, which is part of current Windows.

## 1. Build the release folder

```powershell
scripts\publish.ps1
```

Builds the editor, then publishes `artifacts\server\` (`MacroStation.exe`, about 62 MB, plus its `wwwroot` folder and
`version.txt`, `LICENSE`, `THIRD_PARTY_NOTICES.md` and the `licenses/` folder with the original license texts of all third-party libraries). The version is read from `ClientHub.ServerVersion`; bump it there first (see `versioning.md`).
`-SkipEditor` reuses an editor bundle that is already built.

## 2. Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) once, then:

```powershell
installer\build-installer.ps1
```

It produces `artifacts\MacroStation-Setup-<version>.exe`. The installer (`installer\MacroStation.iss`):

- installs to `Program Files\Macro Station` with a Start menu entry, and an optional desktop shortcut;
- has an unchecked task "Start Macro Station when I sign in to Windows" (a per-user `Run` entry that is removed on uninstall);
- opens TCP port 9820 in the Windows firewall for private networks only, and removes the rule on uninstall;
- closes a running server before an upgrade replaces its files;
- leaves `%AppData%\MacroStation` (profiles, paired devices, plugins, logs) in place on uninstall.

`AppId` in the `.iss` file must never change; it is how upgrades and the uninstaller find the app.

The installer script has not been compiled on the development machine yet (Inno Setup was not installed there), so do a
first install / upgrade / uninstall test on a clean PC before publishing it.

## Not done yet

- The installer is not code-signed, so Windows SmartScreen will warn on first run. Signing needs a code-signing certificate.
- No update check inside the app; upgrading is running a newer installer over the old one.
