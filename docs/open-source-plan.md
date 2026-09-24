# Open-sourcing the three Macro Grid repos

## Context

`macro-grid` (server + editor), `macro-grid-client` (Android client) and `macro-grid-plugin` (plugins + authoring
docs) are private GitHub repos under `Deccoyi/`. They will all go public (MIT). The plugin repo also needs a GitHub Pages
documentation site (getting started, hello-world plugin tutorials). This plan decides WHEN to flip each repo public and
splits the work between Claude and the owner. Final home of this file after approval:
`macro-grid/docs/open-source-plan.md` (English, per project rules).

## Findings from the current state (2026-09-24)

- ✅ **Project renamed to Macro Grid (2026-09-24)** in all three repos (code, docs, package ids, `macrogrid://` pairing scheme, `com.macrogrid.client`), repos recreated as `Deccoyi/macro-grid`, `macro-grid-client`, `macro-grid-plugin` (private). All commits are authored by `Deccoyi <macrogrid.app@gmail.com>` (the project mailbox; history rewritten again for this). Older sections below may still say "Macro Station" / `macro-station-plugin`; read them as the new names. Open: rename the local folders, close the running server exe that locks old build folders, run the plugin builds against the renamed SDK, run trademark searches for "Macro Grid".

- Server `0.2.0`, Plugin SDK `0.3.0`, OBS plugin `0.2.0`, PLC Icons `0.1.0`. Client Stage 5 done and tested on a real device.
- All three repos: `dev` is the working branch. `macro-grid` and `-client` also have `main`. **`macro-grid-plugin` has only `dev`** (its `origin/HEAD` points at `dev`).
- **No GitHub Actions / CI** anywhere. No `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue/PR templates.
- **`macro-grid-plugin` has no `LICENSE` file.**
- The plugin repo's C# projects reference the SDK by relative path (`..\..\..\macro-grid\src\MacroGrid.Plugin.Abstractions`). A public plugin author cannot build that; the SDK is not on NuGet. This blocks the "hello world C# plugin" tutorial.
- `docs/release.md`: the installer script (`MacroGrid.iss`) has **never been compiled or tested on a clean PC**. No signed release APK flow verified. No GitHub Release exists.
- README security note in the server repo is stale ("no pairing/authentication layer") while PIN + token pairing exists.
- ✅ Git history (**scrubbed locally on 2026-09-24**): 108 commits across 3 repos (61 server, 29 client, 18 plugin). All authors and committers are now `Deccoyi <macrogrid.app@gmail.com>`; the employer domain was removed from `docs/agent-notes.md` in every past commit. A trademark scan (Photoshop, Notepad, Adobe, Spotify, Discord, Steam, Stream Deck etc.) found only nominative/technical uses (Windows, Android, Microsoft namespaces, `notepad.exe` sample target, `@adobe/css-tools` dependency); the one commit message naming a media app was rewritten. A scan of all commits (metadata, messages, file contents) finds no employer name, old personal email, real name or user-profile name. Repo-local git config uses the same identity. The remote (GitHub) still holds the OLD history until O2 step 3 is done. Repos are small (<3 MB), no secrets / keystores / `.env` found in tracked files by grep.
- An employer network/domain reference sits in `docs/agent-notes.md` of the server and client repos, in the current files and in many past commits. A machine IP sits in `macro-grid-plugin/docs/handoff-2026-09-23.md`. `macro-grid/docs/plan.md` and `docs/agent-notes.md` are largely Turkish internal notes with stale "empty folder / will do" text.
- ✅ Corrected by evidence (2026-09-24): all 29 PLC icons are original (no path data shared with any Lucide icon, different 40x30 ladder-logic format), so no Lucide licence is needed in the plugin repo and the README says all 29 are AI-generated. Earlier assumption, now obsolete: only about 20-25 icons are original AI-generated art; the rest come from the Lucide icon library (ISC licence; its licence text and copyright notice must ship with the pack and be listed in `THIRD_PARTY_NOTICES.md`). The pack README states neither. App icons are AI-generated too. Disclosure needed, see O3.
- `macro-grid/THIRD_PARTY_NOTICES.md` covers only direct deps and does not list Jint, the plugin repo's deps, or Android/Capacitor deps (client has its own file, not reviewed here).
- Uncommitted work exists right now (`.gitignore` edits, staged `_backup/` deletions in the server repo); another session is doing the README/docs cleanup.

## When to open: the gate

Open **all three repos on the same day**, when the project reaches **"Public Alpha 0.x"**, not at 1.0 and not now.
Reason: the three are one product. The client is useless without the server, the plugin repo cannot be built without the
SDK, and Pages on a private repo needs a paid plan, so the docs site can only be shown once public anyway. Opening
one-by-one only creates broken links between them. Do not wait for 1.0; label everything "alpha" and keep the existing
AI-generated disclaimer.

Flip to public only when **every** item is true:

| # | Gate | Who verifies |
|---|---|---|
| G1 | Identity scrub done (O2): history rewritten (✅ done locally), GitHub repos recreated (✅ done 2026-09-24, private), scan finds no employer name, old email or real name anywhere in tree or history | Claude scans, owner executes |
| G2 | Every repo has LICENSE, README with correct status, SECURITY.md, CONTRIBUTING.md, issue templates, third-party notices | Claude prepares, owner reviews |
| G3 | No secrets/PII/company references in tracked files or history (scan clean) | Claude scans, owner confirms |
| G4 | Server installer compiled and install/upgrade/uninstall tested on a clean Windows PC | Owner |
| G5 | Client release APK built and tested on a real device against the release server | Owner |
| G6 | Plugin SDK `0.3.0` published to NuGet (or an equally public feed), plugin projects build from a clean clone without sibling repos | Claude prepares, owner publishes |
| G7 | Docs site builds locally and both tutorials (JS hello world, C# hello world) were followed verbatim on a clean setup and work | Claude writes, owner runs them |
| G8 | CI green on all three (build + tests) on `main` | Claude writes, owner enables |
| G9 | First tagged release `v0.2.0-alpha` (server + installer) and client `v0.x` APK ready as GitHub Release drafts | Owner publishes |
| G10 | AI-generated icons/logos disclosed, generator terms checked, no brand look-alikes (O3) | Owner, Claude assists |

Flip order on the day (minutes apart): server, client, plugin, then enable Pages, publish releases, verify links.

## Claude's tasks (all done while repos are still private, no push/visibility changes without explicit go-ahead)

**A. Repo hygiene (all three)**
1. Add missing `LICENSE` (MIT, same as others) to `macro-grid-plugin`; per-plugin license note where needed.
2. Add `SECURITY.md` (private reporting via GitHub advisories, LAN-only threat model, no-warranty note), `CONTRIBUTING.md` (build/test steps, Conventional Commits, English only, two-changelog rule), `CODE_OF_CONDUCT.md`, `.github/ISSUE_TEMPLATE/*` and `PULL_REQUEST_TEMPLATE.md`.
3. Make `main` the default and `dev` the integration branch in `macro-grid-plugin` (create `main`); document the branching model in CONTRIBUTING.
4. README pass: fix stale security note, add badges (license, alpha status), screenshots placeholders, install instructions that point to Releases, links between the three repos.
5. Sweep tracked docs for personal/company data (employer domain, machine IPs, `C:\Users\...`); remove or generalize in the current tree first, then prepare the history rewrite described in O2 (mailmap + replace-text, run on mirror clones only). Move internal handoff/agent notes that are useless to outsiders into `docs/internal/` or delete stale ones (e.g. `handoff-2026-09-23.md`).
6. Translate remaining Turkish in public-facing docs (README, `plugin-authoring.md`, `plan.md` header, `versioning.md`) to English, per project language rule; keep Turkish only in UI strings.
7. ✅ (2026-09-24, needs owner review of the manual-check items) Complete `THIRD_PARTY_NOTICES.md` for each repo by reading csproj / package.json / gradle files (Jint, Capacitor, MLKit, etc.); no guessing. Result: each repo has an index `THIRD_PARTY_NOTICES.md` plus `licenses/<library>/` folders with the original license texts (server 22, client 28, plugins: build/test-only deps). Visible via a README section; server publish script and installer, the client APK web assets (`dist/`) and each plugin's build output carry the notices. Proprietary Google ML Kit / Play services terms are linked, not copied. Open: in-app "licenses" screens (server help window, client settings) are suggestions only.
8. ✅ (identity + brand part done; secrets part still open) Re-run secret/PII scan (tracked files and full history, including deleted `_backup/` content) and report.

**B. Build and CI**
9. `.github/workflows/ci.yml` per repo: server (`dotnet build/test` on windows-latest, editor `npm ci && npm run build && npm test`), client (`npm ci`, build, tests, optional debug APK), plugin (build + OBS tests).
10. `release.yml` for server (runs `scripts/publish.ps1`, uploads zip; installer step behind a flag since Inno Setup must be installed) and for client (unsigned/debug or signed-if-secrets APK).
11. Prepare NuGet packaging of `MacroGrid.Plugin.Abstractions` (`PackageId`, metadata, README, symbol package, `Version` 0.3.0) and switch plugin csproj files to a `PackageReference` with a documented local-feed/ProjectReference fallback for developers working on both repos. The owner does the actual `nuget push`.
12. Enable Dependabot config (`dependabot.yml`) for nuget, npm, gradle, github-actions.

**C. Documentation site (in `macro-grid-plugin`, deployed with GitHub Pages)**
13. Tool: VitePress (Node/Vite already used in the project, Markdown-native, built-in search, dark mode). Site source in `macro-grid-plugin/website/`; deployed by a `pages.yml` workflow (upload-pages-artifact + deploy-pages). Base path `/macro-grid-plugin/` until a custom domain exists.
14. Content structure:
    - Introduction: what Macro Grid is, architecture (server, client, plugins), alpha status and disclaimer
    - Getting started: install server, pair phone (PIN/QR), first profile, first button
    - Plugin basics: folder layout, `plugin.json` reference, permissions, SDK/server compatibility, versioning
    - Tutorial 1, JS hello world (from `HelloJs/`): variable + action, install, hot reload
    - Tutorial 2, C# hello world: new project, NuGet reference, `IPlugin`, action provider, settings form (`SettingField`), status item, packaging and install
    - Tutorial 3, showing live data: variables and formatting in widget text
    - Guides: settings pages, icon packs (PLC Icons as the worked example), OBS plugin as a real-world example, debugging/logs, publishing your plugin
    - Reference: manifest schema, JS `host` API, C# SDK interfaces, permissions, changelog links
15. Reuse and convert existing `docs/plugin-authoring.md` (281 lines) and `agent-and-repo-rules.md`; do not duplicate, make the site the single source and leave a short pointer file. Every code sample is taken from the real repo code (HelloJs, OBS, PLCIcons) and each tutorial project is committed under `examples/` and built in CI so samples cannot rot.
16. Verify tutorials myself by executing them from a scratch directory (JS one against a running server if available; C# one with `dotnet build` against the packed SDK), then hand the owner a checklist for the on-device run.

**D. Release prep**
17. Write `docs/open-source-plan.md` (this file, English) and a `docs/release.md` update (release checklist, versioning, tag naming `server-v0.2.0`, `client-v0.x`, `plugin-obs-v0.2.0` style).
18. Draft GitHub Release notes from the public `CHANGELOG.md` files.
19. Final pre-flip audit report: gate table with pass/fail evidence.

## Owner's tasks (things only the owner can do)

- **O1 Decide the licence/branding facts:** confirm MIT for all three, confirm the copyright holder line (handle vs real name), confirm the project name is free to use (no trademark clash), and whether a contact email for SECURITY.md/conduct reports is wanted (a new project mailbox, not a personal or employer one).
- **O2 Identity scrub (decided: full scrub, history is rewritten).** Nothing that names the employer or the old personal email or real name may remain in any repo, commit metadata or file, in any past commit.
  1. ✅ (Decided) Commits use the project mailbox `macrogrid.app@gmail.com` directly (not the noreply address). Add it to the GitHub account, tick "Block command line pushes that expose my email" only if the noreply variant is preferred later; the mailbox is public in every commit, so keep it a dedicated project address. Update the global git config on every machine that commits (a personal/employer email is still in the global config on the dev machine).
  2. ✅ **DONE (local).** History rewritten with `git filter-branch` (git-filter-repo is not installed) on fresh clones, author/committer forced to the identity above, employer domain stripped from `docs/agent-notes.md`; result applied to the working repos (branches `dev`/`main`, old remote-tracking refs, reflogs and unreachable objects removed). Untouched backup mirrors with the OLD history are in the session scratchpad (`bak/`); delete them after the flip.
  3. ✅ **DONE (2026-09-24).** Owner deleted and recreated the three private repos; the rewritten history was pushed (server and client: `dev` + `main`, plugin: `dev`). Original text: Owner reviews the result, then, instead of force-pushing over the old private repos, **delete the three GitHub repos and recreate them empty and private, then push the rewritten history**. Force-push alone can leave the old commits reachable by SHA on GitHub's side. Update `origin` in the local clones.
  4. Old local clones and the backup mirrors (they contain the old identity) are kept off any public place and deleted after the flip. Also check that the Windows user profile name is not present in tracked files (scan already clean for `C:\Users\`).
  5. The "author" of AI-assisted commits keeps the `Co-Authored-By` trailer; those carry no personal data.
- **O3 Legal/asset checks:**
  - PLC Icons: the ~20-25 original icons were AI-generated; the rest are Lucide (ISC). Not a blocker, but: (a) the pack README and `THIRD_PARTY_NOTICES.md` must say which icons are AI-generated (provided under the repo licence "as is") and which are Lucide, and ship the ISC licence text, (b) copyright in purely AI-generated output is uncertain in many jurisdictions, so the MIT grant may be weaker than for human work; acceptable here, (c) owner reads the generator's current terms once and confirms redistribution under an open licence is allowed, (d) spot check that no icon reproduces a real brand logo or trademark. Claude documents which files are original vs Lucide by comparing against the Lucide package, so the split comes from evidence.
  - Provenance note (owner, 2026-09-24): the 29 PLC icons were generated with Gemini. Public docs only say "AI-generated"; the generator name, prompts and generation dates go into the owner's private provenance note. Owner reads Google's current generative-AI terms once (output ownership, redistribution under an open licence, any attribution or watermark rule) and confirms; if the terms forbid open licensing, the icons are removed from the pack before the flip.
  - App icons / logos generated with the free plan of a chat assistant's image tool: generally not a problem. The provider's terms assign output rights to the user and allow commercial use, free plan included; the same caveats as above apply (weaker copyright, no exclusivity, generated art can resemble existing art). Owner confirms the terms in force today, keeps the prompt and generation date in a private note as provenance, and checks the image contains no third-party logo, text or recognisable character. Describe them in the README as "AI-generated".
  - Confirm no bundled fonts, screenshots or sounds have unclear licences, and that no code came from a private/company source.
- **O4 Clean-machine tests:** compile the installer (install Inno Setup 6), run install / upgrade / uninstall on a clean Windows PC; build and sign the release APK (create the keystore, keep it out of git, back it up), install it on a real phone and pair against the release build.
- **O5 Publish packages:** create a nuget.org account/API key and push `MacroGrid.Plugin.Abstractions` `0.3.0`. Credentials stay with the owner.
- **O6 GitHub settings (after flip):** set repo descriptions/topics, default branch `main`, branch protection (require CI), enable Discussions, private vulnerability reporting, Dependabot alerts, secret scanning + push protection, Pages source = GitHub Actions; add repo secrets (signing keystore) if release CI should sign. Optional: social preview image, custom domain.
- **O7 The flip:** Settings > Danger Zone > Change visibility for each repo, in the order above, then publish the prepared release drafts.
- **O8 After the flip:** watch first issues, decide response policy (alpha, best-effort), announce (optional).

## Timeline (suggested)

1. Claude: tasks A, B, C, D (private repos, commits on `dev`): the bulk.
2. Owner in parallel: O1, O2 decision, O3, O4.
3. Gate review with the audit report (D19), owner says go.
4. Same day: O2 execution (if chosen) then O7, O5 before the flip if NuGet tutorial depends on it.

## Verification

- Secret/PII scan clean on tree and history (`git grep`, `git log -p` search, optionally gitleaks in CI).
- `dotnet build` / `dotnet test` / `npm ci && npm test` / plugin builds pass locally and in CI from a fresh clone (no sibling repo present).
- `npm run docs:build` succeeds; a link checker passes on the built site; example projects compile in CI.
- Both tutorials executed verbatim end to end.
- After flip: open each repo unauthenticated (private window), check README renders, Pages URL loads, Release assets download, issue templates appear.

## Open questions to settle with the owner

1. (Settled) Full identity scrub with history rewrite and recreated repos. Remaining: which address goes into the commits (recommended: the GitHub noreply address).
2. (Settled) Docs site uses the default `<handle>.github.io/macro-grid-plugin/` address.
3. Contact email for security and conduct reports.
