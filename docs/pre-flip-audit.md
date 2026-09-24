# Pre-flip audit (task D.19)

Status of the ten gates from [open-source-plan.md](open-source-plan.md), with the evidence behind each one.
Audit date: 2026-09-24. Repos: `Deccoyi/macro-grid`, `macro-grid-client`, `macro-grid-plugin` (all private).

**Verdict: not ready to flip yet.** G4, G7, G8 and G9 are open and depend on the owner (clean-PC test,
running the tutorials, enabling Actions, tagging releases). Nothing found so far blocks the flip once those are done.

| Gate | Status | Evidence |
|---|---|---|
| **G1** Identity scrub | ✅ Pass | All commits in all three repos (78 / 33 / 28) have author and committer `Deccoyi <macrogrid.app@gmail.com>`. A search of every commit (metadata, messages, file contents) for the old employer name, both old personal addresses and the user-profile name finds nothing. Old backups were deleted; the repos were deleted and recreated on GitHub. |
| **G2** Community files | ✅ Pass (owner review) | Every repo has LICENSE (MIT), README, SECURITY.md, CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue templates (3 / 3 / 4), a PR template, THIRD_PARTY_NOTICES.md and a `licenses/` folder (22 / 28 / 5). |
| **G3** No secrets / PII | ✅ Pass (grep only) | Known secret formats (cloud keys, tokens, private keys) match nothing in the full history. No `.jks`, `.keystore`, `.env` or `keystore.properties` file was ever committed; the release key lives outside the repos. Password-like matches are only the property *names* in `android/app/build.gradle`. No gitleaks run yet: add it to CI. |
| **G4** Installer tested | ⏳ Open (owner) | `MacroGrid-Setup-0.2.0.exe` compiled for the first time (Inno Setup 6.7.3). Install / upgrade / uninstall on a clean Windows PC not yet done. Known: `PrivilegesRequired=admin` with per-user (HKCU) autostart entry; installer is unsigned. |
| **G5** Release APK on a device | ✅ Pass | Signed `MacroGrid-0.1.0.apk` (verified with `apksigner`), installed on a real phone, paired with the renamed server, live values and buttons work. Keystore backed up by the owner. Only a re-run against the final release server build is left. |
| **G6** SDK on NuGet | ✅ Pass | `MacroGrid.Plugin.Abstractions` 0.3.0 is live on nuget.org (Trusted Publishing, tag `sdk-v0.3.0`). A clean build of both plugins with an empty package cache restores it from nuget.org and passes; the plugin CI dropped the server checkout. The package's project links stay 404 for outsiders until the repos are public. |
| **G7** Tutorials verified | ⏳ Open (owner) | Docs site builds with no dead links; both examples build and load in the real plugin manager; JS example and plugin install worked on the owner's setup. The tutorials still have to be followed verbatim on a clean setup. |
| **G8** CI green | ⏳ Open (owner) | CI is green on `dev` in all three repos (server: 4 jobs, client, plugin). It runs on pull requests and on `main` only. Open: create `main` in the plugin repo, merge `dev` into `main` in all three, check the first runs on `main`, make CI a required check. |
| **G9** Release drafts | ⏳ Open (owner) | Release workflows and release-note drafts exist. Tags (`server-v0.2.0-alpha`, `client-v0.1.0`, plugin tags) and the drafts are created by the owner after G4–G8. |
| **G10** AI-generated assets | ✅ Pass | The OpenAI and Google terms were read by the owner (output rights go to the user, no attribution needed). The 29 PLC icons were checked against the Lucide set and are original; READMEs say "AI-generated"; no third-party logos. A trademark scan found only descriptive uses (Windows, Android, Microsoft namespaces, `notepad.exe` sample target). |

## Known issues to close before or right after the flip

- Plugins window: invalid plugin folders are not listed and "Install from Folder" accepts a folder without `plugin.json`
  (the Remove confirmation itself was fixed).
- On the development machine some `bin\` / `obj\` writes fail with "Access denied" (cause unknown; building elsewhere
  works). Confirm the builds pass in CI or on a clean machine.
- `MacroGrid.Plugin.Abstractions` name and "Macro Grid" product name: no active product found by web search; trademark
  registries were not searched (owner decision, non-commercial project).
- Local folders and repos were renamed from the old product name; old backup mirrors were deleted.

## Owner checklist (in order)

1. Clean-PC installer test (G4) and fix anything it shows.
2. Publish the SDK to nuget.org (G6), then run both tutorials on a clean setup (G7).
3. GitHub: enable Actions, create `main` in the plugin repo, merge `dev` into `main` in the server and client repos, enable private vulnerability reporting, set descriptions and topics, and check that CI is green on `main` (G8).
4. Tag the releases and create the release drafts (G9).
5. Flip visibility in this order: server, client, plugin; then enable Pages and publish the releases.
6. Right after the flip, lock down SDK publishing (these are not enforced on a private repo under the current plan,
   so they wait for the public repo):
   - **Important:** add a tag ruleset on the server repo for `sdk-v*` (restrict creations, updates and deletions;
     only the repository admin in the bypass list). Without it anyone with write access can publish to nuget.org
     by pushing a tag.
   - Turn on **Required reviewers** for the `nuget` environment (the owner as reviewer, "Prevent self-review" off),
     so every publish waits for an approval.
   - Ask nuget.org to reserve the `MacroGrid.` package ID prefix (mail account@nuget.org from the account address
     with the user name, `MacroGrid.Plugin.Abstractions` and the public project URL). It blocks look-alike package
     names and adds the verified mark.
