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

## What ships

The server ships as a Windows installer that wraps a self-contained single-file `MacroGrid.exe` (the .NET runtime and
ASP.NET are bundled). The target PC needs nothing installed. The editor window uses the WebView2 Runtime, which is part of current Windows; the installer also bundles Microsoft's small WebView2 bootstrapper and runs it only when the runtime is missing (that needs an internet connection). `installer\build-installer.ps1` downloads the bootstrapper from Microsoft on the first build into `artifacts\redist`, checks that it is signed by Microsoft and never commits it.

## 1. Build the release folder

```powershell
scripts\publish.ps1
```

Builds the editor, then publishes `artifacts\server\` (`MacroGrid.exe`, about 62 MB, plus its `wwwroot` folder and
`version.txt`, `LICENSE`, `THIRD_PARTY_NOTICES.md`, `license-agreement.txt` and the `licenses/` folder with the original license texts of all third-party libraries). The version is read from `ClientHub.ServerVersion`; bump it there first (see `versioning.md`).
`-SkipEditor` reuses an editor bundle that is already built.

## 2. Build the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) once, then:

```powershell
installer\build-installer.ps1
```

It produces `artifacts\MacroGrid-Setup-<version>.exe`. The installer (`installer\MacroGrid.iss`):

- first asks the person to accept the user agreement (`installer\license-agreement.txt`: no warranty, limitation of liability, the MIT license); the same text is shown in the editor's Help window;
- installs to `Program Files\Macro Grid` with a Start menu entry and a desktop shortcut (the task is ticked by default);
- has an unchecked task "Start Macro Grid when I sign in to Windows" (a per-user `Run` entry that carries `--autostart`, removed on uninstall). The same switch is in the editor under Preferences, General, together with what a start by Windows and a start by the person do (tray only or open the editor window);
- opens TCP port 9820 in the Windows firewall for private and domain networks (never public ones), and removes the rule on uninstall;
- closes a running Macro Grid before installing or uninstalling, and removes the whole program folder on uninstall;
- leaves `%AppData%\MacroGrid` (profiles, paired devices, plugins, logs) in place on uninstall.

`AppId` in the `.iss` file must never change; it is how upgrades and the uninstaller find the app.

Always do an install / upgrade / uninstall test on a clean PC before publishing an installer. The clean-PC checks are: the
editor window opens (the WebView2 bootstrapper ran if the runtime was missing), a phone pairs, "Start Macro Grid when I sign in"
works after a restart, running the new installer over the old one keeps profiles and paired devices, the entry in the installed
programs list shows the plain name "Macro Grid", and quitting from the tray icon ends the process in Task Manager.

## Not done yet

- The installer is not code-signed, so Windows SmartScreen will warn on first run. Signing needs a code-signing certificate.
- No update check inside the app; upgrading is running a newer installer over the old one.

## Publishing the plugin SDK to NuGet

`MacroGrid.Plugin.Abstractions` is published from CI with NuGet Trusted Publishing, so no API key is stored in GitHub or on
any machine. The workflow is `.github/workflows/publish-sdk.yml` (its file name is part of the policy on nuget.org: do not
rename it) and it runs when a tag `sdk-vX.Y.Z` is pushed.

One-time setup:

1. nuget.org, account menu > Trusted Publishing: add a policy with owner `Deccoyi`, repository `macro-grid`, workflow file
   `publish-sdk.yml` and environment `nuget`. While the repository is private the policy is only temporarily active and
   becomes permanent after the first successful push.
2. GitHub, repository settings: create the environment `nuget` and add the secret `NUGET_USER` (your nuget.org user name,
   not the e-mail address).

### Releasing a new SDK version

A published version can never be replaced or deleted on nuget.org, only unlisted. Check everything before the tag.

1. **Pick the version.** The server loads a plugin only if its SDK satisfies the plugin's `sdkVersion` as a caret range
   (`SemVer.SatisfiesCaret`). While the SDK is `0.x`, a minor bump (`0.3.x` to `0.4.0`) makes every existing plugin
   incompatible, so use a patch bump (`0.3.1`) for additive, non-breaking changes and a minor bump only for breaking ones.
   Use a pre-release suffix (`0.4.0-preview.1`) for anything not final; the tag then is `sdk-v0.4.0-preview.1`.
2. **Set the version in two places, identically:** `<Version>` in
   `src/MacroGrid.Plugin.Abstractions/MacroGrid.Plugin.Abstractions.csproj` and `PluginSdk.Version` in
   `src/MacroGrid.Plugin.Abstractions/PluginSdk.cs`.
3. **Update the changelogs** (`docs/CHANGELOG-developer.md`, and `docs/CHANGELOG.md` if users notice the change) and the SDK
   `README.md` if the reference snippet shows the version.
4. **Commit and push to `dev`** and wait for CI to be green. The tag must point at a commit that is already on `dev` or
   `main`; the workflow refuses anything else.
5. **Tag and push** (only the owner does this; there is no API key to hand out):
   `git tag sdk-vX.Y.Z` and `git push origin sdk-vX.Y.Z`.
6. **Watch** Actions > "Publish SDK". It checks that the tag and both versions match, packs, logs in to nuget.org (short-lived
   key) and pushes the `.nupkg` and the `.snupkg` symbols. `--skip-duplicate` makes a re-run of the same version harmless.
7. **Verify** after 5 to 30 minutes: https://www.nuget.org/packages/MacroGrid.Plugin.Abstractions shows the new version, or
   `https://api.nuget.org/v3-flatcontainer/macrogrid.plugin.abstractions/index.json` lists it.
8. **Move the plugins over:** in the plugin repository set `MacroGridSdkVersion` in `Directory.Build.props` to the new
   version, and raise `sdkVersion` in each `plugin.json` that uses the new API. Build and run the plugin CI.

If the publish fails:

- Version check fails: the tag, `<Version>` and `PluginSdk.Version` differ. Fix the files; delete the tag locally and on
  GitHub (`git tag -d sdk-vX.Y.Z`, `git push origin :refs/tags/sdk-vX.Y.Z`) and tag again. Nothing was published.
- Branch check fails: the tagged commit is not on `dev` or `main`. Push the commit first, then re-tag as above.
- Login fails: the trusted-publishing policy on nuget.org does not match (owner `Deccoyi`, repository `macro-grid`, workflow
  `publish-sdk.yml`, environment `nuget`), the policy is inactive, or the `NUGET_USER` secret is missing or is an e-mail
  address.
- Push rejected with 409 or "already exists": that version is on nuget.org already. Bump the version; it cannot be reused.
- A broken version got out: unlist it on nuget.org (package page > Manage > Listing) and publish a fixed patch version.

