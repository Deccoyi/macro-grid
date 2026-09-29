# The `web` widget plan

Status: built in both repositories (branch `feat/web-widget`) and checked on a current phone (Samsung, Android 16-class WebView) with a test page over `http` and `https`:
the page loads, `window.open`, `top.location`, `alert`/`prompt`, camera, microphone and location are refused, `tel:`, `intent:`, `market:` and `_blank` links open nothing, the
native bridge is not visible to the page, no referrer is sent, and the Example/Reset/Reload/refused-address buttons behave. Still open: step 0 (real chat and alerts links, to be tried
at home), a download check, the old-Android fallback (emulator at most) and whether the login of an embedded site is kept (third-party cookies are not enabled).
Repositories: `macro-grid` (model, renderer, editor, browser deck) and `macro-grid-client` (the phone app's own renderer copy and the Android layer).

**What changed from the plan while building it**
- A widget name does **not** have to be unique. A copy of a widget, a duplicated page and a paste all keep the name, and the action stores the
  widget's id anyway, so the button's dropdown tells two apart by their page ("Chat — Page 2") instead of the server refusing a save.
- "Back to the profile's address" is sent as an empty `url` (`""`), not `null`: the protocol drops null fields, so an empty string is the only way to say it.
- A refused `core.web` address is not silently ignored: the action fails with a message (the phone shows it as the usual button error toast) and logs the host only.
- The placeholder texts of a `web` widget come from the app (`webTexts`, including "web pages are off on this device"); the renderer only has English defaults.
- The editor window's lock cancels every new window, download, external link, permission prompt and script dialog outright (the editor itself uses none of them).
- The import prompt asks on every import that contains web addresses (not only when they changed).
- The "Show web pages" switch defaults to on.
- Checked in a real Chromium: an iframe with exactly these `sandbox`/`allow`/`referrerpolicy` attributes could not open a window, navigate the top page,
  show `alert`/`prompt`, or use geolocation, camera, notifications or clipboard, and sent no referrer. A download click could not be observed from the page.

## Goal

A `web` widget shows a web page inside a grid cell, for example a live chat, a stream dashboard or a status page. Today the type exists
(`WidgetTypes.Web`, a `url` prop, the inspector's `WebFields`) but every renderer draws a placeholder.

The page is **untrusted content from the internet**. It must only be able to show itself inside its cell. It must not be able to open
windows, start downloads, navigate the app away, use the device or reach the server.

## What the embedded page may NOT do

| Blocked | How |
|---|---|
| Open a popup or a new tab (`window.open`, `target="_blank"`) | No `allow-popups` in the iframe `sandbox`. |
| Start a download | No `allow-downloads`. |
| Navigate the whole app (`top.location = ...`) | No `allow-top-navigation` and no `allow-top-navigation-by-user-activation`. |
| `alert`, `confirm`, `prompt`, `beforeunload` dialogs | No `allow-modals`. |
| Lock the screen orientation, take the pointer, start presenting | No `allow-orientation-lock`, `allow-pointer-lock`, `allow-presentation`. |
| Use camera, microphone, location, clipboard, payment, USB and other device features | `allow=""` (empty permissions policy). |
| Tell the site which Macro Grid server the user has | `referrerpolicy="no-referrer"`. |
| Load a scheme other than web pages | The URL must be `http:` or `https:`; `javascript:`, `data:`, `blob:`, `file:` and others are refused. A URL with a user name or password in it is refused too. |
| Read the app's own data (variables, the WebSocket, storage) | The page is cross-origin. A URL that points at the server itself, or at the origin the renderer runs on, is refused (so it can never be same-origin with the app). No `postMessage` bridge exists in this widget (that is the separate `plugin-html` widget). |

`allow-scripts` and `allow-same-origin` stay on (most pages do not work without them; `allow-same-origin` only means the page keeps its
own origin, not ours), and `allow-forms` so a page's own input works.

**Second lock on the phone.** The iframe rules above are the first lock. The phone app's Android layer gets a second one, so a bug or a
missing browser feature cannot open a hole: every permission request that comes from a page (location, camera, microphone, notifications,
storage access) is answered "no" without a prompt, a request to open a new window is dropped, and every download is dropped. This is
checked on a real phone with a small test page that tries each of these (`window.open`, a `target="_blank"` link, a download link, `geolocation`,
`getUserMedia`, `Notification.requestPermission`, `top.location`, `alert`, `prompt`), in the editor, the browser deck and the phone app. The
docs list only what passed.

The same rules apply in **every** place that draws the widget, in the same words: the editor, the browser deck, the phone app. They are
written once as a small function per renderer (`isSafeWebUrl` next to the existing `isSafeImageUrl` in the client repository) and once
in the server model so a profile with a bad URL is refused when it is saved or imported, not only when it is drawn.

## Security review (2026-09-29)

A review of this plan against how the server, the editor window and the phone app work today. The iframe `sandbox` above is sound as the
first lock, but several native defaults sit behind it and would let a page through if the first lock ever failed. Some of them are
holes on their own. Each item below says what is wrong today and what the plan adds (the fix). The table first, the details after it.

### Summary

Risk: 5 = a page could take over the app or the PC, 1 = a small nuisance. Fixability: 5 = small and certain, 1 = cannot be fixed by
this project (only documented or limited). Every item has a fix or a limit written below; none of them blocks the widget.

| # | Where | Hole | Risk | Fixability | Fix in one line |
|---|---|---|---|---|---|
| 1 | Phone | Native bridge visible to iframes on an old system WebView (fallback mode) | 5 | 4 | Detect the fallback natively; if active, web widgets are off on that device. |
| 2 | Phone, all | A `localhost`/loopback URL would be same-origin with the phone app | 5 | 5 | Refuse every loopback form on the normalized URL, in every renderer and in the server model. |
| 3 | Phone | A link or script in the page can start another app or the system browser | 4 | 4 | Own page-load hook: in an iframe only `http`/`https` loads in place, everything else dropped. |
| 4 | Phone | Camera/mic/other permission requests are granted by default; dialogs, file picker, location open | 4 | 5 | Native handlers answer "no" to every request that does not come from the app's own origin. |
| 5 | PC editor | New windows, downloads, external protocol launches, permission prompts are all at their defaults | 3 | 5 | Handlers in `WebViewEnvironment` cancel them for every frame that is not the editor's own. |
| 6 | Server | `/api` does not check `Host`, so a DNS-rebinding page can read it (exists today) | 4 | 5 | Refuse `/api` unless `Host` is a loopback name with the server's port. |
| 7 | Server | `/ws` does not check `Origin` (a page can only guess the PIN, already rate-limited) | 2 | 5 | Refuse a browser `Origin` that is not the deck's or the phone app's. |
| 8 | Phone | One renderer process for app and iframes: a slow page freezes the grid; an engine bug lands in the app's process | 3 | 2 | Mount only on the shown page and in the foreground; per-device "Show web pages" switch; document the limit. |
| 9 | All | An imported or shared profile can point the phone at any site | 3 | 4 | On import, list the sites and ask once; "no" clears those URLs. |
| 10 | All renderers | `sandbox`/`allow` changed after creation would not apply until the next load | 2 | 5 | Set them once at creation, never change them; a test checks the element. |
| 11 | All app pages | No content policy limits what may be framed | 2 | 4 | Add `frame-src http: https:` to the app pages' content policy. |
| 12 | PC editor | Many live pages in the editor cost CPU/RAM next to games and streaming | 2 | 5 | Only the open page's web widgets are live; others show the placeholder. |
| 13 | Phone | Embedded sites' cookies and storage stay on the phone (for example after a token link changed) | 2 | 5 | A "clear web page data" button in the phone app's settings. |
| 14 | Server, logs | A logged URL may leak a secret token | 2 | 5 | Log the host name only, never the full URL. |
| 15 | Phone | `http:` pages can be changed on the way on an untrusted network | 2 | 1 | Allowed by decision; the locks above still hold; the inspector warns. |

**1. Phone: the native bridge must never reach the page (high).** The phone app's web layer talks to its native code (self-update,
kiosk, the pinned socket, gesture exclusion) through a bridge object. On a current Android WebView that bridge is registered only for
the app's own origin (`https://localhost`) and only for the top frame, so a cross-origin iframe cannot see it. But the app framework
**falls back** to an older way of registering it when the WebView lacks the "web message listener" feature (an old, never-updated
system WebView, some tablets without the store). In that fallback the bridge is visible in **every frame, iframes included**, with no
origin check, so a page in a web widget could call every native method of the app.
Plan: a small native method reports whether the safe bridge mode is active. If it is not, the phone app does **not** load any web widget
and shows the placeholder "web pages are turned off on this device: its system WebView is too old". Also: the app config must never get
an `allowNavigation` list (it widens the bridge's allowed origins); a comment in the config and a test that reads it say so.

**2. Phone: the app's own origin and every loopback address are refused (high).** The phone app runs at `https://localhost`, served by
the app itself. A web widget URL with host `localhost` would load **the app's own origin** inside the iframe, so the page would be
same-origin with the app, could read its storage (the device token) and, with the bridge registered for that origin, reach native code.
The URL rule therefore refuses, on every renderer and in the server model: `localhost` and any `*.localhost`, `127.0.0.0/8`, `0.0.0.0`,
`::1`, `::`, IPv4-mapped IPv6 loopback (`::ffff:127.x.x.x`), and a host with a trailing dot (`localhost.`). The check runs on the
**parsed and normalized** URL (the URL parser turns `0x7f.1` and `2130706433` into `127.0.0.1`), never on the raw text. The server's
own addresses (its LAN IPs, the machine name) are refused too, as the plan already says. Note: an address that only *resolves* to the
server under another name has a different origin and so stays cross-origin; the server's own guards (items 6 and 7) cover that case.

**3. Phone: links inside the page must not start other apps or leave the app (high).** The framework's page-load hook sends every
navigation it sees to a host that is not the app's own **out of the app**: it opens the system browser or whatever app is registered
for the link (`tel:`, `sms:`, `mailto:`, store links, other apps' deep links). Android may call this hook for iframe navigations too,
and the sandbox does not stop a frame from navigating **itself**. So a click (or a script) inside the page could open another app.
Plan: the phone app installs its own page-load hook in front of the framework's: a navigation that is **not** in the top frame loads in
place only when it is `http`/`https`, and anything else is dropped. It never starts another app. The top-frame behaviour stays as it is.
The device test page gets a `tel:` link, a store link, an `intent:` link and a plain link, each tried by click and by script.

**4. Phone: every native prompt says "no" (medium).** The framework's defaults are open: a page's camera or microphone request is
granted when the app already holds that permission (it holds the camera for QR pairing), any other media request is granted without
asking, `alert`/`confirm`/`prompt` open native dialogs for any frame, a file input opens the system file picker, location is switched on
and windows may be opened by script. The iframe's `sandbox` and empty `allow` block all of these first. The second lock overrides them
natively: permission requests are denied unless they come from the app's own origin (the app itself uses none today), dialogs from any
other origin are dropped, the file picker is refused for pages not from the app's own origin (the app has no file input today, so it can
simply refuse all), location is switched off, a new-window request returns "no", and downloads are dropped.
QR pairing is not affected: the QR screen opens the camera through a native scanner layer, not through a web-page camera request, so
the Android camera permission of the app stays as it is. Only the WebView's "a page asks for the camera" path answers "no". The device
test checks that QR pairing still works after this change.

**5. PC: the editor window needs the same second lock (medium).** The editor runs in the server's own embedded browser window, on the
PC, next to the editor API. Today that window has no handlers at all, so its defaults apply: a new-window request opens a **new browser
window**, a download goes to the Downloads folder with the browser's download bar, a link with an external scheme asks to **start a
program registered for it** (any installed protocol handler on Windows), and a permission request shows a prompt. The iframe sandbox
blocks the first two, but the rest rely only on the sandbox too.
Plan (`WebViewEnvironment`): for every frame that is not the editor's own origin, cancel new-window requests, downloads and external
scheme launches, deny permission requests, and suppress script dialogs. The editor's own features keep working (check which ones use a
download or a dialog, for example a profile export, before cancelling those for the top frame). The same test page is run in the editor.

**6. Server: refuse foreign `Host` headers on `/api` (medium, exists today).** The editor API trusts a request from this PC when its
`Origin` header is the editor's own **or missing**. A page that uses DNS rebinding (its own name first points at its server, then at
`127.0.0.1`) makes its requests same-origin in the browser, so the browser sends no `Origin` on a `GET`, and `/api` answers it (the pairing
PIN, profiles, plugin lists). Browsers guard against this in part, but the server should not depend on it. The hole exists without this
widget (any tab in the person's own browser can try it), but a web widget keeps an untrusted page open in the editor for hours, which is
exactly what such an attack needs. Plan: a middleware that refuses `/api` unless `Host` is `localhost`, `127.0.0.1` or `[::1]` with the
server's port (and the dev ports), with tests. This is a small change, so it goes in **before** the widget ships.

**7. Server: check `Origin` on `/ws` too (low).** The WebSocket needs a device token or the pairing PIN, and a page cannot read the
token, so a page on the phone or in the editor could at most guess PINs while the Pairing window is open (already rate-limited and
blocked after a few wrong tries). As defence in depth `/ws` refuses a browser-sent `Origin` that is neither missing, the server's own
origin (the browser deck) nor the phone app's (`https://localhost`, `http://localhost`). Such a refusal is logged as a security event.

**8. Phone: one process, one thread (known limit, documented).** Android WebView runs every page of the app, iframes included, in one
renderer process and does not isolate sites. Two results: a bug in the browser engine that a page exploits lands in the same process as
the app (only a current system WebView helps; the app cannot fix it), and a page that loops or does heavy work **slows or freezes the
whole grid**, buttons included. Plan: the iframe is only mounted while its page of the grid is shown and while the app is in the
foreground (unmounted otherwise, which also keeps battery and memory low); the docs say "a slow page slows the deck". A per-device switch
"Show web pages" in the phone app's settings (default on) lets someone turn every web widget off on that device without touching the
profile; a turned-off widget shows the placeholder.
A real fix would be a second WebView in its own app process, drawn over the cell. The owner decided against it (see "Decided"): the
widget stays an iframe with the mitigations above, and choosing well-behaved sites is the person's part.

**9. Imported and shared profiles (medium).** A profile from someone else may carry a web widget or a `core.web` button that points at
any site, and the phone would open it the moment the profile is used. Plan: when a profile with web URLs is imported, the editor lists
the sites (host names) and asks once whether to keep them. If the person says no, those URLs are cleared (the widgets stay, empty).
This also shows the person when an imported profile contains a link with a token.

**10. Sandbox set once (low).** The `sandbox` and `allow` attributes are set when the iframe is created and never changed afterwards (a
change only applies on the next navigation). A test checks the rendered element.

**11. Content policy for frames (low).** The app pages (editor, browser deck, phone app) get a `frame-src http: https:` content policy,
so even a renderer bug cannot put a `javascript:`, `data:` or `blob:` document into a frame. Check that the existing pages still load
with it.

**12. Editor preview cost (low).** The editor preview costs CPU and memory on the PC, which runs next to games and streaming software.
Only the web widgets on the page that is open in the editor are live; the other pages show the placeholder with the host name.

**13. Stored site data (low).** The phone app keeps the embedded sites' cookies and storage. A "clear web page data" button in the
phone app's settings removes them (for example after a stream link with a token was changed).

**14. Secrets in logs (low).** A refused URL (at save, at import, from `core.web`) is logged once with the host name only, never the
full URL, because the URL may hold a secret token.

**15. `http:` pages (low, accepted).** `http:` pages are allowed (decided). On the phone the app allows mixed content for its own
reasons, so an `http:` page on a public network can be changed on the way; the locks above still hold, and the inspector already warns.

## Design

**Model (`macro-grid`, Core).** `props.url` (string). Validation on save/import: `http`/`https` only, no credentials, length at most 2048,
host not the server's own address. Refused values are dropped with a warning, the widget keeps working as an empty cell. Tests in
`MacroGrid.Tests`.

**Renderer (`packages/renderer`, both repositories, independent copies).** `WebContent` replaces the placeholder in `WidgetView`: an
`<iframe>` with the attributes above, `loading="lazy"`, the cell's size, `pointer-events` on only in run mode. Only while there is
no URL, or the URL is refused, it shows a quiet placeholder that says why. Tests with vitest for the URL check and the attributes.

**Editor.** The inspector keeps `WebFields` and rewrites its note into a visible **warning box** right under the URL field (always
shown, not hidden in a tooltip): *"This page runs on your phone. Only use sites you trust."* followed by what is blocked (popups,
downloads, leaving the app) and the limits below. Because the widget runs on the phone, not on the PC the person is editing on, the
warning says so. An `http:` URL gets an extra line that the connection is not encrypted. The editor's canvas shows the **real page**, the same as the phone will, so the person sees what they are building. While editing, `pointer-events` are off so
selecting and dragging a widget still work. (The editor window loads the page from the PC, so the site sees a request from there too; that is
the price of a real preview and the inspector's warning covers it.)

**Phone app (`macro-grid-client`).** Its own renderer copy gets the same `WebContent` and URL check, plus a check that Android's
WebView does not open a second window or start a download when the page tries (the iframe `sandbox` already blocks both; this is
verified on a device, not assumed).

## The main use: a streamer's chat and alert panels

The owner's example: a streamer puts the link of their **live chat** or their **alerts/notifications panel** (the overlay pages that
stream tools give as a link) in a cell, so the phone shows chat and alerts while streaming. This shapes the design:

- These links are `https:`, made to be embedded, and read-only or nearly so. That is the case the iframe handles well.
- **The link often carries a secret.** An alerts panel link usually has a personal token in it; anyone with the link can see (and
  sometimes control) the panel. The URL lives in the profile file like any other widget property, so the inspector warning also says
  "this link may contain a secret; do not share the profile or a screenshot of it". Nothing is hidden or encrypted in the first version.
- **Chat services want to know where they are embedded.** Some (for example live-stream chat embeds) require the embedding site's
  domain as a `parent=` (or similar) parameter in the URL. The phone app and the browser deck are not a normal website, so this has to be
  **tried with the real services first** (step 0 below) before promising it. If a service refuses, that is documented per service as
  "does not work in a web widget"; there is no native WebView fallback (see "Decided").
- **Sending a chat message needs a login**, and a login needs cookies of the embedded page, which are third-party cookies here and often
  blocked. So the first version is for **reading** chat and alerts; whether the person can also type is checked in step 0.
- An alerts panel may play sounds. The iframe gets no autoplay permission, so alert sounds stay silent on the phone; that is intended
  (the PC already plays them).

## Changing the page from a button: a named widget and an action

**A name for the widget.** `Widget` gets an optional `name` (today a widget only has an id and its display text). It is shown in the
inspector for `web` widgets, must be unique among the widgets of a profile (the editor adds a number when a copy would clash), and is
what the person picks in the action's dropdown. The action stores the widget's **id**, not the text of the name, so renaming a widget
does not break the buttons that point at it. Deleting the widget leaves the action with a "widget missing" marker in the editor, never a
crash.

**The action `core.web`** (category "Widgets", built in like `core.page`). Settings: `{ "widgetId": string, "mode": "set" | "reset" | "reload", "url"?: string }`.

| Mode | What the device does |
|---|---|
| `set` | Shows `url` in that web widget. Example: a button "Chat" sets the widget to site X, a button "Stats" sets it to site Y. |
| `reset` | Goes back to the URL saved in the profile. |
| `reload` | Loads the current page again. |

The inspector of a button shows the target as a dropdown of the profile's named web widgets and a URL field with the same warning box as
the widget itself ("only sites you trust, this runs on your phone").

**How it reaches the phone.** It is per device, like a page change: the action acts on the device that pressed the button
(`ActionContext.Device`), not on every phone. The server keeps the override in memory per device and sends it with the existing
`widget.state` push as one new optional field, `url` (a string, or `null` for "back to the profile's URL"; `reload` is a small counter
field so the same URL can be loaded again). Old clients ignore unknown fields, so this is additive and does not raise the protocol
version. The override is not written to the profile. It lasts until the device switches profile, the profile is reloaded, or the
server restarts; a phone that reconnects is sent it again.

**The action is checked by the same URL rule** as the widget's own `url` (`http`/`https` only, no credentials, length cap, not the
server's own address), on the server when the action runs and again in the renderer before it draws. A refused URL is logged and
ignored, the widget keeps what it shows. Variable templates in the URL (for example `{stream.channel}`) are not supported in the first
version; the person picks a fixed address.

**Plugin actions.** `core.web` is a core action, so plugins do not get a way to point a widget at any URL. A plugin that wants that
would use the future `plugin-html` widget instead.

## Limits, to write in the docs and in the inspector's note

- A page that forbids being framed (`X-Frame-Options`, a CSP `frame-ancestors`) cannot be shown in an iframe and the browser gives the
  app no way to tell; the cell just stays blank. Use a page or a URL made for embedding (many chat and dashboard services offer one).
- Cookies of an embedded page count as third-party cookies, which current browsers and Android WebView often block, so a page that
  needs a login may not keep it.
- A page served over `http://` cannot be shown inside the browser deck when the deck itself is opened over `https://` (mixed content).
- Nothing in the widget refreshes the page; the page updates itself.

## Steps

(Done in code: 0a, 1, 2, 3, 3a, 4, 5, 5a and 6 except the open items marked below. Open: step 0 and the device checks of 5a.)

0a. Server, before the widget: the `Host` check on `/api` (review item 6) and the `Origin` check on `/ws` (item 7), with tests. They
   close a hole that exists today, so they may ship on their own.
0. Before any code: try a real live-chat embed and a real alerts panel link in a plain iframe with the sandbox above, on the phone
   app and in the browser deck, and write down what works and what each service needs (parameters, login). The result decides what the
   docs promise.
1. `macro-grid`: URL rule and validation in Core, with tests.
2. `macro-grid`: `WebContent` and `isSafeWebUrl` in the renderer, used by the editor and the deck; tests.
3. `macro-grid`: inspector warning box ("only trusted sites, this runs on your phone"), the live page in the editor canvas, translations (en, tr).
3a. `macro-grid`: the editor window's second lock (review item 5), the import prompt for web URLs (item 9), the `frame-src` policy (item 11)
   and live preview only on the open page (item 12). The URL rule with every loopback form (item 2), the sandbox set once (item 10) and
   host-only logging (item 14) go into steps 1 and 2.
4. `macro-grid`: `Widget.name`, the `core.web` action, the `url` field of `widget.state`, and the inspector parts (name field, the
   action's editor); tests for the action, the per-device override and the URL check.
5. `macro-grid-client`: the same in its own renderer copy, and reading `url` from `widget.state`; check on a phone that popups and downloads stay blocked.
5a. `macro-grid-client`, native: the safe-bridge check that turns web widgets off on an old WebView (review item 1), the own page-load
   hook for iframe navigations (item 3), the "no" to every prompt (item 4), the "Show web pages" switch and "clear web page data" button (item 13)
   and mounting only on the shown page (item 8). Run the full test page on a current phone **and** on the oldest Android the app supports.
6. Docs: `architecture.md` (also the new `widget.state` field and the `core.web` action), the roadmap line, `CHANGELOG.md` in both repositories, and a `docs/design/` note for the action.

## Decided (by the owner)

- Both `http:` and `https:` are allowed (`http:` for a dashboard on the local network); the inspector shows a small warning next to an
  `http:` URL.
- The `core.web` action changes only the phone that pressed the button, like a page change.
- **Iframe only, no second WebView (2026-09-29).** No native WebView over the cell, neither for pages that refuse framing nor for
  process isolation (review item 8). The widget is an iframe with every fix of the security review that is possible; the remaining
  limits (one renderer process on the phone, pages that refuse framing) are documented, and the person is responsible for opening
  well-behaved sites. The inspector warning and the docs say so.

## Open questions

- Should the override survive a server restart? Built as no (it is a live view state, not a setting).
- Should the widget have an optional reload interval (`props.refreshSeconds`) for pages that do not update themselves? Leave out until
  someone needs it.
