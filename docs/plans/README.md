# Plans

Features that are planned and **not implemented yet**. When one ships, move it to `../design/` (as a reference) or delete it, and delete its
line here. Last reviewed: 2026-09-30.

## Plan, proposal or roadmap item?

| Kind | Where | What it is |
|---|---|---|
| **Plan** | `docs/plans/` (this folder) | A feature the owner wants, designed in enough detail to build. The priority order below is the only ordered list of work. |
| **Roadmap item** | `docs/roadmap.md` ("Next") | A one-paragraph idea for the public that has no plan file yet. When it gets a plan, it gets a row below and stays in the roadmap as one line. |
| **Proposal** | `docs/agents/proposals/` (local only, git-ignored; the other repositories keep theirs in `docs/agents/proposals/` too) | An idea from a review that needs a yes or no. Not on the priority list until the owner accepts it; then it becomes a plan or is just done. |

Each plan exists once. If the same text exists in another repository or on the Desktop, that copy is deleted, not kept in sync.

## Priority order

| # | Work | Repositories | Plan | Why here |
|---|---|---|---|---|
| 1 | Roadmap "Next": the parts of a store-like Discover tab that need catalog fields (screenshots, tags, real icons) | `macro-grid` | [roadmap.md](../roadmap.md) | Larger, no user is blocked on them, no plan file yet for any of them. |

Done and no longer listed: the `web` widget (iframe with popups, downloads and navigation blocked; crash guard, recommended live number and Keep loaded on the phone), the branded installer, the plugin manifest field rename (`macroGrid` is now `minMacroGrid`, the old name still read), version unification — one version for the server and the SDK, a `macroGrid` field for every plugin and
the phone app, one release guide, all four sections released,
publishing the SDK NuGet package only when it changed (with a package-validation guardrail and a real old-plugin-on-new-server test, shipped after 1.0.1), the first alpha releases (`server-v0.2.0-alpha` and `server-v0.2.1-alpha` are published, the repositories are public), variable types and boolean conditions (see [../design/variable-types-and-conditions.md](../design/variable-types-and-conditions.md)), auto-update of the server and the user agreement acceptance that goes with it (built, shipped in 0.3.0; see [../design/auto-update.md](../design/auto-update.md) and [../design/agreement-acceptance.md](../design/agreement-acceptance.md)), auto-update of the phone app (see [../design/phone-app-auto-update.md](../design/phone-app-auto-update.md)), plugin distribution (see [../design/plugin-distribution.md](../design/plugin-distribution.md)), SDK 0.4.0 and the SoundBoard plugin (see [../design/sdk-0.4.0-and-soundboard.md](../design/sdk-0.4.0-and-soundboard.md)), the editor docking workspace (see [../design/docking-workspace.md](../design/docking-workspace.md)), the hierarchy tree including its phase 6 (see [../design/hierarchy-tree-and-folders.md](../design/hierarchy-tree-and-folders.md)) and its standalone Plugins tool window (see [../design/plugins-tool-window.md](../design/plugins-tool-window.md)), editor edit commands (context menus, an Edit menu, undo/redo, standard shortcuts and a header toolbar; see [../design/editor-edit-commands.md](../design/editor-edit-commands.md)), Issues and Discussions (the labels and the flow are in `CONTRIBUTING.md`), a much faster install and update (49 published files instead of thousands), security hardening (see [../design/security-hardening.md](../design/security-hardening.md)), and custom plugin widgets (see [../design/plugin-widgets.md](../design/plugin-widgets.md)).

## Rules

- **All plans live here, in the server repository**, even when the work happens in the plugin or the phone app repository. That keeps one priority list and one place to look. Name the repository a phase belongs to in the plan itself. The other repositories' `docs/` folders hold no plans.
- Finish the current item before starting the next, unless the owner names one (a plan is a design, not a start signal).
- A plan states its status and the **repositories it touches** on the first lines, and lists its open questions at the end. Inside a plan, name the repository a phase or step belongs to.
- A change that touches the security model or the SDK says so in its plan and updates `../architecture.md` or `../guides/versioning.md` in the same change.
