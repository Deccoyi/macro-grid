const PLUGIN_IMAGE = /^plugin-image:([^/]+)\/(.+)$/;

/** True for a `plugin-image:<pluginId>/<imageId>` reference — the only kind of option icon that can become a button picture. */
export function isPluginImage(icon: string | null | undefined): icon is string {
  return typeof icon === "string" && PLUGIN_IMAGE.test(icon);
}

/** Maps a plugin-image reference to the editor image endpoint; anything else (a built-in icon name, nothing) has no picture. */
export function resolveOptionIcon(icon: string | null | undefined): string | null {
  const m = typeof icon === "string" ? PLUGIN_IMAGE.exec(icon) : null;
  return m ? `/api/plugins/${encodeURIComponent(m[1]!)}/images/${encodeURIComponent(m[2]!)}` : null;
}

const missing = new Set<string>();
const requested = new Set<string>();

/** True once the picture 404ed this session; thumbnails stay empty instead of retrying. */
export function isImageMissing(icon: string | null | undefined): boolean {
  return typeof icon === "string" && missing.has(icon);
}

export function markImageMissing(icon: string | null | undefined): void {
  if (typeof icon === "string") missing.add(icon);
}

/**
 * Asks the host to fetch and cache a picture from its plugin (`?request=1`). Called when the user actually uses the picture
 * (picks the option as the button icon); browsing never calls it. Fires once per reference and ignores failures.
 */
export function requestPluginImage(icon: string | null | undefined): void {
  const url = resolveOptionIcon(icon);
  if (!url || !icon || requested.has(icon)) return;
  requested.add(icon);
  missing.delete(icon);
  void fetch(`${url}?request=1`).catch(() => {});
}

/** The URL to load for any stored icon: a plugin-image reference goes through the endpoint, a data: URI stays as it is. */
export function iconSrc(icon: string): string {
  return resolveOptionIcon(icon) ?? icon;
}
