# Discover: icons, screenshots and ratings

> Status: **icon, category and tags are built. Screenshots are designed here and not built (recommendation: not now). Install counts and ratings
> are not planned.** Five small leftovers can be done now (below). **Repositories:** `macro-grid` (host, editor), `macro-grid-plugin` (release
> script, example workflow, index, author docs).

## What is built

- Category and search tags come from the plugin manifest (`category`, `tags`) through `macrogrid-index.json`. The host cleans and caps them
  (`CatalogText`: category 32 characters, at most 5 tags of 24 characters).
- The icon is a release asset named by the index entry (`"icon": "<id>-<version>.icon.png"`), a file name only, never a URL. `CatalogText.IsIconFileName`
  allows `.png` and `.svg`, at most 80 characters.
- The host builds the address from the newest version's own release (`PluginCatalogIcons`). Both the package URL and the icon URL must pass
  `PluginSourceUrls.BelongsToRepo`, so an entry cannot point at another repository or host. It fetches at most 100 KB, checks the bytes (PNG magic
  number or an `<svg`/`<?xml` start), keeps up to 64 results in memory, and serves them from `GET /api/plugin-catalog/icon?source=&id=`.
- The editor shows the icon on cards and the detail page through that endpoint. The initial letter stays as the fallback. SVG is drawn as an `<img>`.
- The release script (`macro-grid-plugin/scripts/release-plugin.ps1`) copies the manifest's `icon` next to the package and passes the file name,
  category and tags to `update-plugin-index.ps1`. `publish-catalog.ps1 -Only index` then re-signs the whole index.
- Decision recorded: the catalog icon is a release asset, not a file read from the repository's default branch. It is versioned with the package.

## Recommendation: screenshots not now

Reasons:

1. No plugin has a plugin-level `icon` yet. Every `icon` in the official manifests belongs to a widget. The icon path has never run end to end with
   real data. Prove it with one plugin before adding a second image kind.
2. The catalog is small: four plugins can be released by the script. A screenshot strip adds little when there are four cards.
3. Third-party sources lose even category, tags and icon today (leftover 3). Screenshots would have the same gap.
4. Cost if built now: about 1 to 1.5 days over two repositories, plus a review of every screenshot an author submits (it is drawn in the editor).

Trigger to build it: at least three plugins in the official catalog ship a real plugin-level icon, and the catalog has more than about eight
plugins. The spec below is ready for that day.

## Leftovers to do now

1. **Icon check before the dry run** (`macro-grid-plugin`). In `release-plugin.ps1` the icon is checked after the dry-run `return` (line 158), and a
   bad icon (missing file, wrong type, over 100 KB) is skipped without a word. Move the check above the dry-run return. Throw on a bad icon so the
   author sees it. Copy the asset in the same place, so the dry run's output folder shows it.
2. **One official plugin gets a plugin-level icon** (`macro-grid-plugin`). Add a square `.svg` or `.png` (at most 100 KB) and `"icon"` to its
   `plugin.json`, then release it. A release is outward-facing: the owner approves it first. This is the end-to-end proof of the icon path.
3. **Third-party releases keep their store fields** (`macro-grid-plugin`). `update-plugin-index.ps1` (line 89) always removes `category`, `tags`
   and `icon`, then adds back only what it is given. `examples/third-party-release.yml` passes none of them. So every third-party release strips
   them. Fix: the example workflow reads `category` and `tags` from the manifest, copies the icon as an asset like the release script, and passes
   `-Category`, `-Tags` and `-Icon`. Pass the tags as JSON through an env var, as the permissions are passed.
4. **Author docs** (`macro-grid-plugin`). `website/reference/source-index.md` does not document `icon`, `category` or `tags`. Add the three fields
   with their limits. Add one paragraph to `website/guides/publishing.md`. Write both in en and tr (`website/tr/...`).
5. **Safe headers on icon responses** (`macro-grid`). The host sends no Content-Security-Policy and no `nosniff` header. The icon endpoints serve an
   author-written SVG as `image/svg+xml` from the editor's own origin. Inside an `<img>` it cannot run anything. Opened directly as a URL, a script
   in it would run on the host's origin. Fix: on `GET /api/plugin-catalog/icon` and `GET /api/plugins/{id}/icon`, add
   `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox` and `X-Content-Type-Options: nosniff`. Do not add an
   editor-wide `img-src` rule: profile widgets may show remote images. Test: both endpoints return both headers. This touches the security model:
   add one line to `docs/architecture.md` in the same change.

Commit plan for the leftovers:

- `macro-grid`: `fix(plugins): serve plugin icons with a sandbox policy and nosniff` (leftover 5, with its test and the architecture line).
- `macro-grid-plugin`: `fix(release): check the plugin icon before the dry run` (1), `fix(examples): keep category, tags and icon in
  third-party releases` (3), `docs(reference): document the icon, category and tags index fields` (4). Leftover 2 is a normal plugin release after
  the owner's yes.
- `CHANGELOG.md` (server): nothing for 1 to 4. Leftover 5 is an internal hardening, so it is left out too. `CHANGELOG-developer.md` (plugin repo):
  one line for 3, because third-party authors who copied the example workflow must update their copy.

## Screenshots: build-ready spec (deferred)

### Index field

- Entry-level field `screenshots`: an array of file names, never URLs. Example:
  `"screenshots": ["obs-1.4.0.screenshot-1.png", "obs-1.4.0.screenshot-2.webp"]`.
- At most 3. The host keeps the first 3 valid names, drops duplicates and drops invalid names without an error (like tags).
- New `CatalogText.IsScreenshotFileName`: `^[A-Za-z0-9][A-Za-z0-9._-]*\.(png|webp)$`, case-insensitive, at most 80 characters. `CatalogText.MaxScreenshots = 3`.
- **No SVG.** A screenshot is a raster image. SVG would add the script risk of leftover 5 at a larger size for no gain.
- New property `PluginCatalogEntry.Screenshots` (`IReadOnlyList<string>`, default empty), set in `PluginCatalogClient.ParseEntry` next to `Icon`.
- The field is additive. `formatVersion` stays 2. Older hosts ignore it.

### Signing

- The field sits inside the signed index payload, so it is covered by the signature. Nothing new is needed: `publish-catalog.ps1 -Only index`
  re-signs the whole payload and raises `sequence` on every release.
- Size: 3 names add under 200 bytes per entry. The `SignedCatalogFile` limit is 2 MB. No change.

### Host fetch and cache (`macro-grid`)

- Add `GetScreenshotAsync(owner, repo, entry, n, cancellationToken)` to `PluginCatalogIcons`. Share the address and fetch code with `GetAsync`
  (one private helper). Do not rename the class.
- Address: the newest version's release, exactly as for the icon. The newest version's first `url` must pass `BelongsToRepo`, and so must the built
  screenshot URL. Otherwise: null.
- Fetch with `PluginPackageDownloader.FetchBytesAsync` (allowed hosts only, redirects checked on every hop), cap `MaxScreenshotBytes = 300 * 1024`.
- Type check on the bytes, never the server's `Content-Type` header: PNG is `89 50 4E 47` at offset 0; WebP is `RIFF` at offset 0 and `WEBP` at
  offset 8. The file extension must match the bytes. Anything else: null.
- Cache: a separate dictionary with a byte budget of 4 MB (enough for all screenshots of about four plugins). When an insert would pass the budget,
  clear the cache first, as the icon cache does. Cache a refused file (null) too. Do not cache a network failure, so it can pass.

### Endpoint (`macro-grid`)

- `GET /api/plugin-catalog/screenshot?source=&id=&n=`, `n` from 0 to 2. Same shape as the icon endpoint: use the saved index copy, never go to the
  network for the index when a copy exists.
- 404 for an unknown source or id, `n` out of range, or any fetch or type failure.
- Response: the bytes with `image/png` or `image/webp`, plus the two headers of leftover 5.
- `DescribeEntry` adds `screenshotCount = entry.Screenshots.Count`. The editor needs nothing else.

### Editor (`macro-grid`)

- `api/types.ts`: `screenshotCount?: number` on the catalog entry. `api/client.ts`: `pluginCatalogScreenshotUrl(source, id, n)`, built like
  `pluginCatalogIconUrl`.
- `windows/PluginsWindow.tsx`, detail page only: a "Screenshots" section between the tags and "About". Cards never request a screenshot.
- A row of up to 3 `<img loading="lazy">`, height 160, `object-fit: contain`, horizontal scroll when narrow. The URLs are built only when the
  detail page is open.
- Click opens one image in an overlay at up to 90% of the window. Close with the backdrop or Escape (`useBackdropClose`).
- `onError` hides that image. When all fail, the section is hidden.
- `alt` uses the alt key below.

i18n keys (`en.ts`, `tr.ts`, and `params.ts` for the parameter):

| Key | en | tr |
|---|---|---|
| `plugins.discover.screenshots` | Screenshots | Ekran görüntüleri |
| `plugins.discover.screenshotAlt` | Screenshot {n} | Ekran görüntüsü {n} |
| `plugins.discover.screenshotClose` | Close | Kapat |

`params.ts`: `"plugins.discover.screenshotAlt": ["n"]`.

### Failure behaviour

- A missing field, a bad name, a missing asset, a wrong type, a file over 300 KB or a network error: that image is not shown. Nothing else
  changes. No error text, no entry in the Error List.
- The index never fails to parse because of `screenshots`.

### Security review points

- Fixed hosts: only `PluginSourceUrls` allowed hosts, via the existing downloader. The index carries names, never URLs.
- Same repository: both the package URL and the screenshot URL pass `BelongsToRepo`.
- Bytes decide the type. No SVG. Size capped before the whole body is read.
- The editor never loads a remote image. It only loads `/api/plugin-catalog/screenshot` on its own origin.
- Response headers: sandbox policy and `nosniff`.
- Memory: at most 3 × 300 KB per plugin, 4 MB in total.

### Tests (`macro-grid`)

- `CatalogText`: valid and invalid screenshot names (`a.png` yes, `a.webp` yes, `a.svg` no, `../a.png` no, 81 characters no).
- Parser: more than 3 names keeps the first 3 valid ones; duplicates dropped; a non-array or a missing field gives an empty list.
- `PluginCatalogIcons` (extend `PluginCatalogIconsTests`): PNG and WebP accepted; SVG bytes, other bytes and a wrong extension refused; over 300 KB
  gives null; the URL is built from the newest release; a download URL from another repository gives null and fetches nothing; a cached result is
  not fetched again; a network failure is not cached.
- Endpoint: `n` out of range and an unknown id give 404; a valid one gives the right content type and both headers.
- `DescribeEntry` returns `screenshotCount`.

### Plugin repository checklist (`macro-grid-plugin`)

- [ ] Manifest field `screenshots`: up to 3 paths inside the plugin folder, `.png` or `.webp`, at most 300 KB each. The host does not read this
      field from installed manifests; only the release script does.
- [ ] `release-plugin.ps1`: before the dry-run return, check each file (exists, extension, size). Throw on a bad one. Copy each as
      `<id>-<version>.screenshot-<n>.<ext>` (n from 1) and add them to the release assets.
- [ ] `update-plugin-index.ps1`: new `-Screenshots` string array. Add `screenshots` to the removal list on line 89, so a release without screenshots
      does not keep old names that point at a release that has no such files.
- [ ] `examples/third-party-release.yml`: the same copy and the same argument (after leftover 3).
- [ ] Author docs: `source-index.md` and `publishing.md`, en and tr. Recommend 16:9, about 1280 × 720, no personal data in the image.
- [ ] `docs/CHANGELOG-developer.md`: one line for the new manifest and index field.

### Commit plan (screenshots)

1. `macro-grid`: `feat(plugins): read screenshot names from the catalog index` (CatalogText, model, parser, tests).
2. `macro-grid`: `feat(plugins): serve catalog screenshots from the host` (fetch, cache, endpoint, `screenshotCount`, tests).
3. `macro-grid`: `feat(editor): show screenshots on the Discover detail page` (UI, i18n en and tr, `params.ts`). `CHANGELOG.md`: "New: plugin
   pages in Discover can show screenshots."
4. `macro-grid-plugin`: `feat(release): publish plugin screenshots with the release` (script, index writer, example workflow, docs, developer changelog).

Order: 1 to 3 can merge before 4. With no `screenshots` in any index, nothing changes for anyone.

### Manual tests for the owner

1. Release a test plugin with two screenshots (one PNG, one WebP) to a test source. The release has both assets and the index entry lists both names.
2. Open Discover: no screenshot requests while the grid is shown (check the network panel of the editor's developer tools).
3. Open the plugin's detail page: both images appear. Click one: the overlay opens; Escape closes it.
4. Delete one asset from the release, restart the host, open the page again: only the other image appears, no error text.
5. Release the next version without screenshots: the entry has no `screenshots` field and the section is gone.
6. Open `/api/plugin-catalog/screenshot?...` directly in a browser: the response has the sandbox policy and `nosniff` headers.

## Install counts and ratings

Not planned. They need a server that counts. Release download counts from the code host are the only free signal, and reading them needs that
host's rate-limited API, which the distribution design avoids on purpose.

## Rules that apply to every new field

Every field is written by a plugin author and drawn by the editor. The host validates and caps it (`CatalogText`). A missing field is always fine.
The index `formatVersion` does not change for an additive field. The index has two writers in `macro-grid-plugin`: `release-plugin.ps1` and the
copyable `examples/third-party-release.yml`, both through `update-plugin-index.ps1`. A new field goes into all three.

## Open question

1. Screenshots: wait for the trigger above (recommended), or build now at about 1 to 1.5 days?
