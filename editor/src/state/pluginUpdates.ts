import { useEffect, useSyncExternalStore } from "react";
import { api } from "../api/client";

/** How many installed plugins have a newer version in the official catalog. Module-level store (the same pattern as
 * pluginTreeSelectionStore.ts) so the status bar and the Plugins menu show one number from one request. The Plugins tool
 * window is its own window, so an update made there is picked up when this window gets focus again. */
let count = 0;
const listeners = new Set<() => void>();
let started = false;

function set(next: number): void {
  if (next === count) return;
  count = next;
  for (const listener of listeners) listener();
}

function refresh(): void {
  api.fetchPluginCatalog("official")
    .then((response) => set(response.plugins?.filter((p) => p.installed && p.updateAvailable).length ?? 0))
    .catch(() => {});
}

function start(): void {
  if (started) return;
  started = true;
  refresh();
  window.addEventListener("focus", refresh);
  window.setInterval(refresh, 30 * 60 * 1000);
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function usePluginUpdateCount(): number {
  useEffect(start, []);
  return useSyncExternalStore(subscribe, () => count);
}
