# Project rules

## Language
- Everything in the project is written in **English**: code, identifiers (variables, functions, types, components, files), comments, documentation, changelogs, log messages and commit messages. Do not use Turkish in any of them.
- User-facing UI text is the only exception, and only through the i18n files (for example `tr.ts` / `en.ts`), never hard-coded in components.
- Existing Turkish text is migrated gradually: when you come across Turkish in code, comments, identifiers or docs while working or reviewing, translate it on the way (only the part you are already touching). Do not start a dedicated bulk-translation pass or a translation-only agent.
- The conversation with the user is in Turkish. That does not affect anything written into the project.

## Changelogs
- Keep two changelogs in `docs/`: `CHANGELOG-developer.md` (detailed, technical) and `CHANGELOG.md` (short, public, for non-developers).
- `CHANGELOG.md` uses short, simple sentences, one line per change ("New / Changed / Fixed"). No code, file or API names. Leave out small bug fixes and stability or internal improvements.
- Update both under `[Unreleased]` when a change is finished. See `docs/versioning.md` and the `commit-all` skill.

## Commits
- Conventional Commits (`type(scope): description`), always in English.

## Names
- Never mention third-party product or brand names in code, comments, docs or commits. Describe the pattern generically.