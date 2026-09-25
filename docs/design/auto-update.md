# Auto-update (server)

Status: **built and shipped in 0.3.0** (2026-09-25). Verified by hand on an installed copy with a made-up newer release (update window, notes, Later, Skip, Install now: download, verification, administrator prompt, the visible setup, cancelling it, the setup that skips an accepted agreement, coming back as the person). Still to verify: see "Still to verify". Repository: `macro-grid` only; the phone app and the plugins are not touched.

The server finds out on its own that a newer release exists on GitHub, tells the person (a Windows notification and an update window with the
release notes), and after they agree downloads the installer, verifies it and upgrades itself.

## Behavior

| When | What happens |
|---|---|
| Start-up | A check runs about 60 seconds after start (never blocks start-up or the editor). |
| While running | Another check every 6 hours. |
| Update found | A Windows notification from the tray icon: "Macro Grid 0.3.0 is available"; clicking it opens the update window. The editor's status bar shows an "Update available" item that opens the same window. The notification appears once per version. |
| Update window | Current and new version, the release notes of **every** version between them (newest first), and **Install now**, **Later**, **Skip this version**, plus a link to the release page and "Check for updates". Later and Skip record the answer and close the window (`window.close()`, which the host turns into closing the native window). |
| Later | No notification for 24 hours, then it is announced again. The status-bar item stays. |
| Skip this version | No notification for exactly that version; a newer version notifies as usual. The status-bar item stays. |
| Manual check | Tray menu "Check for updates", the editor's Help menu ("Check for Updates...", which opens the update window and looks at once) and Help > About. Ignores the snooze and the skipped version. Says "You are up to date" when there is nothing, and reports a failure. |
| Install now | Progress bar while downloading, then Windows asks for administrator permission (UAC) and a visible setup opens with a progress window (not silent). The setup shows the user agreement only when its text changed since the person accepted it. When it starts replacing files it closes the app, and Macro Grid starts again and says "Updated to X" once. Cancel in the setup leaves everything as it was: the update window says the update was not installed and offers "Later" as usual. |
| Turned off | Preferences > General > "Check for updates automatically" (on by default) and "Include pre-releases" (on by default while every release is an alpha). Off stops the automatic checks and notifications; the manual check still works. |

Offline, GitHub unreachable or rate-limited: nothing is shown; it is logged and retried at the next interval (a manual check does say so).

## Where updates come from

- `GET https://api.github.com/repos/Deccoyi/macro-grid/releases?per_page=50` (not `/releases/latest`, which leaves out pre-releases).
- Only releases whose tag starts with `server-v` and that are not drafts. Pre-releases (a version label such as `-alpha`, or GitHub's own pre-release flag) count only while "Include pre-releases" is on.
- The version is parsed from the tag (`server-v0.3.0-alpha` is `0.3.0` with the label `alpha`) and compared as SemVer with `ClientHub.ServerVersion` (`0.2.1 < 0.3.0-alpha < 0.3.0`). Only strictly newer versions count; never a downgrade.
- The installer asset is `MacroGrid-Setup-<version without label>.exe` (`server-v0.2.1-alpha` ships `MacroGrid-Setup-0.2.1.exe`). "Install now" also needs the asset's `digest`. A release without either, and a portable (zip) copy, get a button to the release page instead (`ReleaseInfo.CanInstall`, `UpdateInstaller.IsInstalledByInstaller`).
- The response `ETag` is stored and sent back as `If-None-Match`; an unchanged list is a `304` and does not count against GitHub's anonymous limit. A `User-Agent: MacroGrid/<version>` header is the only thing sent about the app; nothing about the person or the PC.

## Download and install

1. Only `https` addresses on `github.com`, `objects.githubusercontent.com` and `release-assets.githubusercontent.com`. The download client does not follow redirects itself; `InstallerDownloader` follows up to 5 and checks every hop against the same list.
2. The file goes to `%LocalAppData%\MacroGrid\updates\<version>\MacroGrid-Setup-<X.Y.Z>.exe.part` and is renamed when complete. Its size must equal the asset's size and its SHA-256 the asset's `digest`, otherwise the file is deleted and the person sees an error. A verified file from an earlier attempt is reused. Downloads never pile up (`DownloadedInstallers`): at most one installer stays, the newest one that is newer than the running version; everything else the updater put in that folder (older offers, installed versions, `.part` files) is deleted at every start, after a download that replaces an older one, and once the running version has caught up the whole folder goes. Only sub-folders named like a version are touched, never a file saved elsewhere; a file that is still locked (the setup that is just finishing) is logged and removed at the next start.
3. The installer is started with `/UPDATE /NORESTART` and `runas`, never silent (a silent setup skips the license page, so a changed agreement would go unseen: see [agreement-acceptance.md](agreement-acceptance.md)). If the person declines the administrator prompt, nothing changes and the update window says so. The app keeps running while the setup is open and waits for it; if the setup ends and the app is still alive, the update was not installed (cancelled, not an error). The setup closes the app itself when it begins replacing files (its `taskkill`).
4. In `installer/MacroGrid.iss`, an `/UPDATE` run skips the welcome, folder, program group, tasks and ready pages and shows the license page only when the agreement hash recorded in `HKCU\Software\Macro Grid` (`AcceptedAgreement`, per person: the setup reads and writes the hive of the account that approved the administrator prompt) differs from the hash of the text it ships (`AgreementHash`, passed in by `build-installer.ps1`); no record means the page is shown. The finished page closes by itself and `MacroGrid.exe --updated` starts afterwards as a `postinstall` entry with `runasoriginaluser`, so the app does not keep running as administrator (`runasoriginaluser` has no effect on a `[Run]` entry without `postinstall`: the app came back elevated when tried that way). The firewall rule is deleted before it is added, so an upgrade no longer leaves copies.

The digest protects against a broken or tampered download. There is no code-signing certificate, so it does not protect against a compromised GitHub account; see the Security model in [../architecture.md](../architecture.md).

## Code

Core (`src/MacroGrid.Core/Updates/`, no UI, unit-tested):

- `ReleaseVersion`, `ReleaseInfo`, `ReleaseFeed`: parse and order versions, read the releases list (`ETag`/`304`), pick the update and its installer asset and digest, the URL allow-list.
- `UpdatePolicy` (with an injected clock) and `UpdateState`/`UpdateStateStore`: whether to announce (enabled, snoozed, skipped, already announced, manual), remembered in `update-state.json` next to the ETag and the cached list. It is kept apart from `preferences.json` because the editor saves the preferences as one object and would overwrite these fields.
- `UpdateChecker`: one check end to end (fetch or cache, find the update, ask the policy).
- `InstallerDownloader`, `DownloadVerifier`, `InstallLocation`.

Host (`src/MacroGrid.Host/Updates/`): `UpdateService` (the timer, the `update` status item, the offer), `UpdateInstaller` (installed-copy detection through the Inno uninstall key, download, start, exit), `LocalFeedHandler` (debug), `UpdateSnapshot`. `TrayContext` has the "Check for updates" item and the notification; `HostText` has their texts.

API (loopback only like the rest of `/api`): `GET /api/update` (versions, notes, `canInstall`, `install` state and progress, last error), `POST /api/update/check`, `/install` (returns at once, the editor polls `GET /api/update`), `/snooze`, `/skip`, and `POST /api/windows/update`.

Editor: `windows/UpdateWindow.tsx` (`?window=update`), `state/useUpdate.ts`, `components/SafeMarkdown.tsx` (the release notes are rendered as React text, never as HTML, so a release body cannot inject script), the status-bar item, the Help > About button, the Preferences switches (`checkForUpdates`, `includePreReleases` in `AppPreferences`). The release notes stay in English; the rest is in `en.ts` and `tr.ts`.

## Release process

`release.yml` builds the installer on every tag and uses the `docs/CHANGELOG.md` section of the version as the release body (`scripts/release-notes.ps1`), because the update window shows it. Publishing a draft reaches every running install within about 6 hours, so the clean-PC installer test in [../guides/release.md](../guides/release.md) is mandatory before publishing.

## Trying it without a release

Set the environment variable `MACROGRID_UPDATE_FEED_FILE` to a JSON file in the shape of the GitHub releases list; the app reads it instead of calling GitHub (checks, the notification, the update window, Later, Skip). The download itself still needs a real release asset.

## Still to verify

Not tested yet against a running server or a real release (they need windows and an installed older build):

- The tray notification and its click, the tray menu item, the status-bar item and the update window in both languages.
- "Install now" on an installed 0.2.x build against a real newer release: download progress, UAC, the visible setup (agreement page only when the text changed, the finished page closing by itself, Cancel leaving the old version running), the app coming back with "Updated to X", the removed download folder, one firewall rule after two upgrades.
- Keep it light: a normal update is the administrator prompt and a progress window that closes by itself (no Next/Install/Finish, no folder or shortcut question, no restart question, no list of programs to close, the window in front); a changed agreement adds only the license page; the earlier folder, desktop shortcut and start-with-Windows choice are kept; a hand-run setup keeps all its pages.
- Downloaded installers: after a cancelled update the file stays (one only); after the next start on the new version the folder is gone; two different offers leave one; an installer run by hand (no `--updated`) is removed at the next start; nothing outside `%LocalAppData%\MacroGrid\updates` is ever touched.
- UAC declined, offline, rate-limited, the portable zip (release page only).
- The release workflow producing the installer and the notes on a tag.

## Later

- Phone app: Android handles app updates itself when installed from a store; a sideloaded APK would need the same feed and the "install unknown apps" permission (a separate plan).
- Plugins: update checks for installed plugins belong to [../plans/plugin-distribution-plan.md](../plans/plugin-distribution-plan.md), which shares the HTTP rules (HTTPS only, host allow-list, timeouts, size caps) but not this schedule.
- Download in the background before asking, so "Install now" is instant.
- Code signing, which would let the installer's publisher be checked as well.
