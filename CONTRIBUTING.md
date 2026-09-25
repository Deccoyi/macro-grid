# Contributing to Macro Grid

> **AI-generated software.** All code, design and documentation of this project, including this file, were created by artificial
> intelligence at the maintainer's direction. It is alpha-stage, has not been reviewed line by line by a human or security-audited, and is
> provided "as is", without warranty of any kind. You use it entirely at your own risk (see the [README](README.md) and the
> [MIT license](LICENSE)).

Thanks for your interest. This repository is the server and editor; the phone app is
[macro-grid-client](https://github.com/Deccoyi/macro-grid-client) and the plugins are
[macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin). Each has its own version and its own contribution rules.

## This is a hobby project

Macro Grid is maintained in spare time. Issues and pull requests are welcome, but replies and reviews can take a while, and there is no
promise that a request will be accepted or a pull request merged. Please be patient, and don't expect support on a schedule.

## Getting set up

See [docs/guides/development.md](docs/guides/development.md) for the requirements, how to build and run, how to test, and the pitfalls. In short: Windows 10 or 11,
the .NET 10 SDK and Node.js 20+.

```powershell
cd editor;    npm ci; npm run build; cd ..
cd webclient; npm ci; npm run build; cd ..
dotnet build
dotnet test
```

(`dotnet run --project src/MacroGrid.Host` needs the built bundles copied into `src/MacroGrid.Host/wwwroot`; see the development guide.)

## Before you start

- For anything bigger than a small fix, open an issue first so we can agree on the approach. Look at [docs/roadmap.md](docs/roadmap.md) for what is planned.
- Branching: `main` holds releases and `dev` is the integration branch. Work on a branch from `dev` and open pull requests against `dev`.
  The maintainer merges `dev` into `main` for a release ([docs/guides/release.md](docs/guides/release.md)).
- Keep a pull request to one topic. Several small, focused commits are better than one large one.

## Rules

- **Language.** Code, identifiers, comments, documentation, changelogs, log messages and commit messages are written in **English**. Text the user sees
  in the editor goes through the i18n files (`editor/src/i18n/tr.ts` and `en.ts`), never hard-coded in components, and a new string needs both languages.
  If you touch existing Turkish text in code, comments or docs, translate the part you touch.
- **Commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description`, for example
  `fix(editor): keep the selection when a widget is resized`.
- **Changelogs.** When a change is finished, update `docs/CHANGELOG.md` under `[Unreleased]` (short, plain sentences for non-developers, no code, file or API names, and
  without small fixes or internal changes). Add to `docs/CHANGELOG-developer.md` only for changes to the plugin SDK or the WebSocket protocol, breaking changes, migrations, or
  anything a plugin author or maintainer has to do differently; the rest belongs in the commit message and the pull request description.
- **Versions** are never bumped in a pull request. The maintainer decides that at release time ([docs/guides/versioning.md](docs/guides/versioning.md)). Changes to the
  WebSocket protocol must stay compatible with older clients (add optional capabilities instead of changing existing messages), and changes to the plugin SDK
  (`MacroGrid.Plugin.Abstractions`) can break plugins, so call them out.
- **Names.** Do not mention third-party product or brand names in code, comments, docs or commits, except where the product is the functional target of the code
  itself (for example a plugin that talks to it). Describe patterns generically.
- **UI.** Read [docs/ui/ui-guidelines.md](docs/ui/ui-guidelines.md) and use the colors in [docs/ui/color-bible.md](docs/ui/color-bible.md). No emoji or text symbols as icons; use
  `lucide-react`.
- **Dependencies.** Check the license of a new dependency and add it to [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). No copyleft dependencies without a
  discussion.

## Tests

Add or update tests with your change. Server logic has tests in `tests/MacroGrid.Tests`; the renderer has Vitest tests in `packages/renderer/test`. Run `dotnet test`
and the type checks (`npm run typecheck` in `editor/`, `webclient/` and `packages/renderer/`) before you open a pull request.

## Code of conduct

Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please do not report security problems in a public issue; see [SECURITY.md](SECURITY.md).

## Issues and labels

Open an issue from the [chooser](https://github.com/Deccoyi/macro-grid/issues/new/choose): pick a form, or its plain-text twin (the same questions, written as
text you fill in). Questions and ideas start in [Discussions](https://github.com/Deccoyi/macro-grid/discussions); a maintainer turns one into an issue when there is
something to fix or build. Security problems go to the private form, never to a public issue.

What the labels mean. New issues get `needs-triage` and the area on their own; the maintainer sets the rest.

| Label | Meaning |
|---|---|
| `bug`, `enhancement`, `documentation` | The kind of work. |
| `regression` | It worked in an earlier version. |
| `area: editor` | The editor window |
| `area: server` | The server or the tray app |
| `area: deck` | The browser deck |
| `area: pairing` | Pairing and connecting phones |
| `area: installer` | The installer or an update |
| `area: plugins` | Loading or running plugins |
| `area: updates` | Checking for or installing updates |
| `needs-triage` | Not looked at yet (automatic). |
| `needs-info` | We asked a question and wait for the reporter. |
| `confirmed` | Reproduced or accepted by a maintainer. |
| `in progress` | Someone is working on it. |
| `priority: high` | Blocks people: a crash, lost data or a broken install. |
| `good first issue`, `help wanted` | A good place to start, or where help is welcome. |
| `duplicate`, `invalid`, `wontfix` | Closing reasons; the closing comment says why. |

## License

By contributing you agree that your contribution is licensed under the [MIT license](LICENSE) of this repository.
