# Technical reference

## Action types

Built-in types are `core.*`; plugin actions are `<plugin id>.*`.

| Type | Settings |
|---|---|
| `core.hotkey` | `keys`, for example `ctrl+shift+s` |
| `core.typeText` | text |
| `core.open` | `target`, arguments |
| `core.openUrl` | `url` (`http://` or `https://`) |
| `core.delay` | `ms`, up to 60000 (the editor shows Wait in seconds) |
| `core.if`, `core.else`, `core.endIf`, `core.stop` | Logic steps: an If with a `when` setting (`condition`, `previousFailed` or `previousOk`), an Else, its End, and Stop |
| `core.variable.set`, `core.variable.add`, `core.variable.toggle`, `core.variable.reset` | Set variable actions on a `user.<name>` variable, with `value` or `amount` |
| `core.page` | `mode`: go to a page, `next`, `prev`, `back` |
| `core.profile` | profile |
| `core.setVolume` | uses the slider or knob value |
| `core.setMute` | `mute` true or false |
| `core.toggleMute` | none |

OBS plugin: `obs.setScene`, `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode`, `obs.setTransition`, `obs.setProfile`, `obs.startStream`, `obs.stopStream`, `obs.toggleStream`, `obs.startRecord`, `obs.stopRecord`, `obs.toggleRecord`, `obs.pauseRecord`, `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay`, `obs.setMute`, `obs.toggleMute`, `obs.setVolume`, `obs.adjustVolume`, `obs.setItemVisibility`, `obs.setText`.

## Widget types and events

Types in profile files: `button`, `toggle`, `slider`, `knob`, `label`, `image`, `web`, `plugin-widget`, `plugin-html` (`plugin-html` is an old placeholder that only draws an empty box).

| Widget | Events |
|---|---|
| Button | `press`, `release`, `longPress`, `doubleTap` |
| Toggle | `toggleOn`, `toggleOff` |
| Slider, Knob | `valueChange` |

## Plugin setting fields

A plugin settings page is built from fields. The kinds are `Text`, `Password`, `Number`, `Slider`, `Bool`, `Select`, `Segmented`, `File`, `List`, `Button`, `Notice`, `Variable`, `Color`, `Hotkey`, `Duration` and `MultiSelect`. The newest three:

| Kind | What it is |
|---|---|
| `Hotkey` | A key combination, captured by pressing the keys. |
| `Duration` | A time that can be shown in milliseconds, seconds or minutes. |
| `MultiSelect` | Several of the options, each ticked on its own. |

A `Password` field is stored protected for the current Windows user, in a JavaScript plugin as well as a C# one (from the Macro Grid version that added this; see the changelog). `host.settings.get()` gives your script the plain value. The editor never shows it back, and backups leave it out. If the settings are opened by another Windows user or on another PC, the value cannot be read: it comes back empty and the Error List asks the person to enter it again. A value saved by an older version stays readable and is protected on disk the first time it is loaded. Passwords inside the rows of a `List` field are protected and hidden from the editor too; a row keeps its password only while it stays at the same position with its other values unchanged. A `Password` value must be text. The value is bound to the plugin: ciphertext copied from another plugin's settings file cannot be read and comes back empty like any unreadable value.

**Rule:** a plugin that holds a secret must use a `Password` field (a JavaScript plugin's settings page is protected by the host) or `IPluginSecrets` (a C# plugin writes its own settings file, so the host cannot enforce it).

## Button pictures from a plugin

A plugin can supply raster pictures for buttons. The profile never holds the picture, only the reference `plugin-image:<pluginId>/<imageId>` (the image id is 1 to 128 characters of letters, digits, `.`, `_`, `:` and `-`). The host turns the reference into the picture when it sends the layout; a device never sees the reference.

| | JavaScript | C# |
|---|---|---|
| Asked for pictures | `host.images.onRequest(ids => { ... })` | the plugin class implements `IPluginImageSource.RequestImages(IReadOnlyList<string> imageIds)` |
| Hand one over | `host.images.put(imageId, dataUrl, version?)` (returns `true` when accepted) | `IPluginHost.Images.Put(imageId, dataUrl, version = null)` (returns `bool`) |
| Say a picture changed | `host.images.invalidate(imageId, version?)` | `IPluginHost.Images.Invalidate(imageId, version = null)` |

The host asks for at most 20 ids per call and one call per 500 ms, with at most 200 open requests; an id that stays unanswered is asked for again after 10 minutes. A picture is accepted only for an id asked for in the last 10 minutes, as `data:image/png`, `jpeg`, `webp` or `gif` in base64, at most 256 KB decoded, its first bytes must match the type, and its header must say at most 1024 x 1024 pixels (a GIF may have at most 64 frames). The host reads only the header and never decodes or resizes the picture. SVG is not accepted. A refused picture adds one Error List line (code `P151`) and nothing else. The host keeps accepted pictures in the plugin's folder (`.image-cache`, up to 64 MB and 2000 files, least recently used first; not part of backups, diagnostics or the signature check) and shows them while the plugin is not running. A picture that is not cached yet leaves the button's icon out until it arrives. A picture is only fetched when it is used: a button or layout that references it, or the editor with `?request=1`. Looking at a list never asks the plugin. Use stable ids (one per item, not the picture's checksum). A cached picture is always served from the cache; one that is older than 7 days is still served at once and asked for again (once per id per 7 days, at most 10 a hour per plugin, in the same batches); the new picture replaces the old. `invalidate` asks again for a cached picture right away, unless the `version` is the one given to `put`. A version is at most 64 printable characters. The editor reads a cached picture from `GET /api/plugins/{pluginId}/images/{imageId}`, which answers 404 for a missing one and only with `?request=1` also asks the plugin for it.

### Searchable option lists

A `Select` field with an options source can be marked `searchable` (JSON name `searchable`, C# `SettingField.Searchable`). The editor then shows a search box and asks for one page at a time. The field keeps `optionsSource` and `dependsOn` as before.

- **C#:** implement `IOptionsSearch` (it extends `IOptionsSource`): `SearchOptionsAsync(sourceId, currentValues, OptionsQuery query, ct)`. `OptionsQuery` has `Text` (trimmed, at most 100 characters), `Offset` (0 or more), `Limit` (1 to 50) and `Selected` (the stored value, or null). `Offset` is how many options the editor already has. Return the page in `OptionsResult.Options`, set `HasMore` when more match, and on the first page (offset 0) put the `Selected` option first (it counts in the page size and in the offset) and set `SelectedMissing` to true when it is no longer offered. A source that only implements `IOptionsSource` still works: the host filters (case, accents and the Turkish dotted and dotless i are ignored) and pages its whole list.
- **JavaScript:** `host.options.source(id, (query, values) => ({ options: [{ value, label, group?, icon? }], more?: boolean, selectedMissing?: boolean, error?: string }))`. `query` is `{ text, offset, limit, selected }`, `values` the form values named in `dependsOn`. The function answers at once from the plugin's memory (no promise) inside the normal 2 second call budget. A throw, a timeout or an unknown id reaches the editor as the `error` text and does not count towards switching the plugin off. Fields of `registerAction` and `settings.page` take `optionsSource`, `searchable` and `dependsOn`.
- **Endpoint:** `POST /api/actions/{type}/options/{sourceId}` (and the settings and tree variants) take `?q=&offset=&limit=&selected=` next to the unchanged body. Without those parameters the call is the old one and returns the source's whole list.
- **Refresh button:** the editor shows a refresh button after every searchable list. It asks again with `?refresh=1`: C# `OptionsQuery.Refresh`, JavaScript `query.refresh === true`. A source that must re-read its data asynchronously answers at once with its current options and `OptionsResult.Refreshing = true` (JavaScript `refreshing: true`, JSON `refreshing`). The editor then shows a spinner and asks the same question again every 1.5 seconds, at most 8 times, and shows the latest options. Caps (50 options, 256 KB) stay.
- **Limits:** at most 50 options per page (the selected one included) and 256 KB per answer; a larger answer is an error, never the options. Option labels and answers are not written to the log.
- **Pictures:** an option `icon` is a built-in icon name or `plugin-image:<pluginId>/<imageId>` (your own plugin id; image id 1 to 128 characters of letters, digits, `.`, `_`, `:` and `-`). Anything else, a `data:` URL included, is dropped.

### Larger web socket messages

A JavaScript plugin that talks to an app on the same PC can ask for bigger incoming messages in `plugin.json`: `"limits": { "wsMessageKB": 256 }` (64 to 1024, default 64). It only applies to a `ws:` permission for `localhost` or `127.0.0.1`; any other address keeps 64 KB. The person approves it ("Receives large messages from a local app") and can switch it off. Messages over 64 KB also share a budget of 2 MB a second per socket; when it is used up the extra messages are dropped and one line goes to the Error List. Outgoing messages stay at 64 KB.

## Network

| What | Where |
|---|---|
| Server | TCP port 9820, all interfaces; TLS port 9821 (`ServerApp.TlsPort`, the port after 9820) |
| Devices (WebSocket) | `ws://<PC address>:9820/ws`; encrypted: `wss://<PC address>:9821/ws` |
| Browser deck | `http://<PC address>:9820/deck/` |
| Editor | `/editor/`; the `/api` endpoints answer only requests from the PC itself |
| QR code content | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

The protocol, the data model and the security model are in the [architecture document](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md).
