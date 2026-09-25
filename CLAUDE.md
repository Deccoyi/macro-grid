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

## Commits
- Conventional Commits (`type(scope): description`), always in English.

## Names
- Never mention third-party product or brand names in code, comments, docs or commits. Describe the pattern generically.

## Docs layout
- `docs/` is sorted by kind: `guides/` (how to work on the code), `ui/`, `design/` (built features), `plans/` (not built yet), `releases/`, and `agents/` (notes and proposals written by AI agents). The index is `docs/README.md`.
- Put a new document in the folder that fits (see the end of `docs/README.md`), never loose in `docs/`. Only the changelogs, `roadmap.md`, `architecture.md` and `README.md` live at its top.
