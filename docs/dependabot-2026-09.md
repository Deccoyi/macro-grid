# Dependabot review, 2026-09-24

Status: **all 14 bot pull requests (macro-grid 8, macro-grid-client 5, macro-grid-plugin 1) were applied together on `dev`, pushed and closed.** No bot pull request is open. The follow-ups below were done in a second session on the same day; what is left is listed at the end.

## Done

### Dependency updates

| Repo | Commit | Content |
|---|---|---|
| macro-grid | `fd59b49` | vite 8, plugin-react 6, lucide-react 1.x, React 19, TypeScript 7 (editor, webclient, renderer), vitest 5 + jsdom 30 + jest-dom 7 in the renderer, Actions (checkout v7, setup-dotnet v6, setup-node v7), NuGet (coverlet 10, Test.Sdk 18, xunit.runner 4) |
| macro-grid | `7ca4e02`, `5dbbbdf` | this note, and the v0.2.0-alpha release notes synced with the published release |
| macro-grid-client | `0a43e85` | React 19, TypeScript 7, vite 8, plugin-react 6, vitest 5, jsdom 30, Actions (checkout v7, setup-node v7, setup-java v6, setup-android v4, upload-artifact v7) |
| macro-grid-client | `fad328f` | google-services 4.5.0 |
| macro-grid-client | `31b5356` | Gradle wrapper 8.14.3 to **9.5.1** |
| macro-grid-plugin | `9b80e15` | Actions only (checkout v7, setup-dotnet v6, setup-node v7, configure-pages v6, upload-pages-artifact v5, deploy-pages v5) |

Code fixes needed by React 19 and TypeScript 7:
- `editor/src/panels/actionForms/forms.tsx`: `import type { JSX } from "react"` (the global `JSX` namespace is gone).
- `editor/src/vite-env.d.ts` and the client's `src/vite-env.d.ts`: `/// <reference types="vite/client" />` (CSS side-effect imports, TypeScript 7).

### Follow-ups (second session)

| Step | Result |
|---|---|
| Pull `dev`, `npm ci`, run everything in fresh clones (`C:\dev`) | renderer 26 tests, editor and webclient typecheck + build, `dotnet test` 206, client typecheck + build + 26 tests: all pass |
| Open the editor and the deck by hand after the React 19 move | No hooks error and no console error in the deck. The editor's profile list, page list, widget picker and property panel work; the icon picker opens (lucide 1.x names fine, checked by the owner). The desktop app window needs `wwwroot/editor` and `wwwroot/deck`, which only `scripts/publish.ps1` produces; without them the window shows a 404 (expected in a dev checkout) |
| Renderer peer range | macro-grid `75cca1e`: `react` and `react-dom` peer ranges moved from `^18.3.0` to `^19.0.0` |
| `gradlew` was stored as mode 100644, so "Assemble debug APK" failed with exit 126 | macro-grid-client `d176e78` sets mode 100755. `ci.yml` run by hand with `build_apk=true` (run 36029157697) passed, including "Assemble debug APK" and the artifact upload. `release.yml` uses the same `./gradlew`, so it is fixed too |
| Dependabot read `main` (old versions) and would reopen the same pull requests | macro-grid `7a88e5e` and macro-grid-plugin `363cfb5` add `target-branch: dev` to every block (the client already had it) |
| Client `dependabot.yml`: `open-pull-requests-limit` written twice per block, long branch names | macro-grid-client `4159fcd` keeps the limit of 2 and renames the group `all-updates` to `deps`; the same rename is in the other two repos to keep branch names short |
| Pre-flip audit and open-source plan out of date | Both documents now record the post-flip GitHub settings, read back with `gh api` (rulesets, secret scanning, fork pull request approval, private vulnerability reporting, five pre-releases, Pages) and list what is still open |

## Not taken

- **Gradle 9.7.1** (bot suggestion): does not work with AGP 8.13 (it uses the `InternalProblems` API that was removed in Gradle 9.6). 9.5.1 is the newest version that builds. Try 9.7+ again together with an AGP 9 upgrade, which is a separate piece of work.
- The single vite and vitest pull requests (macro-grid #8, #9, #10; client #6): broken on their own or already included in the combined update, closed.

## Left

- **Watch the plugin Pages deploy** (configure-pages v6, upload-pages-artifact v5, deploy-pages v5). The new action versions are on `dev` only; the last deploy on `main` (workflow run 36011602591) used the old ones. It runs for the first time on the next docs push to `main`.
- **Watch the first real client release** with `release.yml`. `client-v0.1.0` was released with an APK uploaded by hand. No repository has any secret, so the workflow builds without signing; the signed APK is still built locally. The Android SDK step (setup-android v4) passed in the manual CI run.
- **Dev to main:** `dev` carries all of the changes above; `main` is protected (pull request with 1 approval and required checks). Merging is the owner's decision and is not done automatically.
- **AGP 9 upgrade** (to allow Gradle 9.7+), only if wanted.
- Tutorials are still untested on a clean setup (G7).

## For the owner (low priority, no new work)

- Open the three repositories in a private window without signing in and check that the README and release pages look right. Docs site: https://deccoyi.github.io/macro-grid-plugin/
- nuget.org `MacroGrid.` prefix reservation: optional for an alpha; it blocks look-alike packages and gives the verified mark.
- Required reviewer on the GitHub `nuget` environment: optional; the tag ruleset already limits who can publish.

## Notes

- The installer and the APK are unsigned, so SmartScreen and Play Protect warn. A certificate will not be bought.
- On Windows a `dotnet build` under Desktop can fail with "Access denied" (antivirus or Controlled Folder Access); build outside that folder. For long branch names set `git config core.longpaths true`.
- Your npm config may have `legacy-peer-deps=true`. Then vitest 5 does not get its `vite` peer and the renderer tests fail with `ERR_MODULE_NOT_FOUND`; run `npm ci --legacy-peer-deps=false`.
- If the bot opens new pull requests next month, use the same approach: test the groups together on `dev`. Freezing the React, TypeScript and vite majors with `ignore` is also an option.
