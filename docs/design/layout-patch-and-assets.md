# Layout patches, cached assets and plugin hot loading

Three related gaps were closed together: a saved edit no longer resends the whole layout, icons and
images are sent once and cached on the device, and plugins can be installed, reloaded and removed while the server runs.

## Protocol

A client opts in with `hello.capabilities` (`ClientCapabilities.cs`). A client that sends none (the browser client in
`webclient/`, older phone builds) keeps getting the plain full layout with everything inline, so nothing breaks.

| Capability | What the client gets |
|---|---|
| `assets` | Every `data:` string of 200+ characters anywhere in a layout is replaced by `asset:<hash>` (first 24 hex chars of the SHA-256 of the value). The client asks for the ones it has not cached with `asset.get { hashes }`, the server answers one `asset { hash, data }` per hash (`data` is null for a hash it no longer holds). |
| `layout.patch` | After an editor save, `layout.patch` instead of `layout.full`, when the client already holds a baseline. |

### `layout.patch`

```
{ profileId, pageId, name?, pageOrder: [pageId...], pages: [ { id, meta?, order?, widgets: [Widget...] } ] }
```

- `pageOrder`: every page id in display order (cheap, and it carries page adds, removals and reordering).
- `pages`: only pages that changed. `meta` is the page's own settings (everything except `id` and `widgets`), present only
  if they changed or the page is new. `order` is the widget id order, present only if widgets were added, removed or moved.
  `widgets` are the added or edited widgets, in full.
- `pageId`: the page the client should show afterwards (the server falls back to the first page when the current one was deleted).
- Nothing is sent when a save changes nothing the client renders.

The server keeps, per session, the layout exactly as the client last received it (`ClientSession.SentLayout`, asset
references included) and diffs the new profile against that (`LayoutDiff`). So a patch is always relative to what that
specific client has, whatever else happened in between. Layout sends for one client are serialized (`LayoutLock`).

After a patch the server clears its per-widget "already sent" state only for the changed widgets and re-sends live state
(text, toggle, value, dynamic style); the client drops the cached live state of the same widgets (`changedWidgetIds`),
so every other widget keeps its text, toggle and slider position.

### Client behaviour (`macro-station-client/src/ws`)

- Messages are handled strictly in arrival order. A layout that references uncached assets waits for them (bounded by 5 s;
  an asset that never arrives only leaves that icon blank). `asset` messages skip the queue, otherwise they would wait behind
  the layout that is waiting for them.
- `assets.ts`: in-memory map backed by `localStorage`, least-recently-used eviction beyond 400 assets.
  `resolveAssetRefs` keeps the identity of every subtree that has no references and remembers fully resolved ones, so a
  patched profile hands React the same object for every untouched widget.
- The layout cache on the device stores the compact `asset:` form; it is resolved when read.
- A patch that does not fit the profile the client holds (a missed message) makes the client close the socket; the reconnect
  brings a full layout.

## Server pieces

- `AssetStore` (Core/Sessions): content-hash keyed LRU (2000 entries); every send touches the assets it uses, so only values no
  layout references any more fall off.
- `LayoutSender`: the one place a layout leaves the server (`SendFullAsync`, `SendUpdateAsync`, `SendAssetsAsync`).
  `ClientHub`, `SessionDeviceController` and `WidgetStateService.BroadcastProfileAsync` all go through it.
- `LayoutDiff`: pure JSON comparison, covered by `LayoutDiffTests`.

## Plugin hot loading

`PluginManager` (Core/Plugins) replaces the one-shot `PluginLoader` and is a hosted service, registered after the variable
provider host.

- **Load:** validates the manifest, loads the assembly into its own `PluginLoadContext`, runs `IPlugin.Initialize`, then applies
  the collected registrations: actions into `ActionDispatcher` (all-or-nothing, an action type clash fails the whole plugin and
  rolls back), variable providers into `VariableProviderHost.StartOwned`, catalog sources into `VariableCatalog`. A failed load
  becomes an `Error` entry, never an exception.
- **Unload:** removes the plugin's actions and catalog sources, cancels its providers (bounded wait), removes every variable
  they set (`TrackingVariableStore`), disposes the plugin instance and everything it registered when they implement
  `IDisposable` / `IAsyncDisposable`, drops its status items, then unloads the assembly context and lets the GC run a few rounds.
  If something inside the plugin still references the context (a thread or timer it never stopped), it stays in memory until
  the next restart; the plugin itself is already fully deregistered.
- **DLLs are loaded from memory** (`PluginLoadContext.LoadPluginAssembly`), so plugin files are never locked and can be replaced
  or deleted while the plugin runs. The cost: `Assembly.Location` is empty inside a plugin, use `IPluginHost.DataDirectory`.
- **Endpoints:** `POST /api/plugins/install` loads immediately (an existing id is replaced, its own files such as
  `settings.json` are kept), `POST /api/plugins/{id}/reload`, `DELETE /api/plugins/{id}` unloads and deletes the folder. If a
  file is still in use the folder is marked with `.uninstall` and removed at the next start.
- **Editor:** the Plugins window has install, reload and remove and never asks for a restart. The main editor refetches the
  action list, variable catalog and icon packs when its window regains focus.

Live style values pushed in `widget.state` (a dynamic icon) are externalized too, per client (`LayoutSender.ForClient`), and the
client resolves the references before applying the state.

## Not covered

- The browser client (`webclient/`) does not announce capabilities yet, so it still receives the full layout with icons inline.
