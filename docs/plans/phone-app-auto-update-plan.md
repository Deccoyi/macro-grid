# Auto-update (phone app)

Status: **approved by the owner** (2026-09-25), with the decisions under "Decisions" at the end. Phases 1 to 4 are approved to build in order; phase 5 is asked for separately. Progress (2026-09-25): phases 1 to 4 are all done and committed. Phases 1 (release process), 2 (native plugin) and 3 (web side: feed, policy, state, schedule, update screen, drawer row and dot, full-screen Settings page, texts, 28 new unit tests) are in `macro-grid-client` (`63873af`, `4f74445`, `8bc0734`, released as client `0.2.0`). A real self-update `0.1.2 → 0.1.3` was run on a phone through the app's own screens (Android 16, pairing survived the update, the downloaded file was removed afterwards; a `getLong`/JSON-integer bug found on that run was fixed, see "Risks to verify"). Phase 4 (legal text, docs, websites) is done: the app's Settings page, `README.md`, `SECURITY.md`, `docs/architecture.md`, both websites (English and `tr/`) in `macro-grid-client` (`d686fa0`), and `macro-grid`'s website (`guide/phone-app.md`, `guide/security.md`, English and `tr/`, `ea928cc`). As built: the SHA-256 is computed by reading the finished file once (not while streaming), and `cleanUp` takes the one version to keep (`keep`, null for none) because the web side decides which version that is.
**Repositories:** `macro-grid-client` (all code, the release workflow, its docs and website); `macro-grid` (this plan, and the phone-app and security pages of its website only).

The Android app finds out on its own that a newer release exists on GitHub, shows the changes, and after one tap downloads the APK, verifies
it and hands it to Android's installer. It follows the server's auto-update ([../design/auto-update.md](../design/auto-update.md)) wherever
the platform allows: same feed rules, same version order, same Later / Skip / turn-off behavior, same "never pile up downloads" rule, same
"ask as little as possible" rule ([../design/agreement-acceptance.md](../design/agreement-acceptance.md), "Keep it light").

## What exists today (checked 2026-09-25)

- **Releases:** `client-v0.1.0` ships `MacroGrid-0.1.0.apk` (signed locally with the release key). `client-v0.1.1` ships only
  `MacroGrid-0.1.1-debug.apk`: the repository has no signing secrets, so `release.yml` fell back to a debug build. Both are marked pre-release,
  both assets have a `sha256:` digest.
- **Signing:** the release key lives on the owner's PC only (outside every repository). `release.yml` signs only when four repository secrets
  exist; without them it attaches a debug APK. A CI debug key is created fresh on each runner, so two debug builds do not even share a key.
- **Versions:** `package.json` is the single source. `android/app/build.gradle` derives `versionCode = major*10000 + minor*100 + patch` (0.1.1 is
  101), so it grows with every release as long as minor and patch stay below 100. A pre-release label (`0.2.0-alpha`) breaks the build: the
  gradle script splits on `.` and `"0-alpha".toInteger()` fails. So client versions are plain `X.Y.Z`, and "pre-release" is only GitHub's flag
  (`release.yml` always passes `--prerelease`). The app already sends its version in `hello.clientVersion`.
- **Native code:** two custom bridge plugins in Java (`KioskPlugin`, `GestureExclusionPlugin`, registered in `MainActivity`). No Kotlin in the app
  module. `minSdk 24`, `targetSdk 36`. The manifest already declares a `FileProvider` (from the project template) whose paths include the whole
  external storage; nothing uses it.
- **Texts:** the app is already bilingual (`src/i18n/tr.ts`, `en.ts`, picked from the phone's language). The public release notes file still says
  "Screens are in Turkish for now", which is out of date.
- **Release body:** `docs/release-notes-client-v0.x-alpha.md`, one cumulative "what is in this release" text, the same for every 0.x release. That
  is not usable as per-version notes in an update screen.

## Behavior

| When | What happens |
|---|---|
| Start-up | A check runs about 30 seconds after the app starts (after the first connection attempt, never blocking it). |
| While the app is open | Another check every 6 hours. The phone usually stands as a deck with the app open for hours, so this is the main path. |
| Back to the foreground | When the app becomes visible again and the last check is older than 6 hours, it checks (timers do not run reliably while the app is in the background). |
| Update found | A small dot on the drawer handle and an "Update available: 0.3.0" row at the top of the profile drawer (the drawer gets nothing else). At a **cold start** the update screen opens by itself once per version (never while the deck is in use: see "Options considered", C). |
| Update screen | Current and new version, a scrollable text area with the release notes of **every** version between them (newest first), and **Update now**, **Later**, **Skip this version**, plus a link to the release page. |
| Later | The screen does not open by itself for 24 hours. The dot and the drawer row stay. |
| Skip this version | Not offered again for exactly that version; a newer one is offered as usual. |
| Manual check | Settings page: the app version and a "Check for updates" button. Ignores Later and Skip; says "You are up to date" or reports the failure. |
| Update now | Progress bar while downloading (the APK is about 27 MB), verification, then Android's own install confirmation. The download follows the "Download updates over" setting (see "Mobile data"). After the install Android closes the app and the person opens it again; see "After the install". |
| Turned off | Settings page: "Check for updates automatically" (on by default) and "Include pre-releases" (on by default: every client release is a pre-release today, so off would offer nothing). Off stops the automatic checks; the manual check still works. |

Offline, GitHub unreachable or rate-limited: nothing is shown, the next interval retries (a manual check says so).

One tap is the goal: **Update now** plus Android's confirmation. The first time only, Android also asks to allow "install unknown apps" for
Macro Grid (see "Install").

## Where updates come from

- `GET https://api.github.com/repos/Deccoyi/macro-grid-client/releases?per_page=50`.
- Only tags starting with `client-v`, not drafts. Pre-releases (a version label, or GitHub's pre-release flag) count only while
  "Include pre-releases" is on.
- The version is parsed from the tag and compared with **the same rules as the server's `ReleaseVersion`** (`0.1.1 < 0.2.0-alpha < 0.2.0`,
  labels compared identifier by identifier, build metadata ignored). The parser accepts labels even though client tags have none today, so a
  later change of the tag scheme does not break older apps. Only strictly newer versions count; never a downgrade.
- The asset must be named exactly `MacroGrid-<X.Y.Z>.apk` (what `build-release-apk.ps1` names a release-signed APK) and carry a
  `sha256:` digest. `-debug.apk` and `-unsigned.apk` are never offered for install. A release without the right asset shows only the link to its
  release page (the server's `CanInstall`).
- `ETag` is stored and sent back as `If-None-Match`; an unchanged list answers `304`. `User-Agent: MacroGridClient/<version>` is the only thing sent
  about the app; nothing about the person or the phone.
- **The request is made natively, not with the WebView's `fetch`.** GitHub allows the WebView's cross-origin request, but the WebView would send its
  own user agent, which names the phone model and Android version. The native call sends exactly the header above. The JSON is then parsed in
  TypeScript (testable with the existing unit tests).
- Rate limit: 60 anonymous requests per hour per public IP. The phone usually shares its IP with the PC running the server (one check every 6 hours
  each), so the two together stay far below it.

## Download

In the native plugin, never in the WebView (a 27 MB binary through the bridge is slow and memory-hungry):

1. Only `https` on the same host list as the server (`github.com`, `objects.githubusercontent.com`, `release-assets.githubusercontent.com`).
   Redirects are followed by hand (automatic following off), at most 5, each hop checked against the list.
2. Into the app's own cache folder: `cache/updates/<X.Y.Z>/MacroGrid-<X.Y.Z>.apk.part`, renamed when complete. No storage permission is needed and
   no other app can read it.
3. The size must equal the asset's `size` and the SHA-256 (computed while streaming) the asset's digest; otherwise the file is deleted and the
   screen shows an error. A verified file from an earlier attempt is reused.
4. **Signer check before install:** the plugin reads the downloaded APK's signing certificate (`PackageManager.getPackageArchiveInfo` with
   signing info) and compares it with the running app's. Different (a debug APK, a wrong key) means "This release cannot update the installed app;
   install it by hand", instead of a confusing install failure.
5. Download only after **Update now**, never in advance: no mobile data is spent without the person asking.

### Mobile data (owner decision)

- Setting **"Download updates over"**: **Wi-Fi only** (default) or **Wi-Fi and mobile data**.
- "Wi-Fi" means an **unmetered** connection, read natively from Android's connectivity service (the active network's capabilities: not metered).
  A metered Wi-Fi (a phone hotspot) counts as mobile data, which is what the person wants to protect.
- **Wi-Fi only and the phone is on mobile data:** the download does not start. The update screen says "The update downloads when you are on
  Wi-Fi" and offers a one-time **Download with mobile data anyway**, which first asks "Download 27 MB over mobile data?" (the real size from the
  release asset). A one-time choice does not change the setting.
- **Wi-Fi and mobile data, and the phone is on mobile data:** **Update now** asks the same size confirmation first. On Wi-Fi nothing is asked.
- **The check itself runs on mobile data too.** It is one small JSON request every 6 hours (a few tens of KB, usually a `304` with no body at all
  thanks to the ETag), and knowing that an update exists is what lets the screen say "downloads when you are on Wi-Fi". Blocking the check would
  hide updates from a phone that is rarely on Wi-Fi and save almost nothing.
- If the connection turns metered in the middle of a Wi-Fi-only download, the download stops, the partial file is deleted, and the screen shows
  the same Wi-Fi message.

**Never pile up (owner rule):** at most one downloaded APK, the newest one that is newer than the running version, and only in
`cache/updates/`. On every start everything else there is deleted (older versions, `.part` files, the version that is now installed); when the
running version has caught up the folder goes. Nothing outside that folder is ever touched. Deleting never blocks or fails the start (logged).
Android may also clear the cache folder by itself; that only means a re-download.

The digest protects against a broken or tampered download. Unlike the server, the phone has a second protection: **Android installs an update only
when it is signed with the same key as the installed app**. So as long as the key stays on the owner's PC, even a compromised GitHub account cannot
push a working update to installed phones. This is the main reason for the signing decision below.

## Install

- **Android's `PackageInstaller` session API**, not the older "open the APK file" intent. The session API works from `minSdk 24`, reports the
  result back to the app (installed, cancelled, failed with a reason), and is the only way to the fewer-prompts option below. The APK is streamed
  into the session, so **no `FileProvider` is needed** (the unused template provider could be removed or narrowed in the same change; check that no
  plugin relies on it).
- New manifest permission `REQUEST_INSTALL_PACKAGES`.
- Before installing, `canRequestPackageInstalls()`. When it is off, the app **first shows its own explanation window** (owner rule: the person must
  never land on an Android settings page without knowing why): "To update itself, Macro Grid needs Android's permission to install apps. On the next
  screen turn on 'Allow from this source', then come back." with **Continue** and **Cancel**. Only **Continue** opens that setting for this app
  (`ACTION_MANAGE_UNKNOWN_APP_SOURCES` with the package); Android shows it as a full settings page, not a popup, and kiosk mode does not hide it.
  Coming back, the app checks again and continues with the install by itself (no second tap on **Update now**). **To verify:** on some Android versions changing that setting restarts the app process; then the update
  screen must reopen with the verified file instead of starting over.
- Android's confirmation dialog **cannot be skipped** by a normal app; that dialog is the one tap besides **Update now**.
- Cancelled in Android's dialog: nothing changes, the screen says the update was not installed and offers Later. Failed (incompatible signature,
  not enough space, downgrade): a plain message and the release page link.

### Optional: fewer prompts on Android 12 and newer (phase 5, asked for separately, needs verification)

Owner decision: this improvement targets **Android 12 (API 31) and newer only**. Older Android versions keep the normal confirmation dialog and get
no extra work. `minSdk` stays 24; no Android version loses support.

A session can ask for `USER_ACTION_NOT_REQUIRED`. Android then installs without its confirmation dialog, but only when all of these hold: Android
12 (API 31) or newer, the app holds `REQUEST_INSTALL_PACKAGES` with the setting allowed, it updates **itself**, the app is the **installer of
record** of the installed copy, and its `targetSdk` meets the platform's current minimum (36 does today). A copy installed by hand has the file
manager or the system installer as installer of record, so the first self-update still asks; later ones could be silent. Newer Android versions
also have "update ownership", which may add a prompt. This must be tried on real phones (at least one Android 12 or 13 phone and one 14 or newer)
before it is promised. Even then "Update now" stays a deliberate tap: nothing installs without the person asking.

## After the install

Android stops the app when it replaces it, and a session install does not reopen it. Options:

- **A (owner decision for the first build):** say so before the install ("Macro Grid closes during the update; open it again afterwards"). At the next
  start the app says "Updated to X" once. Simple and predictable.
- **B (not in the first build):** a receiver for Android's "my package was replaced" broadcast starts the app again. Android restricts starting a screen from the background
  (Android 10+), so it may not work at all; to verify before relying on it. Worth it because the phone often stands as a deck without anyone
  touching it.

## Signing and the release process (decided by the owner, 2026-09-25)

An update installs only over an app signed with the same key, so the APK attached to a release must be release-signed. **Decision: the APK is
signed on the owner's PC, and the signing key and its passwords are never put on GitHub** (no repository or environment secrets).

Why:

- **The key cannot be replaced.** Whoever has it can make phones accept a malicious APK as an update of Macro Grid. If it has to be changed,
  every installed app must be uninstalled and installed again.
- **One place is the smallest attack surface.** With the key only on the owner's PC, a compromised GitHub account or a changed workflow can
  publish a release, but installed phones refuse its APK (wrong signer). With the key in CI they would accept it.
- Alpha, one maintainer; the extra work is a few minutes per release.
- The updater's SHA-256 check works the same for a hand-uploaded APK: GitHub computes the `digest` for every uploaded asset. **To verify** once
  on the first release.

**If the key is lost, the update path is closed for good:** every phone would have to uninstall and reinstall. The backup rules (a copy outside
every repository, in a safe place, the passwords in a password store) are already in `docs/release.md`; the release checklist links to them.

### Release flow (macro-grid-client)

1. The tag push runs `release.yml` as today (typecheck, tests, build) and creates the **draft** release, **without any APK attached**.
2. On the owner's PC, `scripts\build-release-apk.ps1` builds the signed APK (signed through `android\keystore.properties`, which points at the key
   outside the repository). Its name is `MacroGrid-<X.Y.Z>.apk`, exactly the name the updater looks for.
3. **Check before uploading** (new checklist items in `docs/release.md`): `apksigner verify --print-certs` shows the release certificate (its
   SHA-256 fingerprint is written in `docs/release.md`; the fingerprint is public, the key is not), and the APK's `versionName` is the tag's
   version and its `versionCode` is `major*10000 + minor*100 + patch` and higher than the previous release's (`aapt dump badging` or
   `apkanalyzer`). Then install it on a real phone over the previous release.
4. Upload it to the draft (the release page on the web, or the command-line client), check that the asset shows a digest, publish.

### What `release.yml` does with its own APK

A debug or unsigned APK must **never** be attached to a release: it cannot be installed over a release-signed app, and the updater would at best
refuse it (it only accepts `MacroGrid-<X.Y.Z>.apk`, and its signer check would stop a wrongly named one).

| | A: build it, keep it off the release (**chosen**) | B: stop building an APK in CI |
|---|---|---|
| What CI proves | The tag builds into an installable app (the Android build, the native plugin and the web bundle together). | Only the web part (typecheck, tests, bundle); an Android build error shows up on the owner's PC first. |
| Where the APK goes | A workflow artifact (kept for a short time, for a quick look), never the release. | Nowhere. |
| Risk of publishing the wrong file | None: the draft has no APK until the owner uploads one. | None. |

Chosen: **A** (the recommendation, applied with the owner's approval of the plan). The signed step (`HAS_KEYSTORE`) is removed from `release.yml`, since the secrets will never exist, and the debug build
is kept only as a check. The `gh release create` call gets no files. `docs/release.md` and the client website's download page lose the text
about a debug APK on a release.

### Other release-process changes (macro-grid-client)

- **Per-version notes:** the release body becomes the version's section of `docs/CHANGELOG.md` (like the server's `scripts/release-notes.ps1`, as
  a small script run in `release.yml`), followed by a short fixed footer (alpha, needs the server, same network). The update screen shows these bodies.
  The cumulative `release-notes-client-v0.x-alpha.md` is retired or becomes that footer; its "Turkish only" line goes.
- **Version guard:** `build.gradle` fails with a clear message when the version has a label or when minor or patch is 100 or more (today it would
  fail with a number error, or silently produce a `versionCode` that does not grow). Client versions stay plain `X.Y.Z`; "pre-release" stays
  GitHub's flag.
- `versionCode` must grow with every release (Android refuses a lower one and the updater never offers an older version anyway). The derivation
  already does this; the release checklist says "never re-release the same version with a different APK".
- Publishing a draft reaches every open app within about 6 hours, so the real-phone test in the checklist is mandatory before publishing.

## The first versions (transition)

- The updater helps only from the first version that contains it. **That version must be installed by hand once**, like today.
- Phones on `0.1.0` (release key) can install it over their copy by hand.
- Phones on `0.1.1` have a CI debug build: Android refuses to install a release-signed APK over it. Those people must **uninstall once** (the
  pairing and saved servers are lost) and install the new version. The release notes and the download page must say this plainly.

## Code (macro-grid-client)

Native (`android/app/src/main/java/com/macrogrid/client/`), one new bridge plugin `UpdaterPlugin`, registered in `MainActivity`. **Java**, like
the two existing plugins (Kotlin would add the Kotlin build plugin to the app module for one class; possible, but not worth it):

- `fetchReleases({ etag })` → `{ status, etag, body }` (the native request above, 10 s timeouts, response capped at a few MB).
- `download({ url, size, sha256, version })` with progress events, the host allow-list, the size and digest check, the signer check.
- `canInstall()`, `openInstallSettings()`, `install({ version })` (the session, result as an event), `cleanUp({ runningVersion })`.
- `getConnection()` → `{ connected, unmetered }` (Android's connectivity service), used before and during a download.

Web (`src/`):

- `update/releaseVersion.ts` and `update/releaseFeed.ts`: parse, order, find the offer and its asset; same cases as the server's unit tests
  (ported, not shared: the server is C#).
- `update/updatePolicy.ts` with an injected clock, and `update/updateState.ts`: enabled, snoozed until, skipped version, last announced, last check,
  ETag, cached list. Stored with the existing `storage.ts` helpers under its own key, apart from `macro-grid.settings`.
- `hooks/useUpdate.ts`: the schedule (start, 6 hours, visibility), the state for the UI.
- `components/UpdateScreen.tsx` (notes as plain text with light formatting of headings and list items; never as HTML, so a release body cannot
  inject markup; the Wi-Fi message and the mobile-data confirmation), the drawer row and the dot. `AppSettings` gets `checkForUpdates`,
  `includePreReleases` and `updateNetwork` (`"wifi"` by default, or `"any"`).

### Settings page (new UI, owner decision)

Today settings are a small dialog (`SettingsPanel.tsx`, opened from the gear at the bottom of the drawer) with kiosk mode, orientation and the
disclaimer. The owner does not want the update settings squeezed into it or into the drawer. So:

- The dialog becomes a **full-screen Settings page** (same gear entry point, a back arrow at the top, the Android back gesture closes it), in the
  app's existing style (`theme.ts` colors, the existing switch and segmented buttons) and the rules of `macro-grid/docs/ui/ui-guidelines.md`.
- Sections, kept short: **Display** (kiosk mode, screen orientation: moved as they are), **Updates** (the version, "Check for updates
  automatically", "Include pre-releases", "Download updates over": Wi-Fi only / Wi-Fi and mobile data, the "Check for updates" button with its
  result, and the one-line note about the connection to github.com), **About** (the disclaimer).
- Small scope: no new settings besides the update ones, no navigation library, one component replacing the dialog.
- All texts in `tr.ts` and `en.ts`. Release notes stay in English, like the server.
- `native/updater.ts`: the typed wrapper (no-op on the web deck, which never updates itself).

Debug: a build-time or developer setting that reads a local JSON file instead of GitHub (the server's `MACROGRID_UPDATE_FEED_FILE` idea), for
trying the screens without a release.

## Legal text and websites

The app gets its first connection to something other than the server, so, as on the server:

- **App (macro-grid-client):** a line in the Updates section of the Settings page: "Checks github.com for a newer version about every six hours; sends
  only the app name and version. Nothing is installed without your tap." The existing disclaimer stays.
- **Docs (macro-grid-client):** `README.md` and `SECURITY.md` ("the app talks only to your server" is no longer true), `docs/architecture.md`
  (security section), `docs/release.md`, both changelogs when built.
- **Website (macro-grid-client/website, English and `tr/`):** `requirements.md` and `download.md` (the update check, the one-time permission, the
  uninstall note for `0.1.1`, the debug-APK paragraph rewritten).
- **Website (macro-grid/website, English and `tr/`):** `guide/phone-app.md` (updates) and `guide/security.md` (a second optional connection, next to
  the server's update check).

## Risks to verify

- **The phone's built-in malware scanner** may warn about an app it does not know. It warns the same way for a hand installation, so this is not new
  with the updater (owner's view). Check once that a self-update is not blocked outright.
- **The platform vendor's developer-verification requirement** for apps installed outside the store (announced to roll out region by region). It
  affects a hand installation just as much, so it is not specific to updates, but it could decide whether sideloading keeps working at all. Check its
  status before the first release with the updater.
- Changing the "install unknown apps" setting restarting the app (see "Install").
- Kiosk mode: Android's install dialog and the settings page appear over the immersive deck; check that the app restores kiosk mode when it returns.
- Downloads on a phone with little free space.
- **Tried on a real phone (Android 16, 2026-09-25, a debug test build under its own package name, over adb; nothing published was touched):**
  the releases list (`200`, then `304` with the ETag), the connection type (mobile data reported as metered, Wi-Fi as unmetered), rejected
  requests (other host, bad version, bad digest), a real 27 MB download over Wi-Fi in under 4 s with progress up to 100 %, size and SHA-256 checks,
  the signer check refusing an APK signed with another key (and deleting it), the clean-up (old versions and `.part` files removed, the newest version
  and unrelated entries kept), the permission check and the Android settings page, and a full self-update `0.1.2 → 0.1.3` through the installer
  session (confirmation, then the app was replaced). **Not tried:** the metered refusal while on mobile data with a valid request (the phone was on
  Wi-Fi by then), cancel in Android's dialog, a failed install, the `installResult` event (the app is stopped when it is replaced), kiosk mode
  after the dialog, the update from a hand-installed release build (installer of record), Android 12+ without the dialog.
- **Found on that phone and fixed:** Capacitor's `getLong` ignores a number that JSON parsed as an integer, so every real download request was refused
  as `bad_request` until the size was read as a plain number (unit tested). Also: a stale `cap sync` (an old plugin list) stopped the QR scanner in
  a test build, so **always run `npx cap sync android` before an Android build** (`build-release-apk.ps1` does).
- **The platform's built-in scan and a third-party security app both showed a warning** for the debug test build while it was being installed
  (the third-party one called it malware); the person tapped through and **the install completed**. Same as a hand installation, so not specific
  to updates (owner's view). The update screen should tell the person to expect Android's install dialog and possibly a security warning, and to
  choose to install anyway. Still to check: a real release-signed update behaves the same.

## Options considered

- **A. Background checks (a scheduled background job) and notifications: not recommended.** The phone mostly stands with the app open, so a
  foreground check at start, every 6 hours and on return already sees a release within hours. A background job would add a library, battery use and,
  to be of any use, a notification (the notification permission on Android 13+, a channel, texts). It still could not install anything: Android's
  confirmation needs the person at the phone. Revisit only if people report missing updates.
- **B. Check in the WebView with `fetch`: rejected** for the user-agent reason above; only the parsing is in TypeScript.
- **C. Opening the update screen by itself while the deck is in use: rejected.** A dialog over the deck in the middle of a stream is exactly the
  interruption the owner wants to avoid. It opens by itself only at a cold start; otherwise the dot and the drawer row wait for a tap.
- **D. The server tells the phone it is out of date (later, not in the first build).** The server already receives `hello.clientVersion`. It could
  send a small "a newer app exists" hint, which helps phones that are rarely opened. Needs a protocol capability and the server's own feed of client
  releases; separate plan.
- **E. The server serves the APK over the local network (later, not in the first build).** The PC downloads and verifies the APK once and phones fetch
  it over the LAN, without reaching GitHub. Same signer check on the phone. Useful for phones without internet access. Separate plan.
- **F. Download before asking, so "Update now" is instant (later).** Costs mobile data without consent; only on unmetered networks if ever.
- **G. Signing in CI (later, only if the owner ever wants it; not in the first build).** The safer way: the key in a GitHub *environment* secret
  (not a repository secret) whose environment only accepts tag refs matching `client-v*` and requires the owner's manual approval for every run;
  a separate signing job that does nothing else; every third-party action pinned to a full commit, not a version tag; the keystore written to the
  runner's temp folder and deleted in an `always()` step; no `pull_request` trigger on that workflow. Even then the key sits outside the owner's PC,
  so a compromised account with approval rights could still use it. Today's decision stands until the owner reopens it.

## Phases

1. **Release process** (`macro-grid-client`): `release.yml` (no APK on the release, the unused signed step removed, per-version notes), version
   guard, `docs/release.md` (the local signing and upload steps, the signer, `versionName` and `versionCode` checks, the certificate fingerprint,
   the lost-key warning). Can ship on its own, before the updater.
2. **Native plugin** (`macro-grid-client`): fetch, download, verify, signer check, cleanup, permission, session install.
3. **Web side** (`macro-grid-client`): feed, policy, state, schedule, update screen, drawer row, the full-screen Settings page (existing settings
   moved, update settings added), texts, unit tests.
4. **Docs and websites** (`macro-grid-client`, `macro-grid` website pages).
5. **Optional, asked for separately:** fewer prompts on Android 12+ only, after the check on real phones.

Tests: unit tests for the version order, the feed parsing (drafts, other tags, missing digest, debug assets, bad URLs), the policy and the cleanup
decision. By hand on real phones: an older release-signed build against a made-up newer release (the debug feed file) and a real one; permission off
and on; cancel in Android's dialog; a debug APK refused by the signer check; offline; the download folder holding one file at most and empty after
the new version starts; kiosk mode after returning.

## Decisions

Decided by the owner on 2026-09-25, when approving the plan:

1. **Signing:** signed on the owner's PC and uploaded by hand; the key and its passwords never go to GitHub. CI builds its debug APK only as a
   workflow artifact and attaches nothing to the release (option A).
2. **Update screen:** opens by itself at a cold start, once per version; never while the deck is in use.
3. **After the install:** the person opens the app again; no automatic restart in the first build.
4. **Fewer prompts:** only for Android 12 and newer, as the separate phase 5; older versions keep the normal dialog, `minSdk` stays 24.
5. **Mobile data:** a "Download updates over" setting, Wi-Fi only by default, with a one-time "download with mobile data anyway" and a size
   confirmation; the check itself also runs on mobile data. The update settings live on a new full-screen Settings page, not in the drawer.
6. **`0.1.1` users:** an "uninstall once and reinstall" note in the release notes and on the download page is enough.
7. **Plugin language:** Java, like the two existing plugins.

## Open questions

None.
