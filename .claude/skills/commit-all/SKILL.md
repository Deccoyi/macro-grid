---
name: commit-all
description: Review everything that has changed since the last commit in a git repo, update that project's docs (CHANGELOG.md and any plan/roadmap markdown files under docs/) to record what was finished, then commit the work as one or more Conventional Commits (conventionalcommits.org), grouped by feature/topic. Use this whenever the user says "commit-all", asks to "commit everything", wants their pending work committed with proper changelog/plan bookkeeping, or asks for a clean set of conventional commits covering the current diff. Works in any git repository (this is a general-purpose, project-agnostic skill, not tied to one repo). Never pushes — it only commits, and always in English regardless of the conversation language.
---

# commit-all

Turn everything that changed since the last commit into a clean, reviewable set of
Conventional Commits, while keeping each project's own docs (changelog + plan/roadmap
files) honest about what actually got finished.

This skill is project-agnostic: it works in whichever git repository it's invoked in
(for example `macro-grid`, `macro-grid-client`, `macro-grid-plugin`, or any other repo on this machine).
Don't hardcode assumptions about repo layout beyond "there's a `.git` and probably a
`docs/` folder" — discover the rest each time.

## Why this order matters

Docs describe *finished* work, so they must be written before the commit that finishes
it, not after — otherwise the changelog entry and the code that satisfies it end up in
different commits (or the changelog never gets updated at all). Updating docs first also
forces you to actually understand what each change accomplishes before grouping it into
a commit, which is what makes the grouping and the commit messages accurate instead of
generic.

## Step 0 — Check the branch

This skill's commits (and any pushes done afterward, separately) target the `dev`
branch by default — never `main` — unless the user has explicitly said otherwise in
this conversation.

```bash
git branch --show-current
```

- If the current branch is `dev` (or another branch the user explicitly named for this
  work), proceed normally.
- If the current branch is `main` (or `master`), **stop before committing anything** and
  warn the user: uncommitted changes are sitting on `main` and need to move to `dev`
  first. Offer to do it (e.g. `git stash`, switch to `dev`, `git stash pop`, then
  continue from Step 1 on `dev`) rather than committing on `main`.
- If changes somehow already got committed on `main` by mistake earlier in the session,
  flag that explicitly too and offer to move those commits to `dev` (e.g. reset `main`
  back and re-apply the commits on `dev`) — don't silently continue building on top of
  them on `main`.
- Never merge `dev` into `main` as part of this skill — that's a separate, explicit
  request the user makes when they actually want to release/promote the work.

## Step 1 — See what changed

Find the most recent commit and diff against it, including staged, unstaged, and
untracked files — you want the full picture of everything not yet committed, not just
what happens to be staged:

```bash
git status
git diff HEAD
git diff --stat HEAD
git log -1 --oneline
```

Untracked files won't show in `git diff`; list them separately (`git status
--porcelain`) and read the ones that look relevant. Read enough of the actual diff to
understand *what* changed and *why*, not just *which files* — that understanding is what
lets you group correctly and write commit messages that describe intent rather than
"updated files.py".

If there is no prior commit (fresh repo) or nothing has changed since the last commit,
say so and stop — there's nothing to do.

## Step 2 — Group changes by feature/topic

Don't treat the diff as one blob. Split it into logical groups the way a careful
engineer would if committing by hand: each group should be a coherent unit of work that
makes sense as a single commit on its own (e.g. "the new retry logic in the scheduler"
vs. "unrelated typo fix in the README" vs. "the OBS plugin scaffolding").

Signs two changes belong in the same group: they touch the same feature/module, one
doesn't make sense without the other, or they're described by the same sentence if you
had to explain the diff to a teammate. Signs they don't: different subsystems, unrelated
bug fixes bundled together, or docs-only changes unrelated to the code changes.

It's fine to end up with a single group if the whole diff really is one cohesive change —
don't force artificial splits just to produce multiple commits.

## Step 3 — Update the project's docs

Before committing, update the docs that track finished work, using whatever the project
already has:

- **Changelogs** — a project may keep two changelogs side by side; update each one that
  exists (in `docs/`, or at the repo root), under an `[Unreleased]` section:
  - **`CHANGELOG-developer.md`** — the detailed, technical log for developers: what
    changed and how (modules, endpoints, protocol messages, files), in Keep a Changelog
    style (Added / Changed / Fixed). Add an entry per meaningful group. If the project's own rules (CLAUDE.md) narrow what belongs
    in this file, follow them and put the rest in the commit message.
  - **`CHANGELOG.md`** — the short, public log for non-developer end users. Write in
    English, with short, simple sentences: one line per change,
    saying what the user can now do or what got fixed (headings "New / Changed / Fixed"). No code, file names, API names or internal jargon. Leave out small bug
    fixes, stability and internal improvements, refactors, tests, tooling and docs — only
    things a user would notice belong here. If a group of changes has nothing a user would
    notice, skip it in this file.
  - If a project has only a single `CHANGELOG.md`, keep using it as before and match its
    existing convention. Don't invent a changelog file for a project that doesn't have one
    unless the user asks for it.
- **Plan / roadmap files** — many projects track work in `docs/` files like `PLAN.md`,
  `ROADMAP.md`, or similarly named documents with checklists or phase breakdowns. If a
  group of changes completes or advances an item tracked there, update it: check off the
  item, mark the phase/task as done, or add a short note. Discover these files by
  looking in `docs/` (and the repo root) for anything plan/roadmap/todo-shaped — don't
  assume a fixed filename, since this varies per project.

If a change doesn't correspond to anything worth recording (e.g. a trivial fix), it's
fine to skip doc updates for that group — don't pad the changelogs with noise.

These doc edits become part of the same commit as the code they describe (see Step 5),
so make them before, not after, committing that group.

## Step 4 — Show the plan and get confirmation

Before touching git, summarize the plan in the chat: for each proposed commit, list the
files included and the exact commit message you intend to use. Wait for the user to
confirm (or adjust) before committing anything — grouping and message wording are
judgment calls worth a quick sanity check, and commits are easy to get wrong in bulk.

## Step 5 — Commit each group

For each confirmed group, in order:

1. `git add` exactly the files belonging to that group (including any doc files updated
   for it in Step 3) — never a blanket `git add -A`, to avoid sweeping in unrelated
   changes.
2. Commit with a Conventional Commits message:
   `<type>(<scope>): <description>`, optionally followed by a body and a footer.
   - **type**: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`,
     `style` — pick the one that matches what the change *does*, not just the file type
     it touches.
   - **scope**: include it when the affected module/area is unambiguous (e.g.
     `feat(scheduler): ...`); omit it when the change spans the project broadly or a
     scope would be arbitrary.
   - **description**: imperative mood, no trailing period, describes the effect of the
     change.
   - Use `BREAKING CHANGE:` in the footer only when the change is actually
     backwards-incompatible.
   - The commit message is always in English, regardless of what language the
     conversation is in.
3. Run the commit, then move to the next group.

Review `git status` after staging each group before committing, the same way you would
for any commit — if something unexpected got swept in, unstage it before proceeding.

## Step 6 — Stop after committing

Do not push. Report a short summary of what was committed (list of commit messages) and
leave pushing as a separate, explicitly-requested action.

## Notes

- Don't run test/lint suites as part of this skill — if the repo has a pre-commit hook,
  it'll run on its own; this skill's job is grouping, docs, and committing, not CI.
- If the diff is large and spans many unrelated areas, it's fine to end up with many
  commits — that's the point of grouping by feature rather than committing everything at
  once.
- If you're unsure whether two changes belong together, ask rather than guessing —
  wrong groupings are hard to un-bundle later.
