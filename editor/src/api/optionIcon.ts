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

/** The URL to load for any stored icon: a plugin-image reference goes through the endpoint, a data: URI stays as it is. */
export function iconSrc(icon: string): string {
  return resolveOptionIcon(icon) ?? icon;
}
