import { useSyncExternalStore } from "react";
import { api } from "../api/client";
import { isBuiltIn, PSEUDO_TAG, type Language } from "./language";
import { buildPseudoPack } from "./pack/pseudo";
import { checkPack, type CheckedPack } from "./pack/validate";

/**
 * The checked strings of the active language pack, or nothing while a built-in language is active (or the pack is missing or damaged,
 * which shows English). A module store read with useSyncExternalStore, the same pattern as state/pluginUpdates.ts, so every
 * component that translates re-renders when a pack finishes loading. Nothing is cached in browser storage: the server holds the file.
 */
let current: CheckedPack | null = null;
let wantedTag: string | null = null;
let loading = 0;
const listeners = new Set<() => void>();

function set(next: CheckedPack | null): void {
  if (next === current) return;
  current = next;
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}

export function getPack(): CheckedPack | null {
  return current;
}

export function usePack(): CheckedPack | null {
  return useSyncExternalStore(subscribe, getPack);
}

/** Makes the store hold the pack of `language`: clears it for a built-in language, otherwise fetches the pack and checks every string again. */
export async function ensurePack(language: Language): Promise<void> {
  if (isBuiltIn(language)) {
    wantedTag = null;
    set(null);
    return;
  }
  wantedTag = language;
  if (current?.tag !== language) set(null);

  if (import.meta.env.DEV && language === PSEUDO_TAG) {
    set(buildPseudoPack());
    return;
  }

  const ticket = ++loading;
  try {
    const file = await api.getLanguagePack(language);
    if (ticket === loading && wantedTag === language) set(checkPack(file, language));
  } catch {
    if (ticket === loading && wantedTag === language) set(null);
  }
}

/** Reloads the active pack only when the server's version of it changed (an import done in the Preferences window). */
export async function refreshPackIfChanged(language: Language): Promise<void> {
  if (isBuiltIn(language)) return;
  try {
    const listed = (await api.listLanguagePacks()).find((p) => p.tag === language);
    if (!listed) {
      if (current?.tag === language) set(null);
      return;
    }
    if (current?.tag !== language || current.version !== listed.version) await ensurePack(language);
  } catch {
    // The server is not reachable; what is loaded stays.
  }
}
