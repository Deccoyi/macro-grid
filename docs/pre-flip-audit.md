# Pre-flip audit (task D.19)

Status of the ten gates from [open-source-plan.md](open-source-plan.md), with the evidence behind each one.
Audit date: 2026-09-24. Repos: `Deccoyi/macro-grid`, `macro-grid-client`, `macro-grid-plugin` (all private).

**Verdict: ready to open as an alpha.** All gates that block opening are passed; the remaining items (G7, G9 and the after-the-flip list) do not need to happen before the repositories go public. Nothing found so far blocks the flip.

| Gate | Status | Evidence |
|---|---|---|
| **G1** Identity scrub | ✅ Pass | All commits in all three repos (78 / 33 / 28) have author and committer the project identity (Deccoyi + project mailbox). A search of every commit (metadata, messages, file contents) for the old employer name, both old personal addresses and the user-profile name finds nothing. Old backups were deleted; the repos were deleted and recreated on GitHub. |
| **G2** Community files | ✅ Pass (owner review) | Every repo has LICENSE (MIT), README, SECURITY.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue templates (3 / 3 / 4), a PR template, THIRD_PARTY_NOTICES.md and a `licenses/` folder (22 / 28 / 5). |
| **G3** No secrets / PII | ✅ Pass (grep only) | Re-run on 2026-09-24 over every commit of all three repos and over all current files: no employer, personal-name, user-profile-path, machine-address or company-network string; no known secret format (cloud keys, tokens, private keys); no key, keystore or `.env` file ever committed; the release key lives outside the repos. The dedicated project mailbox appears only in commit metadata (and in old file versions in history). No gitleaks run yet: add it to CI later. |
| **G4** Installer tested | ✅ Pass | Tested by the owner on a clean, domain-joined company PC on 2026-09-24: install, the user agreement page, editor opens (WebView2 data folder fixed), phone pairs and the mute toggle works, start with Windows after a restart, quit from the tray ends the process, uninstall (also while running) removes everything. Fixes found by that test: WebView2 data folder, plugins built against SDK 0.3.0 loading on 0.3.1, tray exit, uninstall while running. Unsigned installer: Windows shows an unknown-publisher warning. |
| **G5** Release APK on a device | ✅ Pass | Signed `MacroGrid-0.1.0.apk` (verified with `apksigner`), installed on a real phone, paired with the renamed server, live values and buttons work. Keystore backed up by the owner. Only a re-run against the final release server build is left. |
| **G6** SDK on NuGet | ✅ Pass | `MacroGrid.Plugin.Abstractions` 0.3.0 is live on nuget.org (Trusted Publishing, tag `sdk-v0.3.0`). A clean build of both plugins with an empty package cache restores it from nuget.org and passes; the plugin CI dropped the server checkout. The package's project links stay 404 for outsiders until the repos are public. |
| **G7** Tutorials verified | ⏳ Open (owner, not blocking) | Docs site builds with no dead links; both examples build and load in the real plugin manager; JS example and plugin install worked on the owner's setup; the C# example restores the published SDK. The tutorials still have to be followed verbatim on a clean setup; do it before the documentation site is switched on, which happens after the flip anyway. |
| **G8** CI green | ✅ Pass | CI is green on `dev` and on `main` in all three repos (server: 4 jobs, client, plugin). It runs on pull requests and on `main` only, plus by hand. `main` exists in all three repos and is the default branch. Not yet a required check (branch protection is set after the flip). |
| **G9** Release drafts | ⏳ Open | Release workflows and release-note drafts exist. Alpha tags (`server-v0.2.0-alpha`, `client-v0.1.0-alpha`, plugin tags) and the draft releases are created after the flip, so the workflows run on public repositories. |
| **G10** AI-generated assets | ✅ Pass | The OpenAI and Google terms were read by the owner (output rights go to the user, no attribution needed). The 29 PLC icons were checked against the Lucide set and are original. Every README, SECURITY.md, the docs site, the installer agreement, the editor's Help window and the phone app's settings panel now state that the software is AI-generated, provided without warranty or liability and used at the user's own risk. A trademark scan found only descriptive uses (Windows, Android, Microsoft namespaces, `notepad.exe` sample target). |

## Known issues to close before or right after the flip

- Plugins window: invalid plugin folders are not listed and "Install from Folder" accepts a folder without `plugin.json`
  (the Remove confirmation itself was fixed).
- On the development machine some `bin\` / `obj\` writes fail with "Access denied" (cause unknown; building elsewhere
  works). Confirm the builds pass in CI or on a clean machine.
- `MacroGrid.Plugin.Abstractions` name and "Macro Grid" product name: no active product found by web search; trademark
  registries were not searched (owner decision, non-commercial project).
- Local folders and repos were renamed from the old product name; old backup mirrors were deleted.

## Owner checklist (in order)

Done: the clean-PC installer test (G4), the SDK on nuget.org (G6), `main` in all three repositories, CI on `main`.

1. Optional before the flip: run both tutorials verbatim on a clean setup (G7). It can also wait until the documentation site is switched on.
2. Flip visibility in this order: server, client, plugin (Settings, General, Danger Zone, Change visibility).
3. Right after each repository is public: turn on **Private vulnerability reporting** (Settings, Code security), add branch protection on `main` with CI as a required check, set the description and topics, and switch on Actions Pages in the plugin repository (Settings, Pages, source GitHub Actions).
4. Tag the alpha releases (`server-v0.2.0-alpha`, `client-v0.1.0-alpha`, the plugin tags) and publish the draft releases with the installer and the APK attached (G9).
5. Right after the flip, lock down SDK publishing (these are not enforced on a private repo under the current plan,
   so they wait for the public repo):
   - **Important:** add a tag ruleset on the server repo for `sdk-v*` (restrict creations, updates and deletions;
     only the repository admin in the bypass list). Without it anyone with write access can publish to nuget.org
     by pushing a tag.
   - Turn on **Required reviewers** for the `nuget` environment (the owner as reviewer, "Prevent self-review" off),
     so every publish waits for an approval.
   - Ask nuget.org to reserve the `MacroGrid.` package ID prefix (mail account@nuget.org from the account address
     with the user name, `MacroGrid.Plugin.Abstractions` and the public project URL). It blocks look-alike package
     names and adds the verified mark.
