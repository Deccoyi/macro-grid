# Notes for AI agents

Working notes written by and for AI coding agents. They record what was done and why, and hold ideas that need the owner's decision before
anything changes. They are not user documentation and nothing here is a promise.

| Path | What it is |
|---|---|
| [refactor-notes.md](refactor-notes.md) | Log of the `refactor/cleanup` branch: what was restructured, with no change in behavior. |
| [dependabot-2026-09.md](dependabot-2026-09.md) | Review of the September 2026 dependency round, the follow-ups and what is left. |
| [proposals/](proposals/) | Ideas that change behavior or policy. **Nothing in here is decided.** The owner accepts one (then it is done and the file is deleted or moved to `guides/` or `design/`) or rejects it (the file is deleted). |

## Where the instructions for agents live

These are not in this folder because tools look for them at fixed places:

- [`CLAUDE.md`](../../CLAUDE.md) at the repository root: the project rules (language, changelogs, commits, names).
- [`.claude/skills/`](../../.claude/skills/): repeatable procedures, for example `commit-all`.
- [`../guides/engineering-guidelines.md`](../guides/engineering-guidelines.md): the coding rules an agent (or a person) must follow.

## Rules for notes in this folder

- Write in English, like everything else in the repository.
- A note names the date or the branch it belongs to, so a reader can tell how old it is.
- When a note's open items are all done, delete the note; the change is recorded in the changelogs and in git history.
