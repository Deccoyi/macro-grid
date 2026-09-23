# OBS plugin v0.2 + plugin UI altyapısı (status bar, aksiyon seçici, şema formları)

## Context

The OBS plugin (`macro-station-plugins/OBS`, v0.1.0) works, but it has five problems:
- There is no place to see live state.
- Reconnect has real bugs: it can hang, it never receives mute events, and a wrong password makes it retry forever.
- It polls wastefully. OBS's "messages in/out" counter grows by about 8 messages every second.
- Action settings are a raw JSON textarea.
- The editor's "Aksiyon ekle" button just appends the first action in the list.

The user asked for five things:
1. A window-wide status bar where plugins can show state.
2. Solid reconnect handling.
3. Real dropdowns in action forms: scenes, audio inputs, and scene items including items inside groups.
4. A categorized action picker dialog.
5. More OBS variables.

They also asked for plugins to render their own settings window. The chosen approach is a **schema-driven form**: the plugin declares its fields in C# and the host draws the form. The same schema also draws action forms. There is a single OBS connection.

Design reference: an open-source deck app's OBS plugin (MIT/Apache) is used for ideas only. **No code is copied, and its name never appears in commits, comments, docs or the changelog.**

Coordination: a parallel "icon pack" session also needs `IPluginHost.RegisterIconPack(IIconPackSource)`. We agreed on one SDK bump to **0.3.0**:
- This session owns the edits to the shared files: `IPlugin.cs`, `PluginSdk.cs`, `PluginHostCollector.cs`, `PluginLoadResult`, and the SDK line in `versioning.md`.
- That session writes `IIconPackSource.cs` and its own host and editor parts.
- Before touching a shared file, and after each commit, send a message to the icon-pack session.

All work happens on the `dev` branch in both repos.

---

## Step 1 — Plugin SDK 0.3.0 (`macro-station/src/MacroStation.Plugin.Abstractions/`)

All changes are additive, so existing plugins still compile. `PluginSdk.Version` becomes `"0.3.0"`. Because of the 0.x caret rule, a plugin with `sdkVersion ^0.2.0` stops loading, which is expected; OBS moves to `^0.3.0`.

New files:
- **`SettingField.cs`**
  - `record SettingField(string Key, string Label, SettingFieldKind Kind)` with these init properties:
    - `Description`, `Placeholder`, `Default` (JsonNode)
    - `Min`, `Max`, `Step`
    - `Options` (a static `SettingOption[]`)
    - `OptionsSource` (a string id for dynamic options)
    - `DependsOn` (string[]: the keys whose current values are passed to the options query)
    - `AllowVariables` (bool: shows the `{var}` insert button)
    - `VisibleWhen` (a key=value condition)
  - `enum SettingFieldKind { Text, Password, Number, Slider, Bool, Select, Segmented }`
  - `record SettingOption(string Value, string Label, string? Group = null, string? Icon = null)`. `Group` is used for things like "Grup › Öğe" indentation.
  - `record OptionsResult(IReadOnlyList<SettingOption> Options, string? Error)`. `Error` carries messages such as "OBS'e bağlı değil".
- **`IActionDescriptor.cs`**: an optional side interface an `IActionHandler` may implement:
  - `string Category`, `string? Description`, `string? Icon` (a lucide name)
  - `IReadOnlyList<SettingField> Fields`
- **`IOptionsSource.cs`**:
  - `Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken ct)`
  - Implemented by an action handler or a settings page, and used for dynamic dropdowns.
- **`IPluginSettingsPage.cs`**:
  - `IReadOnlyList<SettingField> Fields`
  - `JsonObject Load()`
  - `void Save(JsonObject values)`
  - It can also implement `IOptionsSource`.
- **`IPluginStatusItem.cs`**:
  - `void Update(string text, StatusLevel level, string? icon = null, string? tooltip = null)`
  - `enum StatusLevel { Idle, Ok, Busy, Warning, Error }`

The icon-pack session's agreed signature: `IIconPackSource { string Id; string DisplayName; IReadOnlyList<string> IconNames; string? GetIconSvg(string name); }`. That session creates the file. `PluginLoadResult.IconPacks` is `IReadOnlyList<IIconPackSource>`.

**Step 0:** before any code, run `/commit-all` on all three repos (on `dev`), after confirming with the icon-pack session that it has no uncommitted files.

`IPluginHost` gains:
- `RegisterSettingsPage(IPluginSettingsPage)`
- `IPluginStatusItem CreateStatusItem(string id)`
- `RegisterIconPack(IIconPackSource)` (the other session's interface)

`IVariableStore` gains `void Remove(string name)`. Without it, variables for deleted or renamed OBS inputs would pile up forever.

## Step 2 — Host (`macro-station/src/...`)

- **`PluginHostCollector` / `PluginLoadResult`:** collect settings pages, status items and icon packs per plugin id.
- **New `Core/Plugins/PluginStatusRegistry.cs`:**
  - Thread-safe list of `{pluginId, id, text, level, icon, tooltip, updatedAt}`.
  - `PluginStatusItem` writes into it.
  - It also holds core items: the server version and the connected-device count (from `ClientHub`).
- **`VariableStore.Remove`:** removes the value and raises `Changed`.
- **Built-in actions** (`BuiltInActions.cs`, `PageActions.cs`, `SystemActions.cs`, `AudioActions.cs`) implement `IActionDescriptor`, giving `Category` and `Icon` only: Klavye / Sayfa & Profil / Sistem / Ses. Their existing hand-written forms stay.
- **`ServerApp.cs` endpoints:**
  - `GET /api/actions` returns `{type, displayName, category, description, icon, pluginId, fields}`. `fields` is null for built-ins that have a custom form.
  - `POST /api/actions/{type}/options/{sourceId}` takes the current settings as its body and returns an `OptionsResult`. Timeouts and exceptions come back as `Error`, never a 500.
  - `GET /api/plugins` gains `hasSettings`.
  - `GET /api/plugins/{id}/settings/schema`, plus `GET`/`PUT /api/plugins/{id}/settings`. These go through `IPluginSettingsPage` when one is registered; the old raw pass-through stays as the fallback.
  - `POST /api/plugins/{id}/settings/options/{sourceId}`
  - `GET /api/status` returns the status items.
  - `POST /api/windows/plugin-settings/{id}` opens a `ToolWindow` at `?window=plugin-settings&id=…`, reusing the existing `IUiWindowService` and `ToolWindow.cs` pattern.

## Step 3 — Editor (`macro-station/editor/src/`)

- **`components/StatusBar.tsx`:**
  - Goes in as a new last row of the `App.tsx` root grid. The template changes to `auto auto 1fr auto`, and the error banner moves inside the main area so the rows stay fixed.
  - Core items sit on the left and plugin items on the right. Each item shows a lucide icon, text and a colored level dot, with the tooltip on hover.
  - Clicking a plugin item opens that plugin's settings window.
  - `useEditorState.ts` fetches `GET /api/status` in the existing 2-second `refreshVariables` interval.
  - Styling follows `docs/ui-guidelines.md`, with no emoji.
- **`panels/ActionPicker.tsx`:**
  - Built on `PickerShell` + `usePickerFilter` + `usePickerOpenState`, the same pattern as `VariablePicker.tsx`.
  - Categories come from `ActionInfo.category`, with counts as badges.
  - Search matches the name and the description.
  - Rows show the icon, name and description.
  - Used in two places in `ActionEditor.tsx`:
    - "+ Aksiyon ekle" opens the picker instead of appending `actions[0]`.
    - Each binding's `<select>` is replaced by a button showing the current action's name, which opens the picker to change the type.
- **`panels/actionForms/SchemaForm.tsx`:**
  - A generic renderer for `SettingField[]`, used for action settings and plugin settings.
  - For a `Select` with `OptionsSource`, it fetches options when mounted and whenever a `DependsOn` value changes (for example, the scene item list refetches when the scene changes).
  - It shows a refresh button and the `Error` text.
  - A saved value that is no longer in the list stays visible as "(bulunamadı)", so it is not lost.
  - `AllowVariables` text fields get the `VariablePicker` trigger.
  - `formFor(type, actionInfo)` in `forms.tsx`: hand-written form first, then `SchemaForm` when `fields` exist, then `GenericJsonForm` as the last fallback.
- **`windows/PluginSettingsWindow.tsx`:**
  - Routed via `main.tsx`: `SchemaForm`, a Save button, and the plugin's live status item in the header.
  - `PluginsWindow.tsx` shows the gear for any plugin with `hasSettings`, which opens that window.
  - `ObsSettingsInline`, `getObsSettings`/`saveObsSettings`, `ObsPluginSettings` and the `plugins.obs.*` i18n keys are deleted, so the host no longer has any OBS-specific code.
- **`api/types.ts` / `client.ts`:** new types and calls; i18n keys added to `tr.ts` and `en.ts`.

## Step 4 — OBS plugin 0.2.0 (`macro-station-plugins/OBS/src/`)

`plugin.json`: `version` 0.2.0, `sdkVersion` `^0.3.0`.

### 4a. Connection rewrite. Bugs found and their fixes

| # | Bug in v0.1.0 | Fix |
|---|---|---|
| 1 | `ObsEventSubscription.Inputs = 1<<4` is actually **Transitions**. Inputs is `1<<3`, so `InputMuteStateChanged` etc. never arrive. | Use the correct bit values. Subscribe to General, Config, Scenes, Inputs, Transitions, Outputs, SceneItems and Ui. Never subscribe to the high-volume events (1<<16 and up). |
| 2 | No timeouts. The handshake and every poll use `CancellationToken.None`, so a frozen OBS or a half-open TCP connection hangs `RunAsync` forever while the status says "connected". | Connect and handshake time out after 5 s; each request times out after 5 s. Set `KeepAliveInterval` and `KeepAliveTimeout` on `ClientWebSocket` to 10 s. Two poll timeouts in a row drop the connection. |
| 3 | Race: a request added to `_pending` after the receive loop has ended waits forever. | Add a `_closed` flag, checked after registering the request; a closed connection fails fast. |
| 4 | Race: `Disconnected` can fire before `ObsConnection` subscribes, so the disconnect is missed. | Replace the event with a `Task Completion`, which is safe to await at any time. |
| 5 | Settings saved while connected are ignored until the next drop, and `Enabled=false` does not disconnect. | `ObsSettingsPage.Save` fires a "settings changed" signal that cancels the current session. The loop reconnects at once and the backoff resets. |
| 6 | A wrong password (close code 4009), or codes 4010/4012, retries forever and floods the log. | Read `CloseStatus`. For 4009, 4010 and 4012, stop retrying until the settings change; the status bar shows "Şifre hatalı". Other codes retry with backoff from 2 s up to 30 s, plus jitter. |
| 7 | Every failed attempt is logged (the same line every 30 s). | Log only when the state changes (connected, lost, or a new kind of error). |
| 8 | Each received message allocates a new 16 KB buffer and a MemoryStream. | Reuse one buffer for the life of the connection. |
| 9 | `GetValue<int>` throws on unexpected JSON types, which can kill the receive loop. | Add safe `TryGetInt/Bool/String` helpers. A bad message is skipped, not fatal. |
| 10 | On OBS shutdown the plugin waits for TCP to fail. | On `ExitStarted`, close cleanly and retry later. |
| 11 | On disconnect only the three booleans are reset; duration, fps and scene keep stale values. | Reset every `obs.*` value, and remove the dynamic ones with `Remove`. |

State machine (`ObsConnectionState`): Disabled → Connecting → Connected → Reconnecting(in Ns) / AuthFailed / Error. Each state change updates the status item:
- Connected: `OBS · 60 fps · ● 01:23:45`
- Connecting: `OBS · bağlanıyor…`
- AuthFailed: `OBS · şifre hatalı`
- Disabled: `OBS · kapalı`

It also updates `obs.connected` and `obs.status`.

### 4b. Why "messages in/out" keeps growing, and the fix

OBS shows cumulative per-session counters. v0.1.0 sends **4 sequential requests every second**, about 8 frames/s and roughly 690 k frames a day. That is not a leak, but it is waste. The fix has three parts:
- Send **one `RequestBatch` (op 8) per tick**, containing `GetStreamStatus`, `GetRecordStatus` and `GetStats`, so there is one frame in and one out.
- Tick every 1 s only while streaming or recording (the duration and timecode need it). When idle, tick every 5 s.
- Scene, input, profile, studio mode, virtual cam and replay buffer changes come **only from events**. They are fetched once in full on connect, not polled.

The receive path keeps no growing state: `_pending` is always cleaned up in `finally`, and the caches are rebuilt on reconnect. Session message counts are exposed as `obs.ws.in` / `obs.ws.out` for debugging.

### 4c. `ObsState` cache (new `ObsState.cs`)

It is filled on connect with one batch: `GetSceneList`, `GetInputList`, `GetSpecialInputs`, `GetProfileList`, `GetSceneCollectionList`, `GetStudioModeEnabled`, `GetCurrentSceneTransition`, `GetVirtualCamStatus`, `GetReplayBufferStatus`. After that:
- `GetSceneItemList` runs for each scene.
- For items with `isGroup`, `GetGroupSceneItemList` runs recursively, with a visited set and a depth limit of 8.
- An input counts as an audio input when `inputKindCaps & 2` (`OBS_SOURCE_AUDIO`) is set. If that field is missing (older obs-websocket), a batch of `GetInputMute` probes decides instead.

Events update the cache incrementally:
- Scenes: `SceneListChanged`, `SceneCreated/Removed/NameChanged`
- Inputs: `InputCreated/Removed/NameChanged`, `InputMuteStateChanged`, `InputVolumeChanged`
- Scene items: `SceneItemCreated/Removed/EnableStateChanged`
- Everything else: `CurrentProgram/PreviewSceneChanged`, `StudioModeStateChanged`, `CurrentProfileChanged`, `CurrentSceneCollectionChanged` (which triggers a full resync), `CurrentSceneTransitionChanged`, and `Stream/Record/Virtualcam/ReplayBufferStateChanged`

Every option list is served from this cache, so opening an editor dropdown does not wait on OBS.

### 4d. Actions (with `IActionDescriptor`, Category "OBS", all implement `IOptionsSource`)

Existing type IDs stay, so profiles already using them keep working. Options sources: `scenes`, `audioInputs`, `sceneItems` (depends on `sceneName`; labels look like "Grup › Öğe"), `sources`, `filters` (depends on the source), `transitions`, `profiles`, `textInputs`.

| Type | Fields |
|---|---|
| `obs.setScene` | Scene (select) |
| `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode` | Scene / none |
| `obs.startStream`/`stopStream`/`toggleStream` | none (kept as is) |
| `obs.startRecord`/`stopRecord`/`toggleRecord`, **new** `obs.pauseRecord` | pause has Mode: pause / resume / toggle |
| **new** `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay` | Mode: start / stop / toggle |
| `obs.setMute` | Audio source (select) + Mode: mute / unmute (existing `muted` bool still read) |
| `obs.toggleMute` | Audio source |
| `obs.setVolume` | Audio source + Volume (slider, 0–100 %, stored as 0..1 mul). The slider/knob `context.Value` overrides it. |
| **new** `obs.adjustVolume` | Audio source + step in dB (±) |
| **new** `obs.setItemVisibility` | Scene + Item (groups included) + Mode: show / hide / toggle. Stores `{sceneName, sourceName, parentGroup?}`. At run time it uses `GetSceneItemId(parentGroup ?? sceneName, sourceName)` then `SetSceneItemEnabled`; the ID is looked up from the cache first. |
| **new** `obs.setFilterEnabled` | Source + Filter + Mode |
| **new** `obs.setTransition`, `obs.setProfile` | select |
| **new** `obs.setText` | Text source + text (`AllowVariables`) |

### 4e. Variables (catalog grows from 8 to about 45)

- **Connection:** `obs.connected`, `obs.status`, `obs.ws.in`, `obs.ws.out`
- **Scene:** `obs.scene.current`, `obs.scene.preview`, `obs.studioMode`, `obs.transition.current`, `obs.profile.current`, `obs.sceneCollection.current`
- **Stream:** `obs.streaming`, `obs.stream.reconnecting`, `obs.stream.duration`, `obs.stream.timecode`, `obs.stream.congestion` (%), `obs.stream.bytes`, `obs.stream.kbps` (from the byte difference between ticks), `obs.stream.frames.dropped`, `obs.stream.frames.total`, `obs.stream.frames.droppedPercent`
- **Record:** `obs.recording`, `obs.record.paused`, `obs.record.duration`, `obs.record.timecode`, `obs.record.bytes`, `obs.record.kbps`
- **Outputs:** `obs.virtualcam`, `obs.replayBuffer`
- **Stats:** `obs.stats.fps`, `obs.stats.cpu`, `obs.stats.memory` (MB), `obs.stats.disk` (free MB), `obs.stats.renderTime` (ms), `obs.stats.render.skipped`, `obs.stats.render.total`, `obs.stats.render.skippedPercent`, `obs.stats.output.skipped`, `obs.stats.output.total`, `obs.stats.output.skippedPercent`
- **Dynamic:**
  - `obs.input.<slug>.muted` and `obs.input.<slug>.volumeDb` for each audio input.
  - `obs.item.<sceneSlug>.<sourceSlug>.visible` for each scene item.
  - `<slug>` is the lowercased name with any run of characters outside `[a-z0-9]` turned into `_`. `Template` breaks names at `|`/`}`, so this keeps names safe.
  - The catalog's `Describe()` returns them live from the cache. They are removed with `Remove` on remove, rename and disconnect.

### 4f. Settings and status
- `ObsSettingsPage : IPluginSettingsPage` has the fields Etkin (Bool), Sunucu (Text), Port (Number 1–65535) and Şifre (Password). It saves to the same `settings.json` via `ObsSettings`, then signals a reconnect.
- `ObsSettings.LoadOrCreate` is no longer read on every loop iteration. It is loaded once and reloaded only on the signal.
- `ObsPlugin.Initialize` registers the connection, the settings page, the status item and all actions.

## Step 5 — Docs and versions
- `macro-station`:
  - `docs/versioning.md`: SDK 0.3.0.
  - `docs/plan.md` Aşama 6: remove the stale "ayar arayüzü hariç" and mark schema forms, action picker and status bar done.
  - `docs/agent-notes.md`: describe the schema-form, options-source and status-item pattern, replacing the TODO on line 112.
  - Bump the server version only if `versioning.md` requires it for new endpoints, and ask first.
- `macro-station-plugins`:
  - `docs/plugin-authoring.md`: settings pages, `IActionDescriptor`, options, status item, `Remove`, SDK 0.3.0.
  - `agent-and-repo-rules.md`: the note mentioning SDK 0.2.0.
  - `OBS/README.md` and `OBS/CHANGELOG.md` `[0.2.0]` entry.
- Commits are split per repo and per step, in English `feat:`/`fix:`/`docs:` style, with the Co-Authored-By line and no reference-project name.

## Tests and verification
- **`tests/MacroStation.Tests`:**
  - `PluginLoaderTests`: a stub plugin registers a settings page, a status item and a described action; check that `/api/actions` returns the category and fields and that the SDK caret check passes for `^0.3.0`.
  - `VariableStoreTests`: `Remove` raises `Changed`.
- **OBS plugin tests** (a new `OBS/tests/` project, if the repo rules allow it; otherwise inside Host tests via a DLL reference). They run against a **fake obs-websocket server**, an in-process `HttpListener` WebSocket:
  - handshake with and without auth;
  - close 4009 stops retrying until the settings change;
  - the server goes silent (no response) is detected by timeout and followed by reconnect;
  - abrupt TCP drop leads to reconnect with backoff;
  - `ExitStarted`;
  - a pending request when the socket dies fails immediately;
  - exactly 1 batch frame is sent per tick;
  - group items are enumerated recursively;
  - `Remove` is called when an input is removed.
- **Builds and tests:** `dotnet build` both repos, `dotnet test`, and `npm run build` plus typecheck in `editor/`.
- **Manual, with real OBS 30+:**
  - install via "Klasörden Yükle";
  - the status bar walks through kapalı → bağlanıyor → bağlı;
  - the wrong password shows "şifre hatalı" and there is no log spam;
  - after fixing the password it reconnects within 2 s without a server restart;
  - after closing and reopening OBS it reconnects;
  - the WebSocket session counter in OBS grows by about 2 per 5 s when idle;
  - the action picker's categories and search work;
  - the scene, audio source and grouped item dropdowns fill;
  - the visibility toggle works on an item inside a group;
  - new variables show live in the variable picker and on a button;
  - watch the server's working set for about 30 minutes with OBS streaming and check it stays flat.
