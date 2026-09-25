# Versioning

Macro Grid follows [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`. While the project is before 1.0.0 (`0.MINOR.PATCH`),
the same discipline applies: a breaking change is a MINOR bump, a compatible feature is also a MINOR bump, and a fix is a PATCH bump.
This document describes what is versioned, where each version lives and what counts as a breaking change.

## What is versioned, and where

There is no single project version. Four things are versioned independently, because one can change while the others stay compatible:

| What | Where the version lives | Now |
|---|---|---|
| **Server** (`macro-grid`, this repository) | `ClientHub.ServerVersion` in `src/MacroGrid.Core/Sessions/ClientHub.cs` | `0.3.1` |
| **Phone app** ([macro-grid-client](https://github.com/Deccoyi/macro-grid-client)) | `version` in that repository's `package.json` | `0.1.0` |
| **Plugin SDK** (`MacroGrid.Plugin.Abstractions`) | `PluginSdk.Version` in `src/MacroGrid.Plugin.Abstractions/PluginSdk.cs` | `0.3.0` |
| **Each plugin** ([macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin)) | `version` in the plugin's own `plugin.json` | per plugin |

The plugin SDK is published on NuGet as `MacroGrid.Plugin.Abstractions`; its version is the SDK version (`PluginSdk.Version`) and plugins reference it as a package (developers may use the project path instead, see the plugin repository's `docs/using-the-sdk-package.md`). The browser deck (`webclient/`) and the editor (`editor/`)
are part of the server and released with it.

## The connection between server and phone app

The phone app and the browser deck talk to the server over a WebSocket (port 9820) with JSON messages. Compatibility works like this:

- `hello` and `welcome` carry `clientVersion` and `serverVersion`. They are informational; nothing checks them.
- Optional features are negotiated. A client lists what it understands in `hello.capabilities` (`assets`, `layout.patch`, see
  `ClientCapabilities.cs`), and the server sends the older, plain form to a client that lists nothing. This is how a newer server keeps
  working with an older phone app and the browser deck. New optional features should be added the same way.
- There is no `protocolVersion` number. Changing the meaning of an existing message or field is a breaking change (see below).

## What each bump means

- **MAJOR** (after 1.0.0; before that, a MINOR bump): a breaking change.
  - Server and phone app: an existing WebSocket message or field is removed or changes meaning, or the `hello` flow changes in a way an
    older client cannot handle.
  - Plugin SDK: a public interface a plugin implements or receives (`IPlugin`, `IPluginHost`, `IActionHandler`, `IVariableProvider`,
    `IVariableStore`, `IDeviceController`, `ActionContext`, ...) changes incompatibly. This requires every plugin built for that SDK to be rebuilt.
  - Plugin: it changes its action types, settings or variable names in a way that makes a user's saved profile stop working.
- **MINOR**: a compatible feature: a new widget type, a new built-in action (`core.*`), a new optional message or capability, a new optional
  member or interface in the SDK.
- **PATCH**: a fix, a performance change or an internal refactor with no change in behavior.

## Plugin compatibility

Every plugin declares in `plugin.json` which SDK it was built against and which server it needs:

```json
{
  "id": "obs",
  "name": "OBS Control",
  "version": "0.2.0",
  "sdkVersion": "^0.3.0",
  "minServerVersion": "0.1.0",
  "entry": "MacroGrid.Plugin.Obs.dll",
  "kind": "csharp"
}
```

- `sdkVersion` is a caret range checked against `PluginSdk.Version`. While the SDK is `0.x`, `^0.3.0` matches `0.3.x` only; from 1.0.0,
  `^1.2.0` matches `1.2.0` up to (not including) `2.0.0`.
- `minServerVersion` is the oldest server that has what the plugin uses.
- A plugin that does not satisfy either is listed as incompatible in the editor and is not loaded.

The full manifest and the SDK are described in `docs/plugin-authoring.md` in the plugin repository.

## Releasing

Work happens on the `dev` branch and is merged into `main` for a release. Before a merge to `main` the maintainer decides whether the version
is bumped and by how much (MAJOR, MINOR or PATCH); a version number is never changed silently. When a bump is approved, both changelogs get
their entry:

- `docs/CHANGELOG-developer.md`: the detailed, technical record, in [Keep a Changelog](https://keepachangelog.com/) format.
- `docs/CHANGELOG.md`: the short record for people who are not developers. Short sentences, what is new and what got fixed, with no code,
  file or API names, and without small bug fixes or internal changes.

Both are written in English. See [docs/guides/release.md](release.md) for building the installer.
