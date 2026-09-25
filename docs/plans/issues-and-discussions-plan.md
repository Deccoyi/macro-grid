# Issues and Discussions (all three repositories)

Status: **local files written and checked, GitHub not changed yet** (2026-09-25). The owner approved the plan ("yap"); the open questions took the recommended answers (see the end). What is left are the steps that reach GitHub (labels, Discussions), each after the owner's OK, and the commit and push, which the owner does with the other pending work.
**Repositories:** `macro-grid`, `macro-grid-client` and `macro-grid-plugin` (the same design in each; the plan lives here, like every plan). No code changes; only `.github/` files, a small script, `SUPPORT.md` and the contributing docs.

Goal: make the Issues page as easy to use as a well-run project's: a form for every kind of report, a small set of clear labels, new issues sorted by area on their own, and a separate place (Discussions) for questions and ideas, so an issue always means "something to fix or build".

## What exists today (checked 2026-09-25)

- **Forms (issue forms) already exist** in every repository: `bug_report.yml`, `feature_request.yml`, `config.yml` with `blank_issues_enabled: false` and contact links (private security report, the other two repositories); the plugin repository also has `plugin_request.yml`.
- **Labels are GitHub's defaults only:** `bug`, `enhancement`, `documentation`, `question`, `duplicate`, `invalid`, `wontfix`, `good first issue`, `help wanted`, `accessibility`, plus the bot's `dependencies`, `github_actions`, and a language label (`.NET`, `java`, `javascript`). The plugin form uses `plugin-request`, which is not a label there yet.
- **Discussions are off** (`has_discussions: false`), Projects are off. There is no `SUPPORT.md`.
- **The forms ask "where does it happen?"** in the server form (a dropdown) and "which plugin" in the plugin form, but the answer stays text in the issue body; nothing turns it into a label. The client form has no such question.
- Security reports go to the private advisory form (private vulnerability reporting is on in all three).

So the forms are good; what is missing is the label system, the sorting, and a place for questions.

## Design

### 1. Labels (same shape in all three repositories)

Names are English, lower case, `group: value` so they sort together in the label list. Colors follow the group, so a glance shows the kind.

| Group | Labels | Use |
|---|---|---|
| **Type** | `bug`, `enhancement`, `documentation`, `regression` (new), `plugin-request` (plugin repo) | What kind of work it is. `regression` = worked in an earlier version. |
| **Area** (server) | `area: editor`, `area: server`, `area: deck`, `area: pairing`, `area: installer`, `area: plugins`, `area: updates` | Where it happens. Set from the form's dropdown. |
| **Area** (client) | `area: connection`, `area: deck`, `area: kiosk`, `area: settings`, `area: updates`, `area: install` | Same idea for the phone app. |
| **Area** (plugin) | `plugin: obs`, `plugin: plc-icons`, `plugin: hello-js`, `area: sdk`, `area: store`, `area: docs-site` | Set from the plugin dropdown. |
| **Status** | `needs-triage` (added automatically), `needs-info` (we asked, waiting for the reporter), `confirmed`, `in progress` | Where it stands. Exactly one at a time. |
| **Priority** | `priority: high` | Only for what blocks people (a crash, lost data, a broken install). No other priority labels: a scale nobody keeps up is noise. |
| **Community** | `good first issue`, `help wanted`, `accessibility` | Kept as they are. |
| **Closing** | `duplicate`, `invalid`, `wontfix` | Kept. The closing comment says why, in one sentence. |
| **Bot** | `dependencies`, `github_actions`, `.NET`, `java`, `javascript` | Kept; owned by Dependabot. |

`question` is retired: questions go to Discussions (below). The old label is kept on closed history and removed from the forms.

The label list lives in `.github/labels.json` in each repository (name, color, description), and `scripts/sync-labels.ps1` applies it with `gh` (creates the missing ones, updates color and description, renames nothing silently, deletes nothing: an unknown label is only reported). It is idempotent and safe to run again.

### 2. Forms

Existing forms stay; changes:

- **Server `bug_report.yml`:** keep the "where does it happen?" dropdown; its options map one to one to the `area:` labels (Editor, Server or tray app, Browser deck, Phone app connection or pairing → `area: pairing`, Plugin loading → `area: plugins`, Installer; add "Updates" → `area: updates`; "Other" adds no area).
- **Client `bug_report.yml`:** add the same kind of dropdown (Connecting or pairing, The deck screen, Kiosk mode or rotation, Settings, Updates, Installing the app, Other).
- **Plugin `bug_report.yml`:** its "Plugin" dropdown maps to `plugin: …`.
- **A new "Documentation problem" form** (`documentation.yml`) in all three: a page or file that is wrong, unclear or missing; label `documentation`.
- **All bug forms** gain a checkbox "I searched the existing issues and Discussions" and a "Was it working in an earlier version?" question (Yes: which one, No, Not sure); a "Yes" adds `regression`.
- **`config.yml`:** keep `blank_issues_enabled: false`; add a contact link "Ask a question or share an idea" that opens Discussions, next to the existing security and other-repository links.
- The forms' static `labels:` stay for the type (`bug`, `enhancement`, `documentation`, `plugin-request`).

### 2b. A plain-text report to fill in (owner request)

Besides the forms, every report can be written as **plain text with blanks**: a short fixed layout that a person copies, fills in and pastes,
with no clicking through fields. It is for people who find a form clumsy, who report from a phone, or who paste what another tool gave them.

- **Markdown templates next to the forms.** GitHub lists `.md` templates and `.yml` forms together in the chooser, so this adds a second entry
  per kind: "Bug report (plain text)" and "Feature request (plain text)" (`bug_report_text.md`, `feature_request_text.md`), the same in all three
  repositories, with `name`, `about` and the static label (`bug`, `enhancement`) in the file's header block.
- **The layout is fixed and short**, each line a label with a blank to fill (the parts in `[...]` are replaced by the person; an HTML comment
  under each line says what to write and disappears in the rendered issue):

```
Version: [e.g. 0.2.1]
Where: [editor / server or tray / browser deck / pairing / installer / updates / other]
What happened: [one or two sentences]
What I expected: [one sentence]
Steps: 1. [...]  2. [...]  3. [...]
Worked in an earlier version: [yes, version ... / no / not sure]
Windows: [e.g. Windows 11 23H2]
Logs or screenshots: [paste here, remove anything private]
```

  The feature request has the same shape (`Problem:`, `Idea:`, `Alternatives I tried:`, `Who else needs it:`), the plugin request adds
  `What should the plugin control or show:`, the client bug adds `Phone and Android version:` and `Server version:`.
- **Sorting still works:** `triage.yml` (section 3) reads the `Where:` and `Worked in an earlier version:` lines of a text report the same way
  as the form's dropdowns (a known word after the label; anything else adds no area).
- **The chooser order:** forms first (they give the most complete reports), plain text second; both say in one sentence what they are for.
- **Later, in the app (separate from this plan, added to the roadmap when wanted):** a "Copy report info" button in the editor's Help window
  and in the phone app's settings that puts the version, Windows or Android version and the recent log lines onto the clipboard in exactly
  this layout, so the person only adds "what happened".

### 3. Sorting by itself

GitHub forms can only set fixed labels, so the area and status labels need a small workflow, `.github/workflows/triage.yml`, in each repository:

- Trigger: `issues: [opened]`. Permission: `issues: write` only. No secrets, no checkout of the repository code, no third-party action: one step with GitHub's own `actions/github-script`, pinned to a commit and updated by Dependabot like the other actions.
- The script adds `needs-triage`, reads the dropdown answers from the issue body (the form writes each question as a heading with the answer under it) and adds the mapped `area:`/`plugin:` label, and adds `regression` when the "earlier version" answer is Yes. It never removes a label and never closes or comments.
- Dependabot and pull requests are skipped (`issues` only, and pull requests have their own template).
- The script is short and lives inline in the workflow, with the mapping as a table at its top, so a new dropdown option is a one-line change.

The maintainer changes `needs-triage` to `confirmed` or `needs-info` by hand; there is no bot that closes stale issues (an alpha with a single maintainer should not close people's reports on a timer).

### 4. Discussions (owner decision: on in all three repositories)

- Categories: **Q&A** (an answer can be marked), **Ideas** (feature ideas before they are issues), **Show and tell** (profiles, layouts, setups people made), **General**. Announcements are for the maintainer only. Categories are created in the repository settings on the website (GitHub has no API for them); the plan lists the exact names to click.
- Discussion forms (`.github/DISCUSSION_TEMPLATE/`) for Q&A (what are you trying to do, what did you try, versions) and Ideas (the problem first, then the idea), so questions arrive as complete as bug reports.
- **Rule of thumb, written in `SUPPORT.md` and `CONTRIBUTING.md`:** a question or an idea starts in Discussions; a maintainer turns a discussion into an issue (GitHub has a button for it) when there is something to build or fix. A bug goes straight to an issue.
- Server repository is the main place for general talk; the other two repositories have the same categories but their Ideas and Q&A only concern the phone app or the plugins (each `config.yml` link says so).
- Roadmap: `docs/roadmap.md` stays the only public list of planned work; an Ideas discussion that is accepted becomes a roadmap line or a plan, and the discussion is closed with a link to it.

### 5. Files and docs

- The plain-text templates (`bug_report_text.md`, `feature_request_text.md`) in each repository, and `plugin_request_text.md` in the plugin repository.
- `SUPPORT.md` in each repository (GitHub shows it in the new-issue chooser and on the Support tab): where to ask what, one short page.
- `CONTRIBUTING.md` in each repository: a short "Issues and labels" section with the table above (what each label means and who sets it).
- `docs/guides/triage.md` (server repository, applies to all three): how the maintainer handles a new issue in five steps (read it; label `confirmed` or `needs-info`; reproduce; set `priority: high` only when it blocks people; close with a one-line reason), and the answer templates for the common cases (needs more info, duplicate, works as designed).
- Both changelogs are not touched (this is project housekeeping, not a change of the software).

## Rollout (each step outward to GitHub needs the owner's OK)

1. **Local files** (after the owner's review of this plan): forms, `config.yml`, `triage.yml`, `.github/labels.json`, `scripts/sync-labels.ps1`, `SUPPORT.md`, `CONTRIBUTING.md`, `docs/guides/triage.md`, the discussion forms. Nothing reaches GitHub until the owner commits and pushes.
2. **Labels on GitHub:** run the sync script once per repository (`gh`, the token already has the `repo` scope). First a dry run that lists what it would create or change.
3. **Discussions:** turn them on in the three repositories (`gh api`, a repository setting), then create the categories by hand on the website.
4. **Check:** open a test issue from each form in a scratch state (or use the form preview), see the labels arrive, and delete or close the test issues.
5. Announce nothing; the new chooser page is the announcement.

## Risks and notes

- **Public and open:** Discussions and issues are public and anyone with a GitHub account can write; the project's Code of Conduct already exists and applies. The maintainer gets notifications; set the watch level to what is manageable.
- **Spam and abuse:** GitHub's own moderation (lock, hide, block) is enough at this size; no bot is added for it.
- **Triage workflow:** it reads issue text written by strangers. The script must treat it as data (match known option strings only, never run or interpolate it), and its permission stays `issues: write`.
- **One person's time:** more structure only helps if it is kept up. The plan therefore keeps the label set small, has one status at a time, and adds no automation that closes anything.
- Older issues keep their labels; nothing is bulk-relabelled.

## Done locally (2026-09-25, uncommitted in each repository)

- **Labels:** `.github/labels.json` (12 or 13 new labels per repository) and `scripts/sync-labels.ps1`. Checked with a dry run against GitHub (read only): it lists what it would create, changes nothing without `-Apply`, deletes and renames nothing.
- **Forms:** `bug_report.yml` changed (a "where" dropdown in the client form, "Updates" in the server one, the "did it work in an earlier version" question and the "I searched" checkbox in all); new `documentation.yml`; plain-text twins `bug_report_text.md`, `feature_request_text.md` (and `plugin_request_text.md` in the plugin repository); `config.yml` has the Discussions link.
- **Sorting:** `.github/workflows/triage.yml` (GitHub's `actions/github-script` v9.0.0 pinned to its commit, `issues: write` only, adds only labels that already exist). Its logic was run against nine made-up issue texts, including a hostile one (`constructor`), and gave the expected labels each time.
- **Discussions forms:** `.github/DISCUSSION_TEMPLATE/q-a.yml`, `ideas.yml`, `show-and-tell.yml`.
- **Docs:** `SUPPORT.md` and an "Issues and labels" section in `CONTRIBUTING.md` in each repository; `docs/guides/triage.md` (maintainer's five steps and answers to copy) in this one.
- All YAML and JSON parse; the templates have unique field ids; nothing else in the repositories was touched.

## Still to do, in this order (each step outward to GitHub needs the owner's OK)

1. **Turn on Discussions** in the three repositories (`gh api`, a repository setting), then create the categories by hand on the website: **Q&A** (answerable), **Ideas**, **Show and tell**, **General**; the Announcements category stays for the maintainer. The category names must match the file names of the discussion forms (`q-a`, `ideas`, `show-and-tell`). Do this **before** the push: the chooser's new "Ask a question" link and `SUPPORT.md` point to Discussions.
2. **Create the labels:** `scripts/sync-labels.ps1 -Apply` once per repository (the dry run is already clean).
3. **Commit and push** with the other pending work (the owner's decision). The forms, the triage workflow and the Discussions forms only start to work on the default branch.
4. **Check:** open one test issue from each form and one from a plain-text template, look at the labels that arrive, then close the test issues. Ask a test question in Discussions.

## Open questions (answered with the recommended defaults on 2026-09-25)

0. **Plain text:** both entries, the form and the plain text, stay in the chooser. Done.
1. **Label wording and colors:** as proposed (soft red for type and priority, blue for area, yellow shades for status, purple for plugins, GitHub's own colors for the kept labels). Done.
2. **`priority: high`:** kept, and it is the only priority label. Done.
3. **"Show and tell":** included. Done.
4. **`SUPPORT.md`:** a short copy in each repository. Done.
