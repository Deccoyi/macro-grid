import { useSyncExternalStore } from "react";

/** What the Plugins tool window last selected: either the plugin row itself (its own IPluginSettingsPage,
 * if any), or one node of its tree (a leaf with IPluginTreeItemSettings, or a plain structural node with
 * none). See docs/design/plugins-tool-window.md. */
export type PluginTreeSelection =
  | { kind: "plugin"; pluginId: string; pluginName: string; hasSettings: boolean }
  | { kind: "item"; pluginId: string; pluginName: string; itemId: string; label: string; hasSettings: boolean };

let selection: PluginTreeSelection | null = null;
const listeners = new Set<() => void>();

/** Module-level store (the same pattern as dialogs/dialogStore.ts) so the Plugins tool window and the
 * Properties tool window can share "what is selected" without a context provider — a tool window's Content
 * component takes no props and neither is an ancestor of the other, so this is the plain way to hand a
 * selection from one to the other without an App.tsx edit. */
function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

function getSnapshot(): PluginTreeSelection | null {
  return selection;
}

export function setPluginTreeSelection(next: PluginTreeSelection | null): void {
  if (selection === next) return;
  selection = next;
  for (const listener of listeners) listener();
}

export function clearPluginTreeSelection(): void {
  setPluginTreeSelection(null);
}

export function usePluginTreeSelection(): PluginTreeSelection | null {
  return useSyncExternalStore(subscribe, getSnapshot);
}
