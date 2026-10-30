import type { Diagnostic } from "./types";

/** The source id of what the editor notices while the person works (a widget that did not fit, and so on). */
export const EDITOR_EVENTS_SOURCE = "editor-events";

type Listener = (d: Diagnostic) => void;
const listeners = new Set<Listener>();

/**
 * Lets code that runs outside the Diagnostic Messages provider (the hooks that edit the profile) tell it something: the same message again is
 * counted (x2) instead of listed twice. The provider listens; with no provider nothing happens.
 */
export function emitDiagnostic(d: Omit<Diagnostic, "source">): void {
  for (const listener of listeners) listener({ ...d, source: EDITOR_EVENTS_SOURCE });
}

export function onDiagnostic(listener: Listener): () => void {
  listeners.add(listener);
  return () => void listeners.delete(listener);
}

/** A warning that a widget could not be placed because the page has no free cell. */
export function noFreeCellWarning(): Omit<Diagnostic, "source"> {
  return { id: "no-free-cell", severity: "warning", code: "W300", messageKey: "state.noFreeCell" };
}
