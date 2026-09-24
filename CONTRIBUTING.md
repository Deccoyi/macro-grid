# Contributing to Macro Grid

Thanks for your interest. This repository is the server and editor; the phone app is
[macro-grid-client](https://github.com/Deccoyi/macro-grid-client) and the plugins are
[macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin). Each has its own version and its own contribution rules.

## Getting set up

See [docs/development.md](docs/development.md) for the requirements, how to build and run, how to test, and the pitfalls. In short: .NET 10 SDK, Node.js 20+,
Windows 10 or 11, `dotnet test` for the server tests.

## Before you start

- For anything bigger than a small fix, open an issue first so we can agree on the approach. Look at [docs/roadmap.md](docs/roadmap.md) for what is planned.
- Work on the `dev` branch (or a branch from it) and open pull requests against `dev`. `main` is for releases.
- Keep a pull request to one topic. Several small, focused commits are better than one large one.

## Rules

- **Language.** Code, identifiers, comments, documentation, changelogs, log messages and commit messages are written in **English**. Text the user sees
  in the editor goes through the i18n files (`editor/src/i18n/tr.ts` and `en.ts`), never hard-coded in components, and a new string needs both languages.
  If you touch existing Turkish text in code, comments or docs, translate the part you touch.
- **Commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description`, for example
  `fix(editor): keep the selection when a widget is resized`.
- **Changelogs.** Update both under `[Unreleased]` when a change is finished: `docs/CHANGELOG-developer.md` (detailed, technical, Keep a Changelog style) and
  `docs/CHANGELOG.md` (short, plain sentences for non-developers, no code, file or API names, and without small fixes or internal changes).
- **Versions** are never bumped in a pull request. The maintainer decides that at release time ([docs/versioning.md](docs/versioning.md)). Changes to the
  WebSocket protocol must stay compatible with older clients (add optional capabilities instead of changing existing messages), and changes to the plugin SDK
  (`MacroGrid.Plugin.Abstractions`) can break plugins, so call them out.
- **Names.** Do not mention third-party product or brand names in code, comments, docs or commits, except where the product is the functional target of the code
  itself (for example a plugin that talks to it). Describe patterns generically.
- **UI.** Read [docs/ui-guidelines.md](docs/ui-guidelines.md) and use the colors in [docs/color-bible.md](docs/color-bible.md). No emoji or text symbols as icons; use
  `lucide-react`.
- **Dependencies.** Check the license of a new dependency and add it to [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). No copyleft dependencies without a
  discussion.

## Tests

Add or update tests with your change. Server logic has tests in `tests/MacroGrid.Tests`; the renderer has Vitest tests in `packages/renderer/test`. Run `dotnet test`
and the type checks (`npm run typecheck` in `editor/`, `webclient/` and `packages/renderer/`) before you open a pull request.

## Security

Please do not report security problems in a public issue; see [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contribution is licensed under the [MIT license](LICENSE) of this repository.
