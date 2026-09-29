# Project rules

## Language
- Everything in the project is written in **English**: code, identifiers (variables, functions, types, components, files), comments, documentation, changelogs, log messages and commit messages. Do not use Turkish in any of them.
- User-facing UI text is the only exception, and only through the i18n files (for example `tr.ts` / `en.ts`), never hard-coded in components.
- Existing Turkish text is migrated gradually: when you come across Turkish in code, comments, identifiers or docs while working or reviewing, translate it on the way (only the part you are already touching). Do not start a dedicated bulk-translation pass or a translation-only agent.
- The conversation with the user is in Turkish. That does not affect anything written into the project.

## Changelogs
- Keep two changelogs in `docs/`: `CHANGELOG.md` (short, public, for non-developers) and `CHANGELOG-developer.md` (only what developers need and git history cannot carry).
- `CHANGELOG.md` uses short, simple sentences, one line per change ("New / Changed / Fixed"). No code, file or API names. Leave out small bug fixes and stability or internal improvements.
- `CHANGELOG-developer.md` records only: changes to the plugin SDK or the WebSocket protocol, breaking or incompatible changes, migrations, and anything a plugin author, integrator or maintainer has to do differently. Everything else (how a feature was built, refactors, internal details, small fixes) goes in the commit message and the pull request description, not in this file. Entries already there stay as they are.
- When a change is finished, update `CHANGELOG.md` under `[Unreleased]`, and `CHANGELOG-developer.md` only if the change is one of the kinds above. See `docs/guides/versioning.md` and the `commit-all` skill.

## Versions, releases and signing
- Any version, release, tag or signing work (server, SDK, phone app or plugins): read `docs/guides/release.md` first (the one release guide, with the tag table, the order and the signing keys), then `docs/guides/versioning.md` for the rules. The other repositories link here; do not copy their content into other documents.
- The server and the SDK share one version, set only in `<Version>` in `Directory.Build.props`. Never write a version into code or a project file.
- Tags and plugin or APK releases are outward-facing: ask the owner before each one. The SDK is no longer published to NuGet.

## Commits
- Conventional Commits (`type(scope): description`), always in English.

## Names
- Never mention third-party product or brand names in code, comments, docs or commits. Describe the pattern generically.

## Docs layout
- `docs/` is sorted by kind: `guides/` (how to work on the code), `ui/`, `design/` (built features), `plans/` (not built yet), and the git-ignored `agents/`, `done/` and `releases/` (local working notes, never published). The index is `docs/README.md`.
- Put a new document in the folder that fits (see the end of `docs/README.md`), never loose in `docs/`. Only the changelogs, `roadmap.md`, `architecture.md` and `README.md` live at its top.

## Local notes: macro-grid-library
Working notes are NOT in the public repos. They live in the private repo `macro-grid-library` (sibling folder of `macro-grid`, `macro-grid-client`, `macro-grid-plugin`) and are linked into each repo with directory junctions:
- `macro-grid/docs/agents/` (with `hidden/` and `proposals/`), `docs/done/`, `docs/releases/` -> `macro-grid-library/macro-grid/{agents,done,releases}/`
- `macro-grid-client/docs/agents/` -> `macro-grid-library/macro-grid-client/agents/`
- `macro-grid-plugin/docs/agents/`, `docs/done/` -> `macro-grid-library/macro-grid-plugin/{agents,done}/`

Rules:
- These paths are git-ignored. Never `git add -f` them, never link to them from public docs, never copy their content into public files.
- Session handoffs, refactor logs, unapproved proposals, finished plans and maintainer-only docs (triage answers, internal plans, mockups) go there. Public docs (`docs/design/`, `plans/`, `guides/`, `ui/`, `roadmap.md`, changelogs) stay in the public repos.
- Edit through the junction path (`docs/agents/...`); the change lands in `macro-grid-library`. Commit and push it THERE (`cd ../macro-grid-library`), not in the public repo. The user syncs two PCs through that repo.
- If a `docs/agents` path is missing or is a real folder, run `macro-grid-library/scripts/link.ps1` (`powershell -ExecutionPolicy Bypass -File ...`); move files out of a real folder first.
- Trap: if a file under a junction path is ever git-tracked, a rebase/checkout deletes it from `macro-grid-library` too. These paths are untracked now; before checking out an old commit or a branch that still tracks them, remove the junction (`rmdir docsgents`, this does not delete files). In `macro-grid-library`, run `git status` before `git add -A` and stop if it shows mass deletions; recover with `git checkout <old-commit> -- <path>`. Always `git pull` there before editing.
