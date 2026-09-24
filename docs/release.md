# Releasing the server

## Versions and tags

The three repositories are versioned independently (see [versioning.md](versioning.md)). A release is a Git tag on `main`; the tag name says what it
releases:

| Repository | Tag | Example |
|---|---|---|
| `macro-grid` (server, editor, installer) | `server-vX.Y.Z` | `server-v0.2.0` |
| `macro-grid-client` (Android app) | `client-vX.Y.Z` | `client-v0.1.0` |
| `macro-grid-plugin` (one tag per plugin) | `plugin-<id>-vX.Y.Z` | `plugin-obs-v0.2.0` |

While the project is in alpha, add a pre-release suffix to the tag and mark the GitHub Release as a pre-release: `server-v0.2.0-alpha`. The version
inside the program (`ClientHub.ServerVersion`) stays plain `0.2.0`; the suffix exists only in the tag and the release title.

## Release checklist (server)

1. On `dev`: decide the version bump with the maintainer and set `ClientHub.ServerVersion` (versioning.md). Never bump it silently.
2. Move the `[Unreleased]` entries of `docs/CHANGELOG-developer.md` and `docs/CHANGELOG.md` under the new version and date.
3. Write the release notes from the short `CHANGELOG.md` (a draft for the first one is in [release-notes-v0.2.0-alpha.md](release-notes-v0.2.0-alpha.md)).
4. Run `dotnet test`, and `npm run typecheck` in `editor/`, `webclient/` and `packages/renderer/`; the CI must be green on `dev`.
5. Build locally once with `scripts\publish.ps1` (below) and start `artifacts\server\MacroGrid.exe`; pair a device and press a button.
6. Merge `dev` into `main` (no squash; the maintainer does this, never automatically).
7. Tag `main` and push the tag: `git tag server-vX.Y.Z` then `git push origin server-vX.Y.Z`.
8. The `Release` workflow (`.github/workflows/release.yml`) runs the tests, `scripts/publish.ps1`, zips the folder and attaches it to a **draft**
   GitHub Release. The installer is built on the runner only when the workflow is started by hand (Actions, Release, Run workflow) with
   "installer" ticked; otherwise build it locally (section 2) and drag `MacroGrid-Setup-<version>.exe` onto the draft.
9. Test the installer on a clean PC (install, upgrade over the old version, uninstall), paste the release notes, and publish the draft.
10. Merge `main` back into `dev` if the release commit changed anything.

Client and plugin releases follow the same shape in their own repositories: bump, changelogs, merge to `main`, tag, draft release.

## 1. Build the release folder

The server ships as a Windows installer that wraps a self-contained single-file `MacroGrid.exe` (the .NET runtime and
ASP.NET are bundled). The target PC needs nothing installed except the WebView2 Runtime, which is part of current Windows.

## 1. Build the release folder

```powershell
scripts\publish.ps1
```

Builds the editor, then publishes `artifacts\server\` (`MacroGrid.exe`, about 62 MB, plus its `wwwroot` folder and
`version.txt`, `LICENSE`, `THIRD_PARTY_NOTICES.md` and the `licenses/` folder with the original license texts of all third-party libraries). The version is read from `ClientHub.ServerVersion`; bump it there first (see `versioning.md`).
`-SkipEditor` reuses an editor bundle that is already built.

## 2. Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) once, then:

```powershell
installer\build-installer.ps1
```

It produces `artifacts\MacroGrid-Setup-<version>.exe`. The installer (`installer\MacroGrid.iss`):

- installs to `Program Files\Macro Grid` with a Start menu entry, and an optional desktop shortcut;
- has an unchecked task "Start Macro Grid when I sign in to Windows" (a per-user `Run` entry that is removed on uninstall);
- opens TCP port 9820 in the Windows firewall for private networks only, and removes the rule on uninstall;
- closes a running server before an upgrade replaces its files;
- leaves `%AppData%\MacroGrid` (profiles, paired devices, plugins, logs) in place on uninstall.

`AppId` in the `.iss` file must never change; it is how upgrades and the uninstaller find the app.

The installer script has not been compiled on the development machine yet (Inno Setup was not installed there), so do a
first install / upgrade / uninstall test on a clean PC before publishing it.

## Not done yet

- The installer is not code-signed, so Windows SmartScreen will warn on first run. Signing needs a code-signing certificate.
- No update check inside the app; upgrading is running a newer installer over the old one.
