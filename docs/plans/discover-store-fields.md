# Discover: icons, screenshots and ratings

> Status: **the icon, category and tags are built; screenshots and ratings are not.** Category and search tags come from the plugin manifest (`category`, `tags`) through `macrogrid-index.json`; the icon is a release asset named by the index entry (`icon`) and fetched by the host (`PluginCatalogIcons`, `GET /api/plugin-catalog/icon`). The release script copies the manifest's `icon` (.png or .svg, at most 100 KB) next to the package; no official plugin has a plugin-level icon yet. **Repositories:** `macro-grid` (host, editor), `macro-grid-plugin` (release script, index).

What is left of a store-like Discover tab: a real plugin icon on the cards, screenshots on the detail page, and later install counts or a rating.

## Plugin icon (built)

- The manifest already has an optional `icon` (a square `.svg` or `.png`, at most 100 KB) used by the installed-plugins list. For the catalog the release
  script copies the icon next to the release as an asset (`<id>-<version>.icon.<ext>`) and the index entry gets `"icon": "<file name>"`, a name only,
  never a URL. The host builds the URL itself from the repository it fetched the index from (`PluginSourceUrls`), so an entry cannot point the editor at
  another host.
- The editor never loads a remote image directly. The host fetches it (same download rules as a package: fixed host, size cap of 100 KB, content type
  checked), keeps it in memory, and serves it from `GET /api/plugin-catalog/{source}/{id}/icon`. Cards show it, and the initial letter stays as the fallback.
- SVG is drawn as an `<img>` (nothing in it can run), as for installed plugins.

## Screenshots

- Up to 3 per plugin, `.png` or `.webp`, at most 300 KB each, listed as file names under `screenshots` in the index entry, fetched and served like the icon,
  and only when the person opens the detail page (never for the whole grid).
- Decision for the owner: is it worth the release assets and the review burden for a hobby-sized catalog? Recommendation: icon now, screenshots when
  there are more than a handful of plugins.

## Install counts and ratings

Not without a server that counts. GitHub release download counts are the only free signal; reading them needs the GitHub API (rate limit, which the
distribution design avoids on purpose). Recommendation: not planned.

## Rules that apply to every new field

Every field is written by a plugin author and drawn by the editor: the host validates and caps it (`CatalogText`), a missing field is always fine, the
index `formatVersion` does not change for an additive field, and `macro-grid-plugin`'s release script is the only writer.

## Open questions

1. Icon in the catalog as a release asset (above) or read from the repository's default branch? Release asset is versioned and matches the signed package.
2. Screenshots: yes or later?
