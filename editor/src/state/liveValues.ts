import { useSyncExternalStore } from "react";
import type { StatusEntry, VariableSnapshot } from "../api/types";

/**
 * The 2-second poll's results (variable values, status items) live outside React state on purpose: they change on every tick
 * (the clock variable never stands still), and keeping them in App's state re-rendered the whole editor each time.
 * Only the components that read them through these hooks re-render.
 */
type Listener = () => void;

let variables: VariableSnapshot = {};
let status: StatusEntry[] = [];
let liveNames = "";
const listeners = new Set<Listener>();

const emit = () => listeners.forEach((l) => l());
const subscribe = (l: Listener) => { listeners.add(l); return () => { listeners.delete(l); }; };

export function setLiveVariables(next: VariableSnapshot) {
  variables = next;
  liveNames = Object.keys(next).filter((k) => next[k] != null).sort().join("\n");
  emit();
}

export function setLiveStatus(next: StatusEntry[]) {
  status = next;
  emit();
}

export const useLiveVariables = () => useSyncExternalStore(subscribe, () => variables);
export const useLiveStatus = () => useSyncExternalStore(subscribe, () => status);
/** The names that have a value, as one text: a primitive, so a subscriber re-renders only when a name appears or goes. */
export const useLiveNames = () => useSyncExternalStore(subscribe, () => liveNames);

/** For tests. */
export function resetLiveValues() {
  variables = {};
  status = [];
  liveNames = "";
  emit();
}
