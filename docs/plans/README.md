# Plans

Features that are planned and **not implemented yet**. When one ships, move it to `../design/` (as a reference) or `../done/`, and delete its
line here. Last reviewed: 2026-09-25.

## Plan, proposal or roadmap item?

| Kind | Where | What it is |
|---|---|---|
| **Plan** | `docs/plans/` (this folder) | A feature the owner wants, designed in enough detail to build. The priority order below is the only ordered list of work. |
| **Roadmap item** | `docs/roadmap.md` ("Next") | A one-paragraph idea for the public that has no plan file yet. When it gets a plan, it gets a row below and stays in the roadmap as one line. |
| **Proposal** | `docs/agents/proposals/` (and the same folder in the other repositories) | An idea from a review that needs a yes or no. Not on the priority list until the owner accepts it; then it becomes a plan or is just done. |

Each plan exists once. If the same text exists in another repository or on the Desktop, that copy is deleted, not kept in sync.

## Priority order

| # | Work | Repositories | Plan | Why here |
|---|---|---|---|---|
| 0 | **One version for the server and the SDK (1.0.0), a `macroGrid` field for plugins, one release guide** | `macro-grid`, then `macro-grid-plugin` and `macro-grid-client` | [version-unification-plan.md](version-unification-plan.md) | Ends the drift between the server, SDK and plugin numbers, and the contradicting release notes. Server and SDK first; the plugins and the phone app follow once SDK 1.0.0 is on NuGet. |
| 1 | **Plugin distribution**, phases 1 to 5 (official catalog, added sources, direct links, badges and updates) | `macro-grid-plugin` (phase 1), then `macro-grid` | [plugin-distribution-plan.md](plugin-distribution-plan.md) | Fills the empty Discover tab. Shares the HTTP and download rules with auto-update, so do it after it. Phase 1 lives in the plugin repository only. |
| 2 | **Auto-update of the phone app** (release-signed APK on every release, check GitHub, download, verify, Android's installer) | `macro-grid-client` (all code), `macro-grid` (website pages) | [phone-app-auto-update-plan.md](phone-app-auto-update-plan.md) | Waiting for the owner's approval. Reuses the server's update rules; its first phase fixes the release signing, which every later client release needs. |
| 3 | **Issues and Discussions:** a label system, forms plus a plain-text report to fill in, sorting by area on its own, Discussions for questions and ideas | `macro-grid`, `macro-grid-client`, `macro-grid-plugin` | [issues-and-discussions-plan.md](issues-and-discussions-plan.md) | Small and no code, but it changes how people reach the project. Local files are ready; the GitHub steps (Discussions, labels) wait for the owner. |
| 4 | **SDK 0.4.0 and the sound plugin** | `macro-grid`, then `macro-grid-plugin` | [sound-plugin-plan.md](sound-plugin-plan.md) (phase A here, phase B in the plugin repository) | Needs new settings field kinds, so it changes the SDK and the editor forms first. Add the plugin logo field in the same SDK bump (roadmap, "Next"). |
| 5 | Roadmap "Next": an encrypted connection (TLS), editor keyboard shortcuts with undo/redo, a branded installer (logo and colors), the `plugin-html` widget, the `web` widget, an async host API for JavaScript plugins | `macro-grid`, `macro-grid-client` (the `web` widget) | [roadmap.md](../roadmap.md) | Larger, no user is blocked on them. |

Done and no longer listed: the first alpha releases (`server-v0.2.0-alpha` and `server-v0.2.1-alpha` are published, the repositories are public), variable types and boolean conditions (see [../design/variable-types-and-conditions.md](../design/variable-types-and-conditions.md)), auto-update of the server and the user agreement acceptance that goes with it (built, shipped in 0.3.0; see [../design/auto-update.md](../design/auto-update.md) and [../design/agreement-acceptance.md](../design/agreement-acceptance.md)).

## Rules

- **All plans live here, in the server repository**, even when the work happens in the plugin or the phone app repository. That keeps one priority list and one place to look. Name the repository a phase belongs to in the plan itself. The other repositories' `docs/` folders hold no plans.
- Finish the current item before starting the next, unless the owner names one (a plan is a design, not a start signal).
- A plan states its status and the **repositories it touches** on the first lines, and lists its open questions at the end. Inside a plan, name the repository a phase or step belongs to.
- A change that touches the security model or the SDK says so in its plan and updates `../architecture.md` or `../guides/versioning.md` in the same change.
