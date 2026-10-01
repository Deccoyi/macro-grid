# Releasing Macro Grid

This is the one place that says how anything in Macro Grid is released: the server (with the plugin SDK), the phone app and the plugins.
Read it first for any release, version or signing work; the repository documents linked below only add their own detail, and they do not repeat this page.

| Component | Repository | Detail | Release guide |
|---|---|---|---|
| Server, editor, installer and the plugin SDK (source only) | `macro-grid` | this page | this page |
| Phone app (Android APK) | `macro-grid-client` | `docs/release.md` there (APK build, keystore) | this page, "Order" |
| Plugins (`obs`, `plc-icons`, `soundboard`, `hellojs`) | `macro-grid-plugin` | `docs/release.md` there (release script, index) | this page, "Order" |

## Versions and tags

The version rules are in [versioning.md](versioning.md): the server and the SDK share one number, the phone app and every plugin have their own,
and a plugin says in `plugin.json` which Macro Grid it needs (`minMacroGrid`). A release is a Git tag on `main`; the tag name says what it releases:

| What | Tag | Example | Version comes from |
|---|---|---|---|
| Server **and** SDK (`macro-grid`) | `server-vX.Y.Z` | `server-v1.0.0-beta` | `<Version>` in `Directory.Build.props` |
| Phone app (`macro-grid-client`) | `client-vX.Y.Z` | `client-v0.2.0` | `version` in `package.json` |
| One plugin (`macro-grid-plugin`) | `plugin-<id>-vX.Y.Z` | `plugin-plc-icons-v0.1.3` | `version` in the plugin's `plugin.json` |

Plugin ids: `obs`, `plc-icons`, `soundboard`, `hellojs`. There is no separate SDK tag, and the SDK is not published anywhere (it left NuGet after 1.2.0): the official plugins build against a checkout of this repository at the server tag.

While the project is in beta, the server tag ends in `-beta` and the GitHub Release is a pre-release: `server-v1.0.0-beta`. The version inside the
program stays plain `1.0.0`; the suffix exists only in the tag and the release title, and it is ignored when versions are compared
(before 1.0.0 the label was `-alpha`; those tags stay as history).

## Order of a release

Each step waits for the one before it, because a plugin can only be built against an SDK that has its server tag (the plugin repository checks that tag out), and a phone app is only useful with a server it works with.

1. **Server and SDK** (below): set `<Version>`, changelogs, merge to `main`, tag `server-vX.Y.Z`. The tag builds the installer (draft release).
   Done when the installer draft is tested and published.
2. **Plugins**: nothing to do after a MINOR or PATCH server release, they keep running (`minMacroGrid` says the *oldest* Macro Grid). A plugin needs a new release only when it
   has its own change, when it starts to use something from a newer MINOR (raise its `minMacroGrid` and `MacroGridSdkVersion`), or after a MAJOR (rebuild every plugin, set `minMacroGrid` to the new MAJOR).
3. **Phone app**: independent. Raise the Macro Grid version it needs only when it starts to depend on something new in the server.

Never publish a plugin that needs a Macro Grid version that is not released yet.

## Signing and keys

Nothing that signs is stored on GitHub. The keys stay on the maintainer's PC, outside every repository, and are backed up.

| What | How it is signed or protected | Where the key is | Who checks it | If the key is lost |
|---|---|---|---|---|
| **Plugin package** (the release zip) | ECDSA P-256 over the zip, made locally by `scripts/release-plugin.ps1` in `macro-grid-plugin` (`scripts/sign-package.cs`), stored as `<zip>.sig` and in the source index | `%USERPROFILE%\signing\plugin-signing\plugin-signing-private.pem` on the maintainer's PC | The server, with the public key in `PluginSigning.cs`: a valid signature is what makes a plugin **Official** in the editor | A new key, a server release that contains its public key, and every plugin signed again |
| **Official catalog files** (`index.signed.json`, `revoked.signed.json`) | ECDSA P-256 over the payload bytes with the same plugin key, made locally by `scripts/publish-catalog.ps1` in `macro-grid-plugin` (`scripts/sign-catalog.cs`); each file has a rising `sequence` | the plugin key above | The server, with the same public key: the signature is checked before the file is read, a lower sequence is refused | Same as the plugin key |
| **Phone app** (the APK) | Android release keystore, used by `scripts\build-release-apk.ps1` in `macro-grid-client`; the APK is uploaded to the draft release by hand | A keystore file outside the repository plus `android\keystore.properties` (git-ignored) | Android: an update installs only over an app signed with the same key. The script also stops when the certificate is not the release certificate | The update path closes for good: every user has to uninstall and reinstall |
| **Server installer** | Not code-signed (Windows SmartScreen warns on first run) | none | The in-app updater checks the SHA-256 that GitHub reports for the asset | none |

Never put a key, a keystore or a password in a repository, in a workflow or in chat. The plugin repository has no release workflow on purpose: a plugin release is built and signed
on the maintainer's PC, and `examples/third-party-release.yml` in that repository is a signing-free template for other authors. Details of the two local steps:
`macro-grid-plugin/docs/release.md` (plugin) and `macro-grid-client/docs/release.md` (APK, keystore setup and the certificate fingerprint).

## Release checklist (server and SDK)

1. On `dev`: decide the version bump with the maintainer and set `<Version>` in `Directory.Build.props` (versioning.md). Never bump it silently. This one number is the server and the SDK.
2. Move the `[Unreleased]` entries of `docs/CHANGELOG-developer.md` and `docs/CHANGELOG.md` under the new version and date. A fixed vulnerability goes under a `### Security` heading in both; a GitHub security advisory is optional (see `SECURITY.md`, which promises nothing).
3. Check that the new version's section in the short `CHANGELOG.md` reads well as the release notes: the workflow uses that section (`## X.Y.Z - date`) as the body of the GitHub Release, and the app's update window shows it to everyone who updates. Preview it with `scripts\release-notes.ps1 -Tag server-vX.Y.Z`.
4. Run `dotnet test`, and `npm run typecheck` in `editor/`, `webclient/` and `packages/renderer/`; the CI must be green on `dev`.
5. Build locally once with `scripts\publish.ps1` (below) and start `artifacts\server\MacroGrid.exe`; pair a device and press a button.
6. Merge `dev` into `main` (no squash; the maintainer does this, never automatically).
7. Tag `main` and push the tag: `git tag server-vX.Y.Z-beta` then `git push origin server-vX.Y.Z-beta` (the tag's version must equal `<Version>`; both workflows refuse it otherwise).
8. The tag starts two workflows:
   - `Release` (`.github/workflows/release.yml`) runs the tests, stops if a shipped dependency has a known vulnerability
     (`scripts/check-vulnerabilities.ps1`, the same check CI runs), runs `scripts/publish.ps1`, writes the SBOM
     (`scripts/sbom.ps1`, `MacroGrid-Server-<version>.cdx.json`, one file merged from the .NET, editor, deck and renderer parts, attached to the release), zips the folder, builds
     `MacroGrid-Setup-<version>.exe` with Inno Setup (on every tag; the updater downloads exactly this file name) and attaches both to a **draft**
     GitHub Release whose body is the changelog section from step 3. If the installer is missing from the draft, the app offers the release page
     instead of "Install now".
9. Test the installer on a clean PC (install, upgrade over the old version, uninstall), and test the update on a PC that has the previous
   version installed (see "Updating from inside the app" below). **Publishing the draft is what reaches people: every running install checks
   for updates about a minute after start and every 6 hours after that, so an untested installer must not be published.**
10. Merge `main` back into `dev` if the release commit changed anything.

Client and plugin releases follow the same shape in their own repositories: bump, changelogs, merge to `main`, tag, draft release (plugins: the release script publishes it).

## What ships

The server ships as a Windows installer that wraps a self-contained single-file `MacroGrid.exe` (the .NET runtime and
ASP.NET are bundled). The target PC needs nothing installed. The editor window uses the WebView2 Runtime, which is part of current Windows; the installer also bundles Microsoft's small WebView2 bootstrapper and runs it only when the runtime is missing (that needs an internet connection). `installer\build-installer.ps1` downloads the bootstrapper from Microsoft on the first build into `artifacts\redist`, checks that it is signed by Microsoft and never commits it.

## 1. Build the release folder

```powershell
scripts\publish.ps1
```

Builds the editor, then publishes `artifacts\server\` (`MacroGrid.exe`, about 62 MB, plus its `wwwroot` folder and
`version.txt`, `LICENSE`, `THIRD_PARTY_NOTICES.md`, `license-agreement.txt` and the `licenses/` folder with the original license texts of all third-party libraries). The version is read from `<Version>` in `Directory.Build.props`; bump it there first (see `versioning.md`).
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
- opens TCP port 9820 in the Windows firewall for private and domain networks (never public ones), replacing the old rule on an upgrade, and removes the rule on uninstall;
- always shows the agreement page in a setup run by hand (a first install or a downloaded setup); an automatic update (`/UPDATE`) shows it only to people who have not accepted this exact text: `build-installer.ps1` passes the SHA-256 of `license-agreement.txt` as `AgreementHash`, and the setup records it per person in `HKCU\Software\Macro Grid` (`AcceptedAgreement`; if another administrator approves the administrator prompt, the hash is theirs and the page shows again, which is the safe direction). **When the agreement changes, bump its "Last updated" date; one hash is one text, and even a typo fix asks everyone again, so batch wording changes.** If a release changes the agreement, add "This version has a new user agreement." to its changelog section (the old app cannot read the new text, so the release notes are the only warning);
- when the app started it with `/UPDATE` (an automatic update, a visible setup, not silent), skips every page except the agreement page (when needed) and the progress window, and starts the app again through Explorer, so it runs as the person who was signed in and not as administrator; the app then shows "Updated to X" once (it sees that the version it ran last is lower);
- closes a running Macro Grid before installing or uninstalling, and removes the whole program folder on uninstall;
- leaves `%AppData%\MacroGrid` (profiles, paired devices, plugins, logs) in place on uninstall.

`AppId` in the `.iss` file must never change; it is how upgrades and the uninstaller find the app.

Always do an install / upgrade / uninstall test on a clean PC before publishing an installer. The clean-PC checks are: the
editor window opens (the WebView2 bootstrapper ran if the runtime was missing), a phone pairs, "Start Macro Grid when I sign in"
works after a restart, running the new installer over the old one keeps profiles and paired devices, the entry in the installed
programs list shows the plain name "Macro Grid", and quitting from the tray icon ends the process in Task Manager. If the agreement text changed: the update setup shows the agreement page, and a second Windows user of the same PC is asked to accept it once when they start Macro Grid (Decline closes the app).

## Updating from inside the app

The app finds new releases by itself (design: [../design/auto-update.md](../design/auto-update.md)). What a release has to look like for that:

- The tag is `server-vX.Y.Z` or `server-vX.Y.Z-label`, the release is not a draft, and it has an asset named exactly `MacroGrid-Setup-X.Y.Z.exe` (the version without the label). GitHub reports its SHA-256 as the asset's `digest`, and the app refuses to install without it. A release missing the installer or the digest is announced with a button to the release page instead of "Install now".
- The body of the release is what the update window shows, for every release between the installed version and the newest one.
- Only an installed copy updates itself. The portable zip gets the release page.
- Try it without publishing: set the environment variable `MACROGRID_UPDATE_FEED_FILE` to a JSON file in the shape of the GitHub releases list, and the app reads that instead of GitHub (checks and the update window; the download still needs a real release).

## Not done yet

- The installer is not code-signed, so Windows SmartScreen will warn on first run. Signing needs a code-signing certificate. Updates are checked only against the SHA-256 that GitHub reports, which protects against a broken download, not against a compromised GitHub account.

## The plugin SDK is not published any more

Up to Macro Grid 1.2.0 `MacroGrid.Plugin.Abstractions` was published to nuget.org from CI. Since only the official plugins may be written in C#
(the server loads no other C# plugin), no third party needs the package, and the same code is public in this repository. Publishing stopped and
`publish-sdk.yml` is gone; the maintainer unlisted the existing versions on nuget.org on 2026-09-29 (package page > Manage > Listing). Unlisted versions disappear from search
but stay downloadable by their exact version, which the tests need: `LegacyPluginCompatibilityTests` builds against the real SDK 0.4.0, and package
validation compares against the 1.0.0 baseline. NuGet does not allow deleting a version.

What replaces it:

- The plugin repository builds against a checkout of this repository at the matching server tag (a project reference to
  `src/MacroGrid.Plugin.Abstractions`); its CI checks both repositories out, and `scripts/release-plugin.ps1` there refuses to build when the sibling checkout is not
  at the `MacroGridSdkVersion` it declares. To move the official plugins to a new SDK: tag and release the server first, set `MacroGridSdkVersion` in the plugin
  repository's `Directory.Build.props` to that version and raise `minMacroGrid` in each `plugin.json` that uses the new API.
- **Guardrail:** CI runs `dotnet pack` on the SDK (nothing is pushed), which runs .NET's package validation (`EnablePackageValidation`) against the published `1.0.0` baseline
  (the SDK csproj's `PackageValidationBaselineVersion`). A public member removed or changed incompatibly since 1.0.0 fails the build with an `error CP0...` — the
  "no breaking change within a MAJOR" rule from `versioning.md`, enforced by the build. Move the baseline only when 2.0.0 ships.
- The trusted-publishing policy and the `nuget` environment and `NUGET_USER` secret are no longer used; remove them when convenient.
