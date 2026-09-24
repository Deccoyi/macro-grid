# Pre-flip audit (task D.19)

Status of the ten gates from [open-source-plan.md](open-source-plan.md), with the evidence behind each one.
Audit date: 2026-09-24. Repos: `Deccoyi/macro-grid`, `macro-grid-client`, `macro-grid-plugin`.

**Verdict: ready to open as an alpha.** All gates that block opening are passed; the remaining items (G7 and the after-the-flip list) do not need to happen before the repositories go public. Nothing found so far blocks the flip.

**Update:** all three repositories are public now. The post-flip settings were read back through the GitHub API and are recorded in [Post-flip state](#post-flip-state-verified-through-the-github-api) below.

| Gate | Status | Evidence |
|---|---|---|
| **G1** Identity scrub | ✅ Pass | All commits in all three repos (78 / 33 / 28) have author and committer the project identity (Deccoyi + project mailbox). A search of every commit (metadata, messages, file contents) for the old employer name, both old personal addresses and the user-profile name finds nothing. Old backups were deleted; the repos were deleted and recreated on GitHub. |
| **G2** Community files | ✅ Pass (owner review) | Every repo has LICENSE (MIT), README, SECURITY.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue templates (3 / 3 / 4), a PR template, THIRD_PARTY_NOTICES.md and a `licenses/` folder (22 / 28 / 5). |
| **G3** No secrets / PII | ✅ Pass (grep only) | Re-run on 2026-09-24 over every commit of all three repos and over all current files: no employer, personal-name, user-profile-path, machine-address or company-network string; no known secret format (cloud keys, tokens, private keys); no key, keystore or `.env` file ever committed; the release key lives outside the repos. The dedicated project mailbox appears only in commit metadata (and in old file versions in history). No gitleaks run yet: add it to CI later. |
| **G4** Installer tested | ✅ Pass | Tested by the owner on a clean, domain-joined company PC on 2026-09-24: install, the user agreement page, editor opens (WebView2 data folder fixed), phone pairs and the mute toggle works, start with Windows after a restart, quit from the tray ends the process, uninstall (also while running) removes everything. Fixes found by that test: WebView2 data folder, plugins built against SDK 0.3.0 loading on 0.3.1, tray exit, uninstall while running. Unsigned installer: Windows shows an unknown-publisher warning. |
| **G5** Release APK on a device | ✅ Pass | Signed `MacroGrid-0.1.0.apk` (verified with `apksigner`), installed on a real phone, paired with the renamed server, live values and buttons work. Keystore backed up by the owner. Only a re-run against the final release server build is left. |
| **G6** SDK on NuGet | ✅ Pass | `MacroGrid.Plugin.Abstractions` 0.3.0 is live on nuget.org (Trusted Publishing, tag `sdk-v0.3.0`). A clean build of both plugins with an empty package cache restores it from nuget.org and passes; the plugin CI dropped the server checkout. The package's project links stay 404 for outsiders until the repos are public. |
| **G7** Tutorials verified | ⏳ Open (owner, not blocking) | Docs site builds with no dead links; both examples build and load in the real plugin manager; JS example and plugin install worked on the owner's setup; the C# example restores the published SDK. The tutorials still have to be followed verbatim on a clean setup; do it before the documentation site is switched on, which happens after the flip anyway. |
| **G8** CI green | ✅ Pass | CI is green on `dev` and on `main` in all three repos (server: 4 jobs, client, plugin). It runs on pull requests and on `main` only, plus by hand. `main` exists in all three repos and is the default branch. CI is now a required check on `main` through the `protect-main` ruleset (see Post-flip state). |
| **G9** Release drafts | ✅ Pass | Five pre-releases are published: `server-v0.2.0-alpha`, `client-v0.1.0`, `plugin-obs-v0.2.0`, `plugin-plc-icons-v0.1.1` and `plugin-hellojs-v0.1.0`. The plugin releases were built by the `Release plugin` workflow (all three runs succeeded). The client APK of `client-v0.1.0` was uploaded by hand; the first real run of the client `release.yml` is still to be watched. |
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

### Done

- The clean-PC installer test (G4), the SDK on nuget.org (G6, versions 0.3.0 and 0.3.1), `main` in all three repositories, CI on `main`.
- Visibility flipped to public in all three repositories.
- **Private vulnerability reporting** is on in all three repositories.
- **Branch protection** on `main` in all three repositories, with CI as a required check (`protect-main` ruleset).
- Repository descriptions and topics are set; Pages is switched on in the plugin repository (source: GitHub Actions) and the documentation site answers at https://deccoyi.github.io/macro-grid-plugin/.
- The five alpha pre-releases are published (G9).
- **SDK publishing lock-down, tag part:** the `protect-tags` ruleset blocks creating, updating and deleting tags for everyone except the repository admin, in all three repositories.
- Secret scanning, push protection and Dependabot alerts are on; pull requests from outside contributors need approval before workflows run.

### Still open

1. Optional: run both tutorials verbatim on a clean setup (G7). It can also wait until the documentation site is promoted.
2. Optional: turn on **Required reviewers** for the `nuget` environment of the server repository (the owner as reviewer, "Prevent self-review" off), so every publish waits for an approval. Today the environment only has a deployment branch policy; the tag ruleset already limits who can trigger a publish.
3. Optional: ask nuget.org to reserve the `MacroGrid.` package ID prefix (mail account@nuget.org from the account address with the user name, `MacroGrid.Plugin.Abstractions` and the public project URL). It blocks look-alike package names and adds the verified mark. This cannot be checked through an API here; confirm it on nuget.org.
4. Open each repository in a private window without signing in and check that the README and the release pages render.
5. Watch the first documentation push after the Actions bump (see [dependabot-2026-09.md](dependabot-2026-09.md)) and the first real client release run.

## Post-flip state (verified through the GitHub API)

Read on 2026-09-24 with `gh api`; nothing here is assumed.

| Setting | server | client | plugin |
|---|---|---|---|
| Visibility / default branch | public / `main` | public / `main` | public / `main` |
| Ruleset `protect-main` (branch `main`, active) | yes | yes | yes |
| Rules in it | deletion, non-fast-forward, pull request (1 approval), required status checks | same | same |
| Required checks | Server (build and test), editor, webclient and packages/renderer (typecheck, build, test) | Typecheck, build and test | build-and-test |
| Ruleset `protect-tags` (all tags, active) | yes | yes | yes |
| Rules in it | creation, update, deletion, non-fast-forward | same | same |
| Bypass list of both rulesets | repository admin role only | same | same |
| Secret scanning and push protection | enabled | enabled | enabled |
| Dependabot alerts and security updates | enabled | enabled | enabled |
| Private vulnerability reporting | enabled | enabled | enabled |
| Approval for workflows from outside contributors | all external contributors | all external contributors | all external contributors |
| Discussions | off | off | off |

- Pre-releases: server `server-v0.2.0-alpha`, client `client-v0.1.0`, plugin `plugin-obs-v0.2.0`, `plugin-plc-icons-v0.1.1`, `plugin-hellojs-v0.1.0`.
- Pages: the plugin repository builds the site with the `Documentation site` workflow (build type `workflow`); the last run on `main` succeeded and the site returns HTTP 200.
- `nuget` environment (server repository): only a deployment branch policy, no required reviewer.
- NuGet: `MacroGrid.Plugin.Abstractions` 0.3.0 and 0.3.1 are listed; the server repository has the tags `sdk-v0.3.0` and `sdk-v0.3.1`.
