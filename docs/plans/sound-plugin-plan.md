# Sound plugin plan

Status: planned, not started. This is the canonical copy; phase A is in this repository, phase B in `macro-grid-plugin`. (`macro-grid-plugin/docs/sound-plugin-plan.md` is an untracked older copy: delete it or commit it as a pointer to this file.)

**Repositories:** `macro-grid` (phase A: SDK 0.4.0, host, editor) and `macro-grid-plugin` (phase A4: OBS and PLCIcons manifests; phase B: the Sound plugin). Phase A must ship first. `macro-grid-client` is not touched.

## Context
A soundboard plugin (`Sound/`, C#). The user picks sound files in the plugin's settings window, gives each one a name and a volume, and binds buttons that play or stop them. SDK 0.3.1 cannot express this: there is no file picker, no list field, no inline button (preview), no notice field, and an action never learns that its button was released. The work therefore spans two repositories: first the SDK, host and editor (`macro-grid`, SDK 0.4.0), then the plugin (`macro-grid-plugin`). Both on `dev`.

## Phase A: SDK 0.4.0 (`macro-grid`)

### A1. SDK additions (`src/MacroGrid.Plugin.Abstractions`)
- `SettingField.cs`, new kinds and properties:
  - `SettingFieldKind.File` + `FileFilter` (a WinForms filter string): path text plus a Browse button.
  - `SettingFieldKind.List` + `ItemFields: SettingField[]`: repeated rows, value is a `JsonArray` of `JsonObject`. Row keys that are not in the schema (a plugin-assigned `id`, `missing`, ...) are preserved by the editor. `VisibleWhen` inside a row is evaluated against that row's own values.
  - `SettingFieldKind.Button` + `Command: string`: on click the host passes the command (and the values of the row/form it sits in) to the settings page. Used for preview.
  - `SettingFieldKind.Notice`: read-only warning text (`Description`), conditional through `VisibleWhen`. Used for the missing-file warning.
- New `IReleaseAwareAction`: `Task ReleaseAsync(ActionContext, JsonObject settings, CancellationToken)`. When a handler bound to a widget's **press** event implements it, the host calls it on release.
- New `ISettingsCommandHandler` (optional side interface of a settings page): `Task<string?> RunCommandAsync(string command, JsonObject values, CancellationToken)`. The returned text is shown in the editor as a short info/error message.
- `PluginSdk.Version = "0.4.0"`; update the table in `docs/guides/versioning.md`.

### A2. Host (`src/MacroGrid.Core`, `src/MacroGrid.Host`)
- `Actions/ActionDispatcher.cs`: when `eventName == WidgetEvents.Release`, first call `ReleaseAsync` on the `Press` bindings whose handler is an `IReleaseAwareAction` (same `ResolveVariables` and error collection), then run the regular release bindings.
- `Plugins/PluginLocalizer.cs`: localize `ItemFields` recursively.
- `Host/UiDialogService.cs`: `BrowseForFileAsync(title, filter)` returning the path only. `ServerApp.cs`: `POST /api/browse/file` and `POST /api/plugins/{id}/settings/command` (404 when the settings page is not an `ISettingsCommandHandler`).
- Tests: dispatcher release hook, command endpoint.

### A3. Editor (`editor/src`)
- `api/types.ts` and the API client: new kinds and properties, `browseFile`, `runPluginSettingsCommand`.
- `panels/actionForms/SchemaForm.tsx`: `File` (input + Browse), `List` (row cards, recursive `SchemaForm`, extra keys kept via `{...row}`, add/remove, key `row.id ?? index`), `Button` (runs the command, shows the returned text), `Notice` (warning-colored text). Options fetching and command calls are supplied by `PluginSettingsWindow.tsx`.
- i18n strings; make sure the settings window is wide enough and scrolls for a list.
- Changelogs under `[Unreleased]`.

### A4. Existing plugins
- OBS and PLCIcons `plugin.json`: `sdkVersion` to `^0.4.0` (a caret range on 0.x does not match the next minor); `Directory.Build.props` `MacroGridSdkVersion` to 0.4.0; their own changelogs. Plugin version bumps are decided separately.

## Phase B: Sound plugin (`Sound/`)
Read `CONTRIBUTING.md` and `docs/plugin-authoring.md` first. The layout mirrors OBS: `plugin.json` (`id: "sound"`, `kind: csharp`, `sdkVersion ^0.4.0`), `src/MacroGrid.Plugin.Sound.csproj` (OBS csproj pattern plus the audio library packages, copied to the output), `locales/tr.json`, `README.md`, `CHANGELOG*.md`, `LICENSE`, `NOTICE.md`, `tests/`. The audio library is NAudio (MIT): add it to `THIRD_PARTY_NOTICES.md` and `licenses/`.

### B1. Settings (`SoundSettings.cs`, the `ObsSettings`/`ObsSettingsPage` pattern; also `IOptionsSource` and `ISettingsCommandHandler`)
Stored in `DataDirectory/settings.json`. Fields:
- `outputDevice`: Select, `OptionsSource = "devices"` (default device plus the WASAPI render devices; the device id is stored; if the device is gone, fall back to the default and show a status warning).
- `sounds`: **List**, each row: `file` (File, audio filter), `name` (Text), `volume` (Slider 0–100, default 100), `loop` (Bool), `preview` (Button, command `preview`; stops the preview if it is playing), `missingNotice` (Notice "File not found, pick it again", `VisibleWhen = "missing=true"`). `Load` computes `missing` for each row; `Save` assigns a permanent short id (`s1`, `s2`, ...) to rows without one. **Files are never copied**; the absolute path is stored. A moved file has to be picked again.
- `masterVolume`: Slider 0–100.
- `overlapMode`: Segmented, `overlap` (pressing the same sound again starts another copy) / `cut` (a new sound stops the ones playing).
- `stopStyle`: Segmented, `immediate` (default) / `fade`.
- `fadeInMs`: Number, default 0 (no fade-in). `fadeOutMs`: Number, default 500.
- After saving, the engine updates live (volume, loop; a device change reopens the output).

### B2. Audio engine (`SoundEngine.cs`), within the lightweight resource budget
- One shared-mode `WasapiOut` on the selected device plus one `MixingSampleProvider` (fixed 48 kHz stereo float) feeding a master `VolumeSampleProvider`. The output opens on the first play and closes after about 30 s of idle.
- A voice: file reader (library readers for wav/mp3, Media Foundation for other formats) → resampler/channel converter → loop wrapper (rewinds at the end when loop is on) → per-sound volume → fade provider (fade-in at start, fade-out on stop). A finished voice leaves the mixer.
- Voices are tagged `(soundId, deviceId, pageId, widgetId)`; preview voices have their own tag.
- `Stop(filter, style)`: immediate removes the voice; fade fades over `fadeOutMs`, then removes it.
- Missing file: throw "Sound file not found: ..., pick it again in the settings". The host shows it as a toast on the device and in the status bar. A `CreateStatusItem("sound-files")` entry shows the number of missing files.
- The plugin is `IDisposable`: unloading closes the output and the readers.

### B3. Actions (`SoundActions.cs`, `IActionHandler` + `IActionDescriptor`, category "Sound")
- `sound.play` (also `IReleaseAwareAction`, `IOptionsSource`):
  - `sound`: Select, `OptionsSource = "sounds"`.
  - `playMode`: Segmented, `full` / `hold` (plays while held) / `toggle` (stops it if playing, starts it otherwise).
  - `stopStyle`: Segmented, `default` (from settings) / `immediate` / `fade`; applied on hold release and on a toggle stop.
  - `ExecuteAsync`: for toggle, if this sound is playing, stop it and return; in `cut` mode first stop everything with the settings' stop style; start the sound; return immediately (never wait for playback to finish, so the device queue is not blocked).
  - `ReleaseAsync`: only in `hold` mode, stops the voices this widget started.
- `sound.stop`: `target`, `all` / `one`; `sound` (`VisibleWhen = "target=one"`); `stopStyle`, default/immediate/fade.
- `sound.setMasterVolume`: `mode`, `set` (value 0–100) / `adjust` (± step) / `slider` (the slider/knob `ActionContext.Value`); related fields through `VisibleWhen`.

### B4. Variables (`SoundVariables.cs`, `IVariableProvider` + `IVariableCatalogSource`)
- `sound.<id>.name`: for button text, e.g. `{sound.s1.name}`. The catalog description shows the sound's current name.
- `sound.<id>.playing`: true/false. The existing dynamic color rules (`DynamicRuleEvaluator`) can color a button while it plays; no host work needed.
- `sound.<id>.remaining` (mm:ss) and `sound.<id>.duration`. With loop on, remaining is the rest of the current pass.
- `sound.nowPlaying`: name of the most recently started sound. `sound.masterVolume`.
- Event-driven updates; `remaining` uses a ~4 Hz timer only while at least one sound plays, stopped when idle. Variables of deleted sounds are removed with `IVariableStore.Remove`.

### B5. Tests (`Sound/tests/`)
- Settings load/save, id stability across rename/reorder, `missing` computation.
- Engine logic without a device (reading the mixer by hand): overlap/cut, hold-release filtering, toggle, loop rewind, fade-in/out durations and voice removal after a fade, remaining time.

## Verification (at implementation time)
1. `macro-grid`: `dotnet build` and `dotnet test`; editor `npm run build`.
2. Build the plugin against the local SDK (`-p:UseLocalSdk=true`) and install it with "Install from Folder".
3. End to end: add sounds with Browse (no copy, absolute path), name/volume/loop, preview; switch the output device; full/hold/toggle; overlap vs cut; stop immediate vs fade; fade-in; master slider and `sound.setMasterVolume`; `{sound.s1.name}` and `{sound.s1.remaining}` in button text; a color rule on `playing`; move a file → toast, status entry and settings warning → pick it again.
4. Check that OBS and PLCIcons still load on the 0.4.0 host.
