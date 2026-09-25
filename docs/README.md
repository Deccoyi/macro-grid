# Documentation

Where things live. Public entry points (`README.md`, `CONTRIBUTING.md`, `SECURITY.md`) stay at the repository root.

## Project status

| File | What it is |
|---|---|
| [roadmap.md](roadmap.md) | What is done, what is next, known gaps and what is not planned. |
| [CHANGELOG.md](CHANGELOG.md) | Short public changelog for non-developers. |
| [CHANGELOG-developer.md](CHANGELOG-developer.md) | Detailed technical changelog. |
| [releases/](releases/) | Release notes of published releases (one file per release). |

## How it works

| File | What it is |
|---|---|
| [architecture.md](architecture.md) | How the parts fit together: data model, WebSocket protocol, actions, variables, plugins, security model. |
| [design/](design/) | One design note per bigger feature (automatic profile switching, layout patches and assets, the JavaScript plugin runtime, variable types, automatic updates, the user agreement acceptance). |
| [plans/](plans/) | Designs of features that are planned and **not implemented yet**, with the priority order in [plans/README.md](plans/README.md). Move a plan to `design/` or `done/` when the feature ships (see the end of this page). |

## Working on the code

| File | What it is |
|---|---|
| [guides/development.md](guides/development.md) | Requirements, building, running, testing and the pitfalls. |
| [guides/engineering-guidelines.md](guides/engineering-guidelines.md) | The coding rules that are followed in this repository. |
| [guides/versioning.md](guides/versioning.md) | What is versioned, where each version lives, what counts as breaking. |
| [guides/release.md](guides/release.md) | How a release is made: build, installer, tags. |
| [ui/ui-guidelines.md](ui/ui-guidelines.md) | UI rules for the editor and the phone deck. |
| [ui/color-bible.md](ui/color-bible.md) | The color system ([live preview](ui/color-bible-preview.html)). |

## Working notes for AI agents

[agents/](agents/) holds notes written by and for AI coding agents: refactor logs, one-off review notes and proposals that wait for the
owner's decision. They are not user documentation. See [agents/README.md](agents/README.md).

## Where a new document goes

- A feature that is about to be built: `plans/`. Once it is built: `design/` if the note is still useful as a reference, otherwise `done/` (create it when needed).
- A rule or a how-to for contributors: `guides/`.
- Something about looks: `ui/`.
- Notes from an agent session, a review, or a proposal nobody approved yet: `agents/`.
- Finished one-off notes: delete them, or keep them in `agents/` if they still hold open items.
