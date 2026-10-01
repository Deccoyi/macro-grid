import { useSyncExternalStore } from "react";
import type { SelfPreviewState } from "../grid/selfPreview";

/** Which look the canvas previews for one button that uses its own state. Editor UI only: never saved in the profile. The Inspector sets it and the
 * canvas reads it, the same module-level store pattern as pluginTreeSelectionStore.ts. */
export interface SelfPreview { widgetId: string; state: SelfPreviewState }

let current: SelfPreview | null = null;
const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function setSelfPreview(next: SelfPreview | null): void {
  if (current?.widgetId === next?.widgetId && current?.state === next?.state) return;
  current = next;
  for (const listener of listeners) listener();
}

export function useSelfPreview(): SelfPreview | null {
  return useSyncExternalStore(subscribe, () => current);
}
