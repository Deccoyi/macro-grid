# Version unification plan (1.0.0 baseline)

Status (2026-09-26): sections 1, 2 and 5 are done and released (Macro Grid and SDK 1.0.0 and 1.0.1, tags `server-v1.0.0-beta` and `server-v1.0.1-beta`, SDK on NuGet). Sections 3 (plugins) and 4 (phone app) are merged into `dev` of their repositories; their `dev` to `main` pull requests and the plugin and app releases are waiting for the owner. Deviation from section 3: the plugin index stays `formatVersion` 1 (a server before 1.0.0 refuses any other number) and carries `macroGrid` next to the legacy fields.

**Repositories:** `macro-grid` (server, SDK, loader, editor, central release docs), `macro-grid-plugin` (manifests, build check, release script, store site, docs), `macro-grid-client` (server-version check, docs). The release skill (`~/.claude/skills/release/SKILL.md`, outside the repositories) is updated too.

## Decisions (owner, 2026-09-26)

1. **Beta label.** The alpha label becomes "beta": the tag is `server-v1.0.0-beta` and the GitHub Release is a pre-release. `<Version>` stays plain `1.0.0`; the label is ignored when versions are compared. The SDK/NuGet workflow accepts an optional `-beta` and compares the core version.
2. **Legacy manifests.** A manifest with only `sdkVersion` `^0.4.x` counts as `macroGrid: 1.0.0`, so the published plugins (OBS 0.2.2, PLC Icons 0.1.3, HelloJs 0.1.1, SoundBoard 0.1.1) keep working without a re-release. `^0.3.0` and older are incompatible with "must be rebuilt for Macro Grid 1.0.0". Checked: a plugin built against SDK 0.4.0.0 loads on a server that carries SDK 1.0.0.0 (`PluginLoadContext` hands out the server's own copy of the SDK assembly).
3. **Build check.** In the plugin repository `macroGrid` must have the same MAJOR as `MacroGridSdkVersion` and must not be greater than it; MINOR and PATCH may differ. It is not required to be equal.
4. **Order.** Server 0.3.2 and SDK 0.4.0 are released and on NuGet; 1.0.0 comes next. The Sound plugin is now called SoundBoard (folder `SoundBoard/`, id `soundboard`, tag `plugin-soundboard-v...`).

## Context

The server (`ClientHub.ServerVersion` 0.3.1), the SDK (`PluginSdk.Version` 0.4.0, but `<Version>` 0.3.1 in the NuGet csproj) and the plugins (`sdkVersion` + `minServerVersion`) are numbered separately and have already drifted apart. Example: SoundBoard (then called Sound) said `minServerVersion 0.3.1`, but server 0.3.1 ships SDK 0.3.1, so the plugin does not load there. OBS and PLCIcons on `dev` say `sdkVersion ^0.4.0` with `minServerVersion 0.1.0`, which is not true. `MacroGridSdkVersion` in the plugin repository is 0.4.0, which is not on NuGet. The "Now" column in `docs/guides/versioning.md` is hand-written and goes stale.

Decision: **1.0.0 is the new baseline.** The server and the SDK carry one number, and a plugin says in a single field which Macro Grid it runs on. You can see whether things fit from the numbers alone, with no notes to keep in sync.

## The rule (one sentence)

> A plugin declares `"macroGrid": "1.3.0"` ⇒ it runs on every Macro Grid where **1.3.0 ≤ server < 2.0.0**.

- **MAJOR** (2.0.0): a breaking change in the server or the SDK. Every plugin is rebuilt.
- **MINOR** (1.3.0): a compatible feature (a new SDK member, a new action, a new message). Older plugins keep working.
- **PATCH** (1.3.2): a fix. Server and SDK share the patch number too.
- Versions are **always three parts** (MAJOR.MINOR.PATCH). A two-part value is rejected by the loader and by the plugin build. Compatibility means the same MAJOR and a server version at least the required one. Pre-release suffixes such as `-alpha` are ignored in this comparison (1.0.0-alpha loads a plugin that requires 1.0.0).

## 1. Server and SDK: one source (`macro-grid`)

- `Directory.Build.props`: `<Version>1.0.0</Version>` in one place (all projects, the NuGet package included). The separate `<Version>` in the SDK csproj is removed.
- `PluginSdk.Version` (`src/MacroGrid.Plugin.Abstractions/PluginSdk.cs`) and `ClientHub.ServerVersion` (`src/MacroGrid.Core/Sessions/ClientHub.cs`) are no longer written by hand. They are read from the assembly's `AssemblyInformationalVersion`, and `ServerVersion` points at `PluginSdk.Version`, so the two cannot diverge. `const` becomes `static readonly`, because a `const` is baked into the plugin DLL at compile time and would report the wrong value. The 1.0 baseline is breaking anyway, so this costs nothing extra.
- **One tag:** `server-v1.0.0-beta` (the label is dropped once the beta ends). The naming across repositories stays the same (`server-v` / `client-v` / `plugin-<id>-v`). `release.yml` and `publish-sdk.yml` both run on this tag, and both check tag == `<Version>`. `sdk-v*` tags are no longer used; the old ones stay as history.
- `ci.yml`: a step checks that `PluginSdk.cs` and `ClientHub.cs` contain no version literal.

## 2. Loader and manifest (`macro-grid`)

- `PluginManifest.cs`: a new `MacroGrid` field (`"1.3.0"`, three parts required). `SdkVersion` / `MinServerVersion` become optional legacy fields.
- `SemVer.cs`: add `SatisfiesPlatform(serverVersion, "1.3.0")` (same major, server ≥ required, pre-release ignored).
- The three places that compute compatibility call one helper, `PluginCompatibility.Check(...)`:
  - `PluginManager.Loading.cs` (the `SatisfiesCaret` / `SatisfiesMinimum` checks)
  - `Api/PluginCatalogApi.cs` (listing and choosing a catalog version)
- A legacy manifest (only `sdkVersion` / `minServerVersion`) is evaluated with the old rule. `^0.x` matches no 1.x, so it is listed as incompatible with the message "must be rebuilt for Macro Grid 1.0.0". Old third-party plugins do not break silently; the reason is shown.
- Catalog: `PluginCatalogModels.cs`, `PluginCatalogClient.cs`, `PluginCatalogInstaller.cs` (the manifest == index equality check also covers `macroGrid`), `editor/src/api/types.ts`. The editor's incompatibility message reads "Needs Macro Grid 1.3.0+, this install is 1.2.4".

## 3. Plugin repository (`macro-grid-plugin`)

- Every `plugin.json`: `sdkVersion` and `minServerVersion` are replaced by `"macroGrid": "1.0.0"` (OBS, PLCIcons, SoundBoard, HelloJs, `examples/hello-*`). Each plugin's own `version` stays independent.
- `Directory.Build.props`: `MacroGridSdkVersion` → `1.0.0`.
- `build/Plugin.props`: a build-time check. In C# plugins, `macroGrid` in `plugin.json` have the same MAJOR as `MacroGridSdkVersion` and **not be greater** than it (three parts; MINOR and PATCH may be lower, so a plugin keeps running on more servers). Otherwise the build fails, so declaring a Macro Grid the plugin was not built against is impossible.
- `scripts/update-plugin-index.ps1`, `scripts/release-plugin.ps1`, `examples/third-party-release.yml`: the index gets `macroGrid`. `formatVersion` stays 1 (a server before 1.0.0 refuses any other number); the 1.0.0 server accepts 1 and 2.
- `scripts/release-plugin.ps1` already knows `soundboard`; check that the plugin CI (`.github/workflows/ci.yml`) builds SoundBoard too.
- Store site (`website/.vitepress/store-lib.ts`, `PluginDetail.vue`): a "Needs Macro Grid 1.3.0+" badge.
- Docs: `website/basics/compatibility.md`, `website/reference/manifest.md`, `website/reference/source-index.md` (and the `tr/` copies).

## 4. Phone app: independent number, tracked in code (`macro-grid-client`)

The app keeps its own version (0.2.0 and on), but which server it works with lives in code instead of a note:

- `package.json` gets `"macroGrid": "1.0.0"`: the oldest server the app needs (raised when it starts to need a new capability).
- `src/ws/connection.ts`: when `welcome.serverVersion` arrives it is compared with the same rule. An older server shows "Update Macro Grid on the computer"; a different major shows "Update the app".
- The release notes get a "Works with Macro Grid 1.0.0+" line generated from that field.

## 5. Release and signing docs: one source of truth

Problem: release knowledge is repeated in three repositories and one skill, the copies contradict each other, and a new agent reads the wrong one. Contradictions found:

| Where | Wrong or stale |
|---|---|
| `~/.claude/skills/release/SKILL.md` | Points to `macro-grid/docs/release.md`, which does not exist (it is `docs/guides/release.md`). No SoundBoard in the plugin list. Says to update the "Now" column. Says the installer is built only when started by hand with "installer" ticked, while `release.yml` builds it on every tag. |
| `macro-grid-plugin/docs/release.md` | Tag table says `plcicons`; the real tags are `plugin-plc-icons-v*`. No SoundBoard. Talks about checking `sdkVersion` / `minServerVersion`. |
| `src/MacroGrid.Core/Plugins/Distribution/PluginSigning.cs` (comment) | Says the private key is a GitHub Actions secret and `release.yml` signs. Signing now happens locally (`release-plugin.ps1`). |
| `docs/guides/release.md` | Describes a separate `sdk-v*` tag flow and the "a 0.x minor is breaking" SDK rule. |
| `macro-grid-client/docs/versioning.md` | Says nothing checks the versions (wrong after section 4). |

**Structure:**

- **Central document:** `docs/guides/release.md` is rewritten as "Releasing Macro Grid", covering every repository:
  1. **The version rule** (the sentence above, and what MAJOR / MINOR / PATCH mean).
  2. **Tag table:** `server-vX.Y.Z` (server + SDK), `client-vX.Y.Z`, `plugin-<id>-vX.Y.Z` (ids: `obs`, `plc-icons`, `soundboard`, `hellojs`), and the beta suffix rule.
  3. **Order:** server + SDK → wait until the version is on NuGet → plugins (raise `macroGrid` only on a MAJOR bump or when a plugin uses new API) → APK (raise `package.json` `macroGrid` if needed). Each step says how to tell it is done.
  4. **Signing table:**

     | What | How | Where the key is | Who verifies | If the key is lost |
     |---|---|---|---|---|
     | Plugin zip | ECDSA P-256, `scripts/sign-package.cs` (called by `release-plugin.ps1`) | `%USERPROFILE%\signing\plugin-signing\plugin-signing-private.pem`, only on the maintainer's PC | The server, with the public key in `PluginSigning.cs` → "Official" badge | New key + a server release with the new public key + every plugin signed again |
     | APK | Android keystore, `scripts\build-release-apk.ps1` | `C:\keys\macro-grid.jks` + `android\keystore.properties` (git-ignored) | Android (no update without the same signature); the script checks the certificate fingerprint | The update path is closed for good |
     | Server installer | Not signed (SmartScreen warns) | — | The updater, with GitHub's SHA-256 | — |
     | SDK (NuGet) | No key, Trusted Publishing (`publish-sdk.yml`, environment `nuget`) | — | nuget.org | — |

     The backup rule and "a key never goes into a repository" live here too.
  5. Links to each repository's detailed steps.
- **Repository docs keep only their own detail** and do not repeat the tag table, the version rule or the signing summary; they link to the central document:
  - `macro-grid-plugin/docs/release.md`: the `release-plugin.ps1` steps, the correct `-Name` list (SoundBoard included), the `macroGrid` check.
  - `macro-grid-client/docs/release.md`: keystore setup and fingerprint stay here (APK-specific); add the central link and the `macroGrid` step.
  - `docs/guides/versioning.md` and `macro-grid-client/docs/versioning.md`: the new rule; the "Now" column is removed.
  - `macro-grid-plugin/website/guides/publishing.md`, `website/basics/compatibility.md` (and `tr/`): the `macroGrid` field and unsigned publishing for third-party authors.
- **Release skill** (`~/.claude/skills/release/SKILL.md`): kept thin, with correct paths and SoundBoard. It says "read `macro-grid/docs/guides/release.md` first" and does not repeat tag, version or signing detail, so the doc and the skill cannot drift apart.
- **Each repository's `CLAUDE.md`:** add "Version, release or signing work → read `macro-grid/docs/guides/release.md` first", so a new agent lands in the right place.
- Fix the `PluginSigning.cs` comment (the key is local; `release-plugin.ps1` signs).
- Changelogs get a 1.0.0 entry: the SDK and the server now carry the same number; plugin manifests have a `macroGrid` field.

## Order of work

1. `macro-grid`: sections 1, 2 and 5 (on `dev`), with tests (SemVer and loader, legacy and new manifests).
2. Tag `server-v1.0.0-beta`; SDK 1.0.0 goes to NuGet (the owner tags).
3. `macro-grid-plugin`: section 3. Every plugin is re-released with `macroGrid: "1.0.0"`.
4. `macro-grid-client`: section 4.

When done, move this file to `../done/`.

## Verification

- `dotnet test` (`macro-grid`): a `SatisfiesPlatform` table (required 1.0.0 ↔ server 1.0.0-beta passes, 1.3.0 ↔ 1.2.5 fails, 1.3.0 ↔ 1.3.4 passes, 1.3.0 ↔ 2.0.0 fails, "1.3" is invalid), and a legacy `^0.3.0` manifest is incompatible with the "rebuild" message.
- Run the server: OBS, SoundBoard and PLCIcons (`macroGrid: 1.0.0`) load in the editor. A copy with `macroGrid: "1.1.0"` shows as incompatible with the right message.
- `macro-grid-plugin`: `macroGrid` "1.1.0" with SDK 1.0.0 fails `dotnet build`; matching values pass.
- Client: `npm test`, with old and new server cases added to `connection.test.ts`.
- Docs: grep the three repositories and the skill for `sdk-v`, `sdkVersion`, `minServerVersion`, `plcicons`, `docs/release.md` and "Now". Nothing may remain outside legacy notes and changelog history. Every path and script name in the central document must exist (`release-plugin.ps1 -Name` matches the script's `ValidateSet`).

## Open questions

- None right now. Ideas not decided: enforce "no breaking change inside a MAJOR" in CI with the .NET package validation against the previous package version.
