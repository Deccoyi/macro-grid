# Custom plugin widgets

A plugin can add its own widgets to the Toolbox. The widget's code is one bundled script that runs on the device in a sandboxed Web Worker and draws to a
canvas. It has no network access and no page around it, so a slow, looping or broken widget cannot freeze the deck. Everything a widget knows comes from its
own plugin on the PC, over a small bridge. The author's guide is in the plugin repository (`docs/plugin-authoring.md`, section "Custom widgets"); this note
is the design behind it.

Status: **built and merged** — server, SDK, shared renderer, browser deck, editor preview and the phone app (its own repository) are all done.

## How it runs

```
app page (main thread)             sandboxed frame (opaque origin, CSP)         worker (its own thread)
 cell container -- adds --------->  <canvas>  --transferControlToOffscreen-->   draws, own frame loop
 renderer <============ MessagePort (variables, events, request, run, pointer, ping) ============> macroGrid API
 renderer <-> server: plugin.widget.* over the existing WebSocket (the editor uses HTTP and its own snapshot)
```

For each plugin widget the renderer puts one **sandboxed frame** (`sandbox="allow-scripts"`, `srcdoc`, a strict content policy, opaque origin) into the
cell. The frame makes its own canvas, creates the worker from a `blob:` URL and hands it the canvas and one `MessagePort`. From then on the worker talks
to the renderer directly. Why a frame and not `new Worker` from the app page: a worker made by the app would have the app's origin (its storage and
same-origin requests); a worker made inside an opaque-origin frame inherits that origin and the frame's content policy (`connect-src 'none'`).

Why the canvas is made **inside** the frame: desktop browsers run a sandboxed frame in its own process, and a canvas handed from another process to a worker
stops that worker at its first drawing call. Canvas, frame and worker share one process this way. The phone's WebView is single-process and would also
have worked with a shared launcher, so this is the one model that works everywhere. Removing the frame ends the worker at once. The frame has
`pointer-events:none`: the renderer receives every touch (and in the editor, selecting and dragging still work) and forwards it to the worker.

The shared renderer lives in `packages/renderer/src/widgets/pluginWidget/`: `bootstrap.ts` (the code that runs first in the worker), `launcher.ts` (the frame),
`runtime.ts` (workers, watchdog, limits, the bridge to a host), `PluginWidgetContent.tsx` (the cell). A place that draws widgets gives the renderer a
`PluginWidgetHost` (the browser deck: WebSocket; the editor: HTTP and its variable snapshot).

## What the server does

- **Manifest.** `plugin.json` `widgets` (`PluginWidgetManifest`). The loader checks each widget on its own: safe relative paths inside the plugin folder, no
  links, a script of at most 2 MB (1 MB for a plugin that is not verified), images and fonts at most 4 MB per plugin, at most 16 widgets, `fps` 1 to 60
  (default 15, held to 30 when not verified), known options only, no `Password` or `File` settings. A refused widget is reported in the Error List (`P140`)
  and the rest keeps working.
- **Approval.** Options a widget declares (`keepLoaded`, `storage`) are approved together with a JavaScript plugin's permissions
  (`widget:<id>:<option>` in `plugin-permissions.json`); a C# plugin with such options waits for approval too. An update that adds one waits again.
- **Delivery.** A client that announces the capability `plugin-widgets` gets `props.runtime` on each `plugin-widget` in its layout: the script and images as
  content-hashed assets (`asset:<hash>`, fetched with `asset.get`), the frame-rate cap, the options, the keys of the `Variable` settings and whether the plugin
  is verified, or `unavailable` with a reason (`missing`, `disabled`, `needsApproval`, `incompatible`, `invalid`, `noWidget`). It is never saved: it is
  stripped from saved and imported profiles.
- **Router** (`PluginWidgetRouter`). The plugin and widget are taken from the widget in the profile the device has open, never from the message. Limits:
  16 KB per request, 64 KB per reply or event, 10 requests a second per widget and 30 per device, 4 in flight per widget, 2 seconds for the plugin, 32
  subscribed variables; `ready` and `subscribe` are limited too. A widget reads its own plugin's variables (a JavaScript plugin needs `variables`) and the
  variables the person bound in a `Variable` setting; it runs only its own plugin's actions (a JavaScript plugin needs `actions`); a run without a real
  touch cannot type. Changed variables go out batched every 100 ms, events only to devices showing a matching widget, and the last event per name that the
  plugin marked `retain` is given to a widget that comes on screen later. A request, a run or a subscribe needs the widget to be in the device's profile,
  not on its shown page (a widget that keeps itself loaded must still be reachable).
- **Import.** A profile from someone else loses `Variable` bindings that point outside the widget's own plugin (the person did not choose them).
- **Editor.** `GET /api/plugin-widgets` (the Toolbox list), `.../{plugin}/{widget}/runtime`, `.../assets/{hash}`, `POST .../request` (device id `editor`).
  The preview runs the same worker with `mode: "edit"`; a run is refused and no pointer input goes to the widget.

## Limits on the device

The renderer enforces these; the server does not trust them (it checks every message again).

| What | Limit |
|---|---|
| Live workers | 8 per device, 2 of plugins that are not verified; the rest draw "Paused: too many widgets on this page" |
| Frame rate | the widget's `fps`; the sum over all live widgets is scaled down together above 120 |
| Watchdog | ping every second; no answer for 3 s: the worker is stopped, started once more after 10 s, then waits for the person |
| Busy | over half a core (a quarter when not verified) for 10 s: frame rate halved, then stopped |
| Start | the worker must say it started within 3 s |
| Gesture | one real pointer up on a widget grants one `run` with a key-press window, once, within a second |
| Page | only the shown page's widgets run; leaving a page stops them (a widget with `keepLoaded` is paused instead) |

## Security

The boundary is the opaque origin plus no network plus a validated bridge. Details and the threat table are in
[security-risk-assessment.md](security-risk-assessment.md) (T14).

- The frame's content policy has `default-src 'none'`, `connect-src 'none'`, scripts only from blobs and `'wasm-unsafe-eval'` (no `'unsafe-eval'`).
  `allow-same-origin` is never in the sandbox (pinned by a test). The bootstrap also removes `fetch`, `XMLHttpRequest`, `WebSocket`, `EventSource`,
  `importScripts`, `Worker`, `SharedWorker`, storage and `BroadcastChannel` from the worker before the author's script runs (defence in depth).
- A widget can run out of memory like any web code, and that kills the browser's renderer. The phone app handles the loss so the app itself survives.
- Not offered, by design: network access, cookies, running in the background, camera, microphone, location, clipboard, keyboard input.

## Desktop cost

Every sandboxed frame may be given its own process by the browser. Check in the browser's task manager how many processes a page with several widgets makes and
keep the number of live widgets in the editor in mind when a page has many.
