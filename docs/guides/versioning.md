# Versioning

Macro Grid follows [Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`, always written with all three parts.
This document says what is versioned, where each version lives, what a bump means and how a plugin says which Macro Grid it runs on.
How a release is made (tags, order, signing) is in [release.md](release.md).

## The rule

> A plugin declares `"macroGrid": "1.3.0"` ⇒ it runs on every Macro Grid from **1.3.0 up to, but not including, 2.0.0**.

**The server and the plugin SDK are one thing and carry one number.** Macro Grid 1.3.0 is server 1.3.0 and SDK 1.3.0 (the NuGet package
`MacroGrid.Plugin.Abstractions` 1.3.0). There is no separate SDK number to keep in step, and a plugin needs no second range.

## What is versioned, and where

| What | Version lives in | Independent? | Tag |
|---|---|---|---|
| **Macro Grid**: the server (with the editor and the browser deck) and the **plugin SDK** | `<Version>` in `Directory.Build.props`, the only place. `ClientHub.ServerVersion` and `PluginSdk.Version` read it from the assembly. | One number for both | `server-vX.Y.Z` |
| **Phone app** ([macro-grid-client](https://github.com/Deccoyi/macro-grid-client)) | `version` in that repository's `package.json` | Yes | `client-vX.Y.Z` |
| **Each plugin** ([macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin)) | `version` in the plugin's `plugin.json` | Yes | `plugin-<id>-vX.Y.Z` |

Never write a version into code or into a project file: CI fails when `PluginSdk.cs` or `ClientHub.cs` contain a version literal or a
`.csproj` sets its own `<Version>`. During the beta the tag carries the label (`server-v1.0.0-beta`) and the GitHub Release is a
pre-release; the version inside the program stays plain `1.0.0`, and the label is ignored when versions are compared.

## What each bump means

- **MAJOR** (`2.0.0`): a breaking change. Every plugin has to be rebuilt and every phone app may need an update.
  - Macro Grid: a public interface a plugin implements or receives (`IPlugin`, `IPluginHost`, `IActionHandler`, `IVariableProvider`,
    `IVariableStore`, `IDeviceController`, `ActionContext`, `PluginManifest`, ...) changes incompatibly; or an existing WebSocket message
    or field is removed or changes meaning, or the `hello` flow changes in a way an older phone app cannot handle.
  - Plugin: it changes its action types, settings or variable names so that a user's saved profile stops working.
- **MINOR** (`1.3.0`): a compatible feature: a new widget type, a new built-in action (`core.*`), a new optional message or capability,
  a new optional member or interface in the SDK. Plugins built for an older 1.x keep running.
- **PATCH** (`1.3.2`): a fix, a performance change or an internal refactor with no change in behavior.

Within one MAJOR the SDK must stay compatible with plugins built for any earlier version of that MAJOR: add members, never remove or change them.

Before 1.0.0 the parts were numbered separately: the last one was server 0.3.2 with SDK 0.4.0. From 1.0.0 on there is one number.

## Plugin compatibility

Every plugin declares in `plugin.json` the oldest Macro Grid it runs on:

```json
{
  "id": "obs",
  "name": "OBS Control",
  "version": "0.3.0",
  "macroGrid": "1.0.0",
  "entry": "MacroGrid.Plugin.Obs.dll",
  "kind": "csharp"
}
```

- `macroGrid` is `MAJOR.MINOR.PATCH`. A two-part value (`"1.0"`) or a range (`"^1.0.0"`) is refused.
- A C# plugin is built against the SDK package, so `macroGrid` must be the SDK version it uses or older, in the same MAJOR (the plugin repository's
  build checks this). Raise it only when the plugin starts to use something added in a newer MINOR; otherwise it keeps running on more servers.
- A plugin that does not fit is listed as incompatible in the editor, with the reason ("Needs Macro Grid 1.3.0 or newer, this is 1.2.4", or
  "must be rebuilt" for a different MAJOR), and is not loaded. Discover and the source index use the same rule and only offer a version that fits.

| Plugin says | Macro Grid 1.2.4 | 1.3.0 | 1.9.9 | 2.0.0 |
|---|---|---|---|---|
| `1.0.0` | runs | runs | runs | rebuild |
| `1.3.0` | needs 1.3.0 | runs | runs | rebuild |

### Older manifests

Before 1.0.0 a manifest carried `sdkVersion` (a caret range) and `minServerVersion` instead of `macroGrid`. They are still read, only when
`macroGrid` is absent:

- `sdkVersion` `^0.4.x` counts as `macroGrid: 1.0.0`. SDK 1.0.0 changed nothing a 0.4 plugin uses, and the loader hands a plugin the server's own
  copy of the SDK assembly whatever version it was built against, so a plugin built for 0.4.0 runs on 1.x without a re-release.
  `minServerVersion` is ignored.
- Any other `sdkVersion` (`^0.3.0` and older) is incompatible with "must be rebuilt for Macro Grid 1.0.0".
- A new plugin should write only `macroGrid`. A plugin that also has to run on servers older than 1.0.0 may keep the two old fields next to it.

## The connection between server and phone app

The phone app and the browser deck talk to the server over a WebSocket (port 9820) with JSON messages. Compatibility works like this:

- `hello` and `welcome` carry `clientVersion` and `serverVersion`. They are informational; nothing checks them yet.
- Optional features are negotiated. A client lists what it understands in `hello.capabilities` (`assets`, `layout.patch`, see
  `ClientCapabilities.cs`), and the server sends the older, plain form to a client that lists nothing. This is how a newer server keeps
  working with an older phone app and the browser deck. New optional features should be added the same way.
- There is no `protocolVersion` number. Changing the meaning of an existing message or field is a breaking change (see above).
- The phone app keeps its own number. Which Macro Grid it needs is meant to be recorded in its `package.json` (`macroGrid`) and checked when
  `welcome` arrives; this is planned in [../plans/version-unification-plan.md](../plans/version-unification-plan.md).

## Releasing

Work happens on the `dev` branch and is merged into `main` for a release. Before a merge to `main` the maintainer decides whether the version
is bumped and by how much (MAJOR, MINOR or PATCH); a version number is never changed silently. When a bump is approved, the changelogs get
their entry:

- `docs/CHANGELOG.md`: the short record for people who are not developers. Short sentences, what is new and what got fixed, with no code,
  file or API names, and without small bug fixes or internal changes. It is also the source of the release notes and of the update window.
- `docs/CHANGELOG-developer.md`: the developer record, in [Keep a Changelog](https://keepachangelog.com/) format, only for what git history cannot carry:
  changes to the plugin SDK or the protocol, breaking changes, migrations and anything a plugin author or maintainer has to do differently. Older entries are
  more detailed and stay as they are.

Both are written in English. The steps, the tags and the signing keys are in [release.md](release.md).
