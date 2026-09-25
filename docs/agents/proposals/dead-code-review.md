# Dead code review

Scans run on this branch: unused usings and private members (compiler style rules), unused TypeScript locals/parameters (`noUnused*`), TypeScript exports without an importer, C# public/internal types, methods and properties referenced only once in `src/` and `tests/`, and a duplicate-code scan. Everything provably unused was removed (see `docs/agents/refactor-notes.md`). What is left below was **not** deleted because it is reached in ways a text search cannot prove.

## Kept on purpose (looks unused, is not)
- `packages/renderer/src/types.ts` exports (for example `DynamicCase`): part of the renderer package surface (`export *` from `index.ts`) and mirrored by the server model.
- `MacroGrid.Plugin.Abstractions` members: public SDK, consumed by plugins in another repository.
- Protocol records and `AppPreferences` / profile properties: (de)serialized by name, never referenced from C#.

## Suspicious, needs a decision
- `src/MacroGrid.Host/wwwroot/index.html` and the tray item "Open test page": temporary test client, superseded by the deck at `/deck/`. See `retire-test-client.md`.
- `packages/renderer/demo/`: a Vite playground that no script, CI job or doc references. Keep only if someone still uses it for visual checks; otherwise delete, and drop `packages/renderer/tsconfig.json` from including it if it does.
- Old docs and one-off files in `docs/`: see `docs-housekeeping.md`.
- Example ids such as `obs.switchScene` in tests and docs are sample data for the first-party plugin id, not a dependency.
