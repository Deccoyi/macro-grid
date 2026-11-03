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
const watched = new Set<string>();
/** How many times each picture arrived after a miss; part of its URL so an image that failed once is fetched again. */
const arrivals = new Map<string, number>();
const listeners = new Set<() => void>();

/** Delays between retries after a miss (about 10 s in total); the plugin answers asynchronously, so the picture may be cached a moment later. */
export const RETRY_DELAYS_MS = [500, 1000, 2000, 4000, 2500];

/** Lets a component re-render when a picture arrives or fails (used with useSyncExternalStore). */
export function subscribeImages(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
const notify = () => listeners.forEach((l) => l());

/** True once the picture 404ed and is not being retried; thumbnails stay empty instead of retrying. */
export function isImageMissing(icon: string | null | undefined): boolean {
  return typeof icon === "string" && missing.has(icon);
}

export function markImageMissing(icon: string | null | undefined): void {
  if (typeof icon === "string" && !missing.has(icon)) { missing.add(icon); notify(); }
}

/** Fetches a picture, retrying with backoff while it is missing; plain GETs only, except the first one when `ask` is set. Gives up after ~10 s. */
function fetchWithRetry(icon: string, url: string, ask: boolean): void {
  let attempt = 0;
  const run = () => {
    void fetch(ask && attempt === 0 ? `${url}?request=1` : url).then((res) => {
      if (res.ok) {
        if (attempt > 0 || missing.has(icon)) {
          missing.delete(icon);
          arrivals.set(icon, (arrivals.get(icon) ?? 0) + 1);
          notify();
        }
        return;
      }
      const delay = RETRY_DELAYS_MS[attempt++];
      if (delay !== undefined) setTimeout(run, delay);
    }).catch(() => {});
  };
  run();
}

/**
 * Asks the host to fetch and cache a picture from its plugin (`?request=1`). Called when the user actually uses the picture
 * (picks the option as the button icon); browsing never calls it. Fires once per reference and retries while the picture is not cached yet.
 */
export function requestPluginImage(icon: string | null | undefined): void {
  const url = resolveOptionIcon(icon);
  if (!url || !icon || requested.has(icon)) return;
  requested.add(icon);
  watched.add(icon);
  missing.delete(icon);
  fetchWithRetry(icon, url, true);
}

/** A stored button picture that is not cached yet (the layout send already asked the plugin): checks again with backoff, never asks the plugin itself. Once per reference. */
function watchPluginImage(icon: string): void {
  if (watched.has(icon)) return;
  watched.add(icon);
  fetchWithRetry(icon, resolveOptionIcon(icon)!, false);
}

/** The URL for a picture thumbnail, or null when it is known to be missing. Never causes a request by itself. */
export function thumbSrc(icon: string | null | undefined): string | null {
  const url = resolveOptionIcon(icon);
  if (!url || isImageMissing(icon)) return null;
  const n = arrivals.get(icon!);
  return n ? `${url}?v=${n}` : url;
}

/** The URL to load for any stored icon: a plugin-image reference goes through the endpoint (and is watched until it arrives), a data: URI stays as it is. */
export function iconSrc(icon: string): string {
  const url = resolveOptionIcon(icon);
  if (!url) return icon;
  watchPluginImage(icon);
  const n = arrivals.get(icon);
  return n ? `${url}?v=${n}` : url;
}
