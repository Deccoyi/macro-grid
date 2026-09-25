# Refactor notes (branch `refactor/cleanup`)

Behavior is unchanged unless a line says otherwise. Suspicious leftovers: `docs/agents/proposals/dead-code-review.md`. Behavior-changing ideas: `docs/agents/proposals/`.

## Tooling
- Added `.editorconfig` and `docs/guides/engineering-guidelines.md`.
- `noUnusedLocals` / `noUnusedParameters` on in editor, webclient and renderer; one unused test import removed.
- Unused C# usings removed (only one was found in the whole solution).

## Removed
- `ClientSession.RemoteAddress` / `ConnectedAt` (never read; the constructor no longer takes the address, the log line still prints it).
- Redundant `export` on ~45 symbols only used inside their own file (editor, webclient). Renderer exports were left alone because the renderer is a package with a public surface (`export *` from `types.ts`).

## Split / moved
- `MacroGrid.Host/ServerApp.cs` (569 lines) is now the composition root; endpoints live in `Host/Api/*Api.cs` (`MapProfileApi`, `MapAppApi`, `MapCatalogApi`, `MapPluginApi`, `MapWindowApi`, `MapDeviceApi`) and DI registration in `Host/ServiceRegistration.cs`. Routes, methods and registration order are unchanged.
- Shared helpers extracted: `ApiResults` (JSON result, `{ error }` result, tolerant JSON body reader), `OptionsEndpoint` (the two identical "dynamic options" endpoints), `AddActionHandler` / `AddVariableProvider` / `AddHostedSingleton` registration helpers, `WindowApi.ShowAsync`.
- `PluginManager` and `JsPlugin` are partial classes by concern (`*.Loading`, `*.Unloading`, `JsPlugin.HostApi`, `JsPlugin.Bootstrap`; `JsAction` and `JsSettingsPage` got their own files). Entry lookup shared through `FindEntry`.
- `Core/Model/Profile.cs` is now one file per type (same namespace and names).
- `AssetStore`, `ToggleStateStore`, `WidgetStateService` moved from `Core/Sessions` to `Core/Widgets` (namespace `MacroGrid.Core.Widgets`; internal use only, public class names unchanged).
- Tray, window and dialog classes moved to `Host/Ui` (namespace `MacroGrid.Host.Ui`).
- editor: `useEditorState` composed from `useProfileDocument`, `usePageActions`, `useWidgetActions`, `useServerCatalogs` (same returned shape); `api/client.ts` uses shared `get` / `send` helpers; the redundant mount-time catalog fetch (it ran twice) is now one.
- webclient: `App.tsx` split into `components/TopBar`, `ConnectScreen`, `ActionErrorToast`; inline style objects hoisted into typed constants.

## Public surface / wire changes
- `/ws` non-WebSocket request now answers `A WebSocket request is expected.` (was a Turkish sentence). Text only, no client reads it.
- webclient UI text moved from inline English/Turkish pairs to `webclient/src/i18n/{en,tr}.ts` (same strings).

## Comments
- Example names of a third-party product in source comments (SDK XML docs, core, editor) replaced by generic ones. Sample ids such as `obs.switchScene` in tests and docs are left as sample data.

## Deviations from the plan
- Turkish translation pass: everything that remains is real localization data (`HostText`, `tr.ts`, `catalogText.ts`, bilingual fallbacks passed to `AppLanguage.Pick`, tests of those). No Turkish comments were found, so nothing was translated.
- `tr.ts` / `en.ts` key parity is already enforced by the compiler (`en` is typed as `Record<keyof typeof tr, ...>`), so no extra runtime test was added.
