# Development

How to build, run and test the server and editor, and the pitfalls that are easy to hit. For how it fits together see
[architecture.md](architecture.md); for the rules for contributions see [../CONTRIBUTING.md](../CONTRIBUTING.md).

## Requirements

- Windows 10 or 11 (the server is Windows-only: WinForms tray app, WebView2, `SendInput`, core audio).
- [.NET 10 SDK](https://dotnet.microsoft.com/download).
- [Node.js](https://nodejs.org/) 20 or newer (CI and the maintainers use 24), for the editor and the browser deck.
- The [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (already part of current Windows).

`nuget.config` pins nuget.org as the only package source; keep it.

## Build and run

The editor and the browser deck are React apps. The server serves their built bundles from `src/MacroGrid.Host/wwwroot/editor` and
`wwwroot/deck` (both are ignored by git), so build them once before the first run:

```powershell
cd editor;    npm install; npm run build; cd ..
cd webclient; npm install; npm run build; cd ..
Copy-Item editor\dist\*    src\MacroGrid.Host\wwwroot\editor -Recurse -Force
Copy-Item webclient\dist\* src\MacroGrid.Host\wwwroot\deck   -Recurse -Force   # create the folder first if needed
dotnet run --project src/MacroGrid.Host
```

`scripts\publish.ps1` does the bundle builds and the copying for you when it publishes a release (see [release.md](release.md)).

The server starts in the system tray. The tray menu opens the editor, a small test page, and the data folder. Phones connect to
`<the PC's address>:9820`.

### Working on the editor or the deck with hot reload

With the server running, start the Vite dev server of the part you work on; it proxies `/api` and `/ws` to the server on port 9820:

```powershell
cd editor;    npm run dev   # http://localhost:5190/editor/
cd webclient; npm run dev   # http://localhost:5192/deck/
```

`packages/renderer/demo` (`npm run dev`, port 5183) is a small page that shows every widget type, handy for looking at the renderer alone.

### Rebuilding while the server runs

A running `MacroGrid.exe` locks the files in `bin\`, so quit it (tray menu, Exit) before `dotnet build`. The exe serves the editor from its own
`bin\Debug\net10.0-windows\wwwroot\editor` copy, which only `dotnet build` refreshes. After a front-end change: build the bundle, copy `dist` into
`wwwroot\editor`, stop the exe, `dotnet build`, start it again. The server sends `Cache-Control: no-store` for HTML and `/api` responses so the
WebView2 window does not keep showing an old bundle.

The `MSB3277` warning about `WindowsBase` when building the Host project is harmless.

## Tests

```powershell
dotnet test                                   # all server tests
cd packages\renderer; npm test                # renderer tests (Vitest)
cd editor;            npm run typecheck       # type checks (also in webclient and packages/renderer)
```

Be careful when a test sends `widget.down` to a real server: it presses real keys in whatever window is active. Automated tests use a fake
`IInputService`.

When you test the editor or the API by hand, use a separate test profile (**+ Profile**) instead of your real one, and delete it afterwards.

## Data and logs

The server keeps everything in `%AppData%\MacroGrid\` (profiles, paired devices, preferences, plugins, logs; see
[architecture.md](architecture.md#data-model)). The tray menu opens the folder. Logs are in `logs\`.

## Pitfalls

- **Events on a Shadow DOM portal fire twice.** The renderer draws widget content into a shadow root through `createPortal`. Pointer handlers passed
  as JSX props were called twice for one native event. `ShadowHost` attaches pointer events with `addEventListener` inside `useLayoutEffect`; do the
  same for any new DOM event handler on content in the shadow root.
- **Two copies of React.** A package linked with `file:` can end up with its own React copy, and the symptom is a click updating state twice. Every
  Vite config that uses `@macro/renderer` has `resolve.dedupe: ["react", "react-dom"]`; keep it, and keep `node_modules` out of the package.
- **`setPointerCapture` can throw** for a stale pointer id and silently kill the handler. `usePressGesture` wraps it in try/catch; do the same for
  similar pointer capture and release calls.
- **Empty 200 responses.** A `PUT` or `DELETE` that returns `200` with no body makes `res.json()` throw on the client although the request
  worked. The API returns `204`, and the client's `json()` helper does not parse an empty body. Do the same for new endpoints.
- **Unsaved changes.** Anything that replaces the profile in memory (switching profile, creating one, importing) must call
  `confirmDiscardIfDirty()` first (`useEditorState.ts`); there is also a `beforeunload` guard.
- **A CSS grid trap:** do not use `min()` inside `minmax()`; the whole declaration was treated as invalid and the grid collapsed to one column.
- **Keys and names:** a new key name for `core.hotkey` must be added to both `KnownKeys` and `VirtualKeys`; a test checks that they agree.
- **No emoji or text symbols as icons** in the UI; use `lucide-react` (see [ui-guidelines.md](ui-guidelines.md)).
- **UI text** goes through `editor/src/i18n/tr.ts` and `en.ts`, never hard-coded in components. Some server strings (the tray menu and a few
  native dialogs) are not translated yet.
- **The editor's copy of the rule evaluator** (`editor/src/grid/evaluateDynamic.ts`) only drives the live preview. The server's
  `DynamicRuleEvaluator` is what really runs. If one changes, change the other.
- **The renderer exists twice:** here (`packages/renderer`, used by the editor and the browser deck) and in the client repository (used by the
  phone app). The two are deliberately independent. A widget-rendering change that both need must be made in both.
