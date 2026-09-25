# Plugin distribution plan

> Status: **planned, not implemented.** This document describes how plugins will be
> found, downloaded and installed from GitHub, and the repo format plugin authors
> must follow.

**Repositories:** `macro-grid` (phases 2 to 5: host, editor, docs) and `macro-grid-plugin` (phase 1: `release.yml`, index generator, author docs, the store website). `macro-grid-client` is not touched.

## Context

Today plugins can only be installed from a local folder. We want three more ways:
browse the official plugin repo (`Deccoyi/macro-grid-plugin`), add another
developer's multi-plugin repo as a source, and install a single-plugin repo from a
pasted link. Anything not published by us must be clearly marked as third-party.

## Current state

- **Install:** `PluginManager.InstallFromFolderAsync` (`src/MacroGrid.Core/Plugins/PluginManager.cs`)
  copies the folder to `%AppData%\MacroGrid\plugins\<id>\` with `CopyDirectory`
  (overwrite only, not atomic) and loads it. Endpoint: `POST /api/plugins/install`
  (`src/MacroGrid.Host/Api/PluginApi.cs`), backed by the native folder dialog.
- **Manifest:** `plugin.json` → `PluginManifest` (SDK): `id`, `name`, `version`,
  `sdkVersion` (caret range), `minServerVersion`, `entry`, `kind`, `permissions` (JS only).
  Compatibility: `SemVer.SatisfiesCaret` / `SatisfiesMinimum`; failures get the
  `Incompatible` status.
- **Plugin data:** `IPluginHost.DataDirectory` is the install folder, so an update must
  keep the user's files (e.g. `settings.json`).
- **UI:** `editor/src/windows/PluginsWindow.tsx` already has an empty **Discover** tab
  (`plugins.comingSoon.*`).
- **Plugins repo:** `release.yml` runs on `plugin-<name>-v<version>` tags and creates
  `<id>-<version>.zip` as a **draft** pre-release. No hash, no signature, no index.
- **Missing:** plugin signing, any outbound HTTP from the host, update checks.
- **Reusable:** `ProfilePackage.cs` (safe zip reading with entry caps),
  `PluginManager.MissingPlugins(...)` (later: offer installs on profile import),
  `IsSafeSegment`, `PluginPermissionStore` (JS permission approval).

## 1. Install methods

| # | Method | Trust | Warning |
|---|---|---|---|
| 1 | Local folder (exists today) | Local, not verified | "Local" badge |
| 2 | Official catalog (built in, cannot be removed) | Signature must verify with the official key, otherwise refused | None |
| 3 | Added source: a third-party **multi-plugin** repo, saved in the source list | SHA-256 from that repo's `macrogrid-index.json` | Third-party confirmation on every install |
| 4 | Direct link: a third-party **single-plugin** repo URL | SHA-256 from the release's `.sha256` asset | Third-party confirmation |

No method uses the GitHub API. Everything is fetched from fixed
`raw.githubusercontent.com` and `releases/download` URLs, so the API rate limit
(60 requests/hour per IP, anonymous) never applies. Methods 2 and 3 share the catalog
code path. Method 4 is a small resolver that turns a repo URL into a package URL,
then uses the same download/verify/install pipeline.

## 2. Branches and where built files live

- **No compiled files are committed to any branch.** Built zips live only as
  **GitHub Release assets** (attached to a tag, stored outside git history).
  There is no `release` branch.
- `dev`: day-to-day work.
- `main`: the **released** state and the repo's **default branch**. `dev` is merged
  into it when releasing. The host reads `raw.githubusercontent.com/<owner>/<repo>/HEAD/...`,
  which serves the default branch, so `main` must always describe what is actually released.
- Pushing a tag on `main` runs the release workflow: build → zip → hash →
  (official only) sign → publish a GitHub Release with those assets.
- After the release the workflow updates the metadata on `main` (the index for
  multi-plugin repos; for single-plugin repos `plugin.json` is already correct).

```
dev  ──●──●──●──────●──●
             \ merge   \ merge
main ─────────●──[tag]───●──[tag]
                   │          │
            GitHub Release  GitHub Release
            obs-0.2.0.zip   obs-0.3.0.zip  (+ .sha256, + .sig)
```

## 3. Repo format for plugin authors

### 3a. Multi-plugin repo (methods 2 and 3)

- Public GitHub repo with `macrogrid-index.json` at the root of `main`, read from
  `https://raw.githubusercontent.com/<owner>/<repo>/HEAD/macrogrid-index.json`.
- Tags: `plugin-<name>-v<version>`.
- Each version is a published (not draft) Release asset `<id>-<version>.zip` with
  `plugin.json` at the zip root (the layout `release.yml` already produces).
- Index schema, `formatVersion` 1:

```json
{
  "formatVersion": 1,
  "name": "Example Plugins",
  "author": "someone",
  "plugins": [
    {
      "id": "obs",
      "name": "OBS",
      "description": "...",
      "author": "...",
      "homepage": "https://github.com/<owner>/<repo>",
      "kind": "csharp",
      "versions": [
        {
          "version": "0.2.0",
          "sdkVersion": "^0.3.0",
          "minServerVersion": "0.2.0",
          "url": "https://github.com/<owner>/<repo>/releases/download/plugin-obs-v0.2.0/obs-0.2.0.zip",
          "sha256": "<hex>",
          "size": 123456,
          "permissions": []
        }
      ]
    }
  ]
}
```

- Rules the host enforces:
  - `url` must be `https://github.com/<same owner>/<same repo>/releases/download/...`,
    so an index can only point to its own repo.
  - `sha256` and `size` are required.
  - `id`, `version`, `sdkVersion`, `minServerVersion`, `kind` and `permissions` in the
    zip's `plugin.json` must match the index entry exactly, otherwise the install is refused.
- `sdkVersion` / `minServerVersion` are copied into the index so Browse can grey out
  incompatible versions before downloading. CI fills them from `plugin.json`, so
  authors never write them twice. The host picks the highest compatible version.

### 3b. Single-plugin repo (method 4)

- Public GitHub repo with `plugin.json` at the root of `main`, always equal to the
  latest released version.
- Tags: `v<version>`. Release assets: `<id>-<version>.zip` (`plugin.json` at the zip
  root) and `<id>-<version>.zip.sha256`.
- The host resolves a pasted `https://github.com/<owner>/<repo>` like this:
  1. Read `raw.githubusercontent.com/<owner>/<repo>/HEAD/plugin.json` to get `id`, `version`,
     `sdkVersion`, `minServerVersion`, `kind`, `permissions`. Compatibility is shown
     before anything is downloaded.
  2. Download `github.com/<owner>/<repo>/releases/download/v<version>/<id>-<version>.zip`
     and its `.sha256`.
  3. Run the normal pipeline. The zip's `plugin.json` must equal the one from step 1.
- If the repo has `macrogrid-index.json` instead, the host says it is a multi-plugin
  repo and offers to add it as a source (method 3).
- The origin is recorded, so "update available" works by re-reading that `plugin.json`.

### 3c. Manifest additions and author tooling

- New optional manifest fields: `description`, `author`, `homepage`. Additive; older
  hosts ignore unknown properties.
- Planned optional manifest field for a plugin logo (avatar): a path to a small square image (SVG or PNG) inside the
  package. The Store list and detail page and the editor's Plugins window show it; without one they keep the category
  glyph. To do with the next manifest or SDK change. Needs a size and format limit, and the release zip and the
  Store catalog build must carry the image (`website/store/icons/` holds only the category glyphs today).
- Copyable GitHub Actions workflows (one per repo type) that build, zip, hash, publish
  the release and update the index/metadata on `main`.
- Author docs on the plugins website: update `website/guides/publishing.md`, add
  `website/reference/source-index.md` (index JSON schema + single-plugin format).

## 4. Official catalog and signing

- The official source URL (`Deccoyi/macro-grid-plugin`) is hard-coded in the host.
- A dedicated plugin-signing key (ECDSA P-256, available in the .NET base library),
  separate from the NuGet/APK keys:
  - private key: GitHub Actions secret in the plugins repo;
  - public key: embedded in the host;
  - `release.yml` produces `<zip>.sig` over the zip's SHA-256; index entries gain a
    `signature` field.
- An official-source package that fails signature verification is refused.
  Packages from any other source are always third-party, even if signed.
- Plugins repo changes: hash + signature steps in `release.yml`, publish releases
  instead of drafts, release from `main`, regenerate `macrogrid-index.json` on `main`,
  make `main` the default branch (already done: `main` is the default in all three repositories).

## 5. Host install pipeline

1. **Fetch.** The HTTP client is used only when the Discover tab is opened or an
   install is started; nothing runs in the background (lightweight resource budget).
   - HTTPS only, to `raw.githubusercontent.com`, `github.com` and GitHub's release
     download redirect host.
   - Timeouts and size caps: index ≤ 1 MB, package ≤ 100 MB.
   - No network → "You are offline" message; Discover shows a loading state meanwhile.
2. **Download** to `%AppData%\MacroGrid\plugins-staging\<guid>\`; verify size,
   SHA-256 and (official only) the signature.
3. **Safe unzip:** reject `..`, absolute paths and links; cap each entry and the total
   size, following `ProfilePackage`.
4. **Check the manifest** against the index / `plugin.json` it came from, then
   compatibility. JS permissions go through the existing approval flow.
5. **Install** via `InstallFromFolderAsync(stagingDir)`, then delete the staging folder.
   User files survive because `CopyDirectory` only overwrites.
6. **Record the origin** in `%AppData%\MacroGrid\plugin-installs.json`:
   `{ "<id>": { "sourceUrl", "version", "trust" } }`. Used for badges and updates.

Saved sources live in `%AppData%\MacroGrid\plugin-sources.json`.

## 6. API (loopback only, like the rest of `/api`)

- `GET /api/plugin-sources`, `POST /api/plugin-sources {url}`, `DELETE /api/plugin-sources/{id}`
- `GET /api/plugin-catalog?source=<id>`: fetch + validate the index; entries come back
  with compatibility and installed/update state.
- `POST /api/plugin-catalog/install {source, id, version}`: runs the pipeline and
  returns the same `PluginInstallResult` as today.
- `POST /api/plugin-link/inspect {url}`: method 4 step 1, data for the confirmation dialog.
- `POST /api/plugin-link/install {url}`: method 4 install.

## 7. UI (`PluginsWindow.tsx`)

- **Discover tab:** source selector (Official + saved sources), "Add source…" and
  "Install from link…" buttons, loading state, offline message, plugin cards (name,
  author, version, description, compatibility, Install / Update / Installed).
- **Third-party confirmation dialog:** repo URL, author, version and the risk:
  - C# plugins: "runs with full access to your computer";
  - JS plugins: the list of requested permissions.
- **Installed tab:** Official / Third-party / Local badges and "Update available".
  The update flag is only computed after Discover data has been fetched.
- All strings go through `en.ts` and `tr.ts`.

## 8. Security notes

- This is the host's first outbound connection. It is opt-in and only triggered by
  the user. `architecture.md` (Security model) must be updated when this ships.
- An index can only reference its own repo's releases. A pasted link is never saved
  as a source without the user choosing to.
- Future work, out of scope here:
  - per-author keys / trust-on-first-use;
  - removing stale files on update;
  - atomic install with rollback;
  - offering installs for plugins missing on profile import;
  - non-GitHub hosts.

## 9. Rollout phases

- [ ] **Phase 1 — plugins repo:** `main` branch, release hash + signature, index
      generator, author docs and schemas for both repo types.
- [ ] **Phase 2 — host:** catalog client + install pipeline for the official source (method 2).
- [ ] **Phase 3 — host:** added multi-plugin sources with the third-party warning (method 3).
- [ ] **Phase 4 — host:** single-plugin direct links (method 4).
- [ ] **Phase 5 — host:** badges and update-available.

Each phase updates both changelogs under `[Unreleased]`.
