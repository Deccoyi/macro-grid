# Documentation

Where things live. Public entry points (`README.md`, `CONTRIBUTING.md`, `SECURITY.md`) stay at the repository root.

## Project status

| File | What it is |
|---|---|
| [roadmap.md](roadmap.md) | What is done, what is next, known gaps and what is not planned. |
| [CHANGELOG.md](CHANGELOG.md) | Short public changelog for non-developers. |
| [CHANGELOG-developer.md](CHANGELOG-developer.md) | Detailed technical changelog. |

## How it works

| File | What it is |
|---|---|
| [architecture.md](architecture.md) | How the parts fit together: data model, WebSocket protocol, actions, variables, plugins, security model. |
| [design/](design/) | One design note per bigger feature (automatic profile switching, layout patches and assets, the JavaScript plugin runtime, variable types, automatic updates, the user agreement acceptance), and the [security risk assessment](design/security-risk-assessment.md). |
| [plans/](plans/) | Designs of features that are planned and **not implemented yet**, with the priority order in [plans/README.md](plans/README.md). Move a plan to `design/` when the feature ships and is still useful as a reference (see the end of this page). |

## Working on the code

| File | What it is |
|---|---|
| [guides/development.md](guides/development.md) | Requirements, building, running, testing and the pitfalls. |
| [guides/engineering-guidelines.md](guides/engineering-guidelines.md) | The coding rules that are followed in this repository. |
| [guides/versioning.md](guides/versioning.md) | What is versioned, where each version lives, what counts as breaking. |
| [guides/release.md](guides/release.md) | How a release is made: build, installer, tags. |
| [ui/ui-guidelines.md](ui/ui-guidelines.md) | UI rules for the editor and the phone deck. |
| [ui/color-bible.md](ui/color-bible.md) | The color system ([live preview](ui/color-bible-preview.html)). |
| [ui/editor-icons.md](ui/editor-icons.md) | Editor icons: format, sizes, colors by state, the full list, and the custom icons to draw ([preview](ui/editor-icons-preview.html)). |

## Where a new document goes

- A feature that is about to be built: `plans/`. Once it is built: `design/` if the note is still useful as a reference, otherwise delete it.
- A rule or a how-to for contributors: `guides/`.
- Something about looks: `ui/`.
- Working notes of the maintainer or an agent (session handoffs, refactor logs, unapproved proposals): `agents/` and `done/`. Both are git-ignored, so they stay on the maintainer's machine and are not published.
