# User agreement acceptance (server)

Status: **built** (2026-09-25). Tested by hand on an installed copy: the update setup skips the agreement page when its text was accepted, shows it when there is no record, closes its progress window by itself and starts the app again as the person (not as administrator, when the setup is started from Explorer); the cancelled setup leaves the installed version running. Not tested by hand: the start-up dialog for a second Windows user (unit-tested comparison only) and an update that really changes the text. Decided by the owner on 2026-09-25 (see "Decision"). Repository: `macro-grid` only (`installer/MacroGrid.iss`, `installer/build-installer.ps1`, `Host`, docs). Part of the auto-update work: [auto-update.md](auto-update.md).

## Why

The installer shows the user agreement (`installer/license-agreement.txt`) and the person must accept it before anything is installed. The automatic update used to run the installer with `/SILENT`, and a silent setup skips the license page. So an updated agreement would reach people who never saw it. The owner's rule: when the agreement changes, everyone who updates must accept the new text.

The first case is already here: the agreement gets a sentence about the automatic update check and a "no warranty" box, and everyone on 0.2.x accepted the old text, so the first update to the version that carries this feature must ask.

## Decision

**The update is not silent.** "Install now" starts the installer as a normal, visible setup, like the updaters of other desktop programs: a small window with a progress bar while the files are replaced, and the app comes back on its own. The setup itself shows the agreement, so there is no separate acceptance window inside the app.

- The setup shows the **license page only when the person has not accepted this exact text yet**. Every other page (welcome, folder, tasks, ready) is skipped during an update, so a normal update is: administrator prompt, progress window, done.
- **Decline = Cancel in the setup, and the old version keeps running** (owner decision). Nothing is changed and the version that is already installed keeps working (its own agreement was accepted earlier). The update window says the update was cancelled, and it offers "Later" as usual. There is nothing to do in the app, no locked state and no uninstall prompt.
- A normal (wizard) install, run by hand, **always shows the license page** (owner decision); only the update run skips it when the text was already accepted. The record is written by every successful install.
- **The record is per person** (owner decision), not per PC: the agreement is between the software and the person. It is stored under `HKCU`, so each Windows user has their own.
- **The update window has a text area that lists the changes** (the release notes of every version between the installed one and the new one, newest first; it exists already). When the new version has a new user agreement, the notes say so ("this version has a new user agreement"), and **it has to be accepted during the install**: the setup shows the license page and cannot continue without Accept. The old app cannot read the new text before installing, so the setup is where it is shown.

### Keep it light (owner decision)

A visible setup must still be easy: an update should ask as little as possible of the person.

- **A normal update is one click:** the administrator prompt (UAC). Then only the progress window, which closes on its own, and the app starts again. No "Next", no "Install", no "Finish", no folder or shortcut questions.
- **A changed agreement adds one page:** the license page with Accept, then straight into the installation. No welcome page before it and no "ready to install" page after it.
- The setup **keeps what the person chose earlier** (install folder, desktop shortcut, start with Windows) and never asks again on an update. It does not ask to restart Windows and it does not stay open behind other windows (the progress window comes to the front).
- It **closes a running Macro Grid by itself** (the app already exits first) and never shows a "close these programs" list.
- The WebView2 runtime is only installed when it is missing, as today, without a page of its own.
- A wizard install by hand (first install, or running the downloaded setup) keeps all its pages; only the update run is trimmed.

## Building blocks

- **Agreement file:** it has a "Last updated" line and CRLF line ends, and both are part of the text. The hash is computed over the bytes as shipped (the installer and `LegalDocuments` read the same file). The date must be bumped whenever the text changes. Any edit, even a typo fix, asks everyone again, so batch wording changes.
- **Build:** `build-installer.ps1` computes the SHA-256 of `license-agreement.txt` and passes it to `MacroGrid.iss` as a define (`AgreementHash`).
- **Record:** the setup writes `AgreementHash` to `HKCU\Software\Macro Grid` (value `AcceptedAgreement`) after a successful install, whether the person accepted the page or it was skipped because the hash already matched, and it reads the same value to decide about the license page. The setup runs elevated: when the person elevates with their own account (the usual case) `HKCU` is theirs. If an administrator with another account approves the prompt, `HKCU` is that administrator's, the stored hash does not match, and the license page is shown again. That errs on the side of showing the agreement, never of hiding it.
- **Setup script:** in `[Code]`, `ShouldSkipPage` skips `wpLicense` when the stored value equals `AgreementHash`, and skips welcome, directory, tasks and ready pages when `IsUpdateRun` (the `/UPDATE` switch). The finished page closes on its own during an update (`CurPageChanged` on `wpFinished` for an update run, checked on a real run), and the existing `--updated` entry starts the app as the person. `UsePreviousAppDir`, `UsePreviousTasks` and `UsePreviousGroup` stay on (Inno's default) so an update keeps the earlier choices; `CloseApplications` and `RestartApplications=no` stay as they are.
- **`UpdateInstaller`:** starts the setup with `/UPDATE /NORESTART` (no `/SILENT`, no `/VERYSILENT`), elevated as before. Keep `/SUPPRESSMSGBOXES` out unless tests show a prompt that must not appear; the progress window is wanted. The app still exits itself first, and treats a non-zero exit code or a cancelled setup as "update not installed" (not an error).
- **Downloaded installer cleanup (owner request; the auto-update session checks it):** the downloaded setup (about 63 MB in `%LocalAppData%\MacroGrid\updates`) must never pile up. Today `UpdateInstaller.CleanUpDownloads()` runs only when the app starts with `--updated` (`Host/Program.cs`), so these cases leave files behind: the setup was cancelled or failed, the person installed by hand with a downloaded setup (no `--updated`), a download was aborted (`.part` files), or several versions were offered and never installed. Rule: keep **at most one** downloaded installer, the newest offered and not yet installed; delete everything else. Delete all of it (the folder too) when the running version is equal to or newer than every downloaded version. Run this on every start (not only after an update), after a successful install, and when a new download replaces an older one. It is only for what the updater downloaded; a file the person saved elsewhere is never touched. Deleting must never block or fail the start-up (log a warning). Add a unit test for the decision (which files stay) and note it in the manual test.
- **Docs:** `docs/design/auto-update.md` and `docs/guides/release.md` describe the setup as visible; the agreement re-acceptance rule (bump the date, one hash per text) goes into `docs/guides/release.md`.

## Tests

- Unit: the hash of the agreement file (stable across a rebuild; changes with any edit); `UpdateInstaller` arguments have no `/SILENT` or `/VERYSILENT`.
- Manual, on a clean PC: install 0.2.1 (agreement accepted in the wizard, no record), update to the new build (license page shown once, Accept continues and the app returns, Cancel leaves 0.2.1 untouched and running), update again with the same agreement (no license page, only the progress window), change the agreement text and update (license page again).

## Second step: the agreement window in the app (owner decision, 2026-09-25)

**As built:** a native Windows dialog (`AgreementDialog`, opened by `AgreementGate` in `Program.Main`) that shows the agreement text with Accept and Decline **before the server is built or started**. Nothing of the server exists until the person accepts, which covers "no actions, no devices, no update check, no notification" without a special mode. It replaces the planned `?window=agreement` page, which would have needed the web server that must not run yet. Accept writes the hash and the date to `HKCU` (`AgreementRecord`); Decline or closing the window exits and it asks again at the next start. A build without the agreement file (a development build) asks nothing. The comparison is `AgreementAcceptance` in Core (unit-tested); the registry access is not.

The record is per person, so another Windows user of the same PC has no record for a new text, even though the setup was accepted by the person who updated. So **the app also checks at start**:

- On start, when the current person's `HKCU\Software\Macro Grid` value `AcceptedAgreement` is missing or differs from the hash of the agreement the app ships, the app shows the agreement in a small tool window (like Help) with **Accept** and **Decline**, before the editor opens and before anything else is offered.
- **Accept** writes the hash and the date to `HKCU` and the app continues as usual. **Decline** closes the app (it cannot be used without accepting); it asks again at the next start.
- While it is not accepted the server does not run actions and accepts no devices (start the listener only after acceptance, or refuse everything until then). The update check and the tray notification also stay quiet until accepted.
- The person who just accepted the license page in the setup has the record already, so they are not asked again. A wizard install by hand writes the record too.
- Core: `AgreementAcceptance` (hash of the shipped text, compare with the record; testable without the registry). Host/editor: `?window=agreement` (Accept, Decline, the text from `GET /api/legal`).
- Tests: unit (same hash accepted, changed text not, missing record not); manual: sign in as a second Windows user and start the app (asked once, Accept continues, Decline closes it, next start asks again).

## Open questions

None. Decided by the owner on 2026-09-25: Decline keeps the old version running; the update window lists the changes and a new agreement must be accepted during the install; the record is per person; the app asks other people of the same PC at start (second step).
