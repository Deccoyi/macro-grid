import type { DictKey } from "../i18n/tr";

/** Most undo steps kept; the oldest is dropped past this (lightweight resource budget). */
export const HISTORY_LIMIT = 100;

/** Two changes with the same coalesce key closer together than this become one undo step (typing in a
 * field, dragging a slider, several rect changes from one widget drag). */
export const COALESCE_WINDOW_MS = 600;

/** One remembered state. `id` identifies the document state it holds (see `History.currentId`), and
 * `label` names the step that leads away from it: the step Undo reverts when this entry is on top of
 * `past`, or the step Redo re-applies when it is on top of `future`. */
export interface HistoryEntry<S> {
  state: S;
  id: number;
  label: DictKey;
}

/**
 * Snapshot undo/redo history (docs/design/editor-edit-commands.md, "Undo / redo"). Pure data plus the
 * pure functions below, so the rules can be tested without React: the profile document hook keeps one of
 * these in a ref and applies the returned states itself.
 *
 * Every document state gets an id. `savedId` is the id of the state last saved, so "dirty" is simply
 * `currentId !== savedId`: undoing back to the saved state clears the dirty mark and redoing past it sets
 * it again, and dropping the oldest entry at the cap never shifts anything.
 */
export interface History<S> {
  past: HistoryEntry<S>[];
  future: HistoryEntry<S>[];
  currentId: number;
  savedId: number;
  nextId: number;
  lastCoalesceKey: string | null;
  lastChangeAt: number;
}

export function createHistory<S>(): History<S> {
  return { past: [], future: [], currentId: 0, savedId: 0, nextId: 1, lastCoalesceKey: null, lastChangeAt: 0 };
}

/**
 * Records a change: `previous` is the state before it. Pushes `previous` onto `past` (unless the change
 * coalesces into the step already on top) and clears `future`. A change never coalesces into a step
 * across a save, so Undo can always get back to exactly what was saved.
 */
export function recordChange<S>(
  history: History<S>,
  previous: S,
  label: DictKey,
  options: { coalesceKey?: string; now: number },
): History<S> {
  const { coalesceKey, now } = options;
  const coalesce =
    !!coalesceKey &&
    coalesceKey === history.lastCoalesceKey &&
    now - history.lastChangeAt < COALESCE_WINDOW_MS &&
    history.past.length > 0 &&
    history.future.length === 0 &&
    history.currentId !== history.savedId;

  const id = history.nextId;
  if (coalesce) {
    return { ...history, currentId: id, nextId: id + 1, lastChangeAt: now };
  }
  const past = [...history.past, { state: previous, id: history.currentId, label }];
  if (past.length > HISTORY_LIMIT) past.splice(0, past.length - HISTORY_LIMIT);
  return {
    ...history,
    past,
    future: [],
    currentId: id,
    nextId: id + 1,
    lastCoalesceKey: coalesceKey ?? null,
    lastChangeAt: now,
  };
}

/** Steps back one change. `current` is the state now showing; it goes onto `future` for Redo. Null when
 * there is nothing to undo. */
export function undo<S>(history: History<S>, current: S): { history: History<S>; state: S } | null {
  const top = history.past[history.past.length - 1];
  if (!top) return null;
  return {
    state: top.state,
    history: {
      ...history,
      past: history.past.slice(0, -1),
      future: [...history.future, { state: current, id: history.currentId, label: top.label }],
      currentId: top.id,
      lastCoalesceKey: null,
    },
  };
}

/** Re-applies the change Undo last reverted. Null when there is nothing to redo. */
export function redo<S>(history: History<S>, current: S): { history: History<S>; state: S } | null {
  const top = history.future[history.future.length - 1];
  if (!top) return null;
  return {
    state: top.state,
    history: {
      ...history,
      future: history.future.slice(0, -1),
      past: [...history.past, { state: current, id: history.currentId, label: top.label }],
      currentId: top.id,
      lastCoalesceKey: null,
    },
  };
}

/** The state with id `savedId` (by default the current one) has just been saved. Passing the id taken
 * when the save started keeps a change made while the request was in flight marked unsaved. */
export function markSaved<S>(history: History<S>, savedId: number = history.currentId): History<S> {
  return { ...history, savedId, lastCoalesceKey: savedId === history.currentId ? null : history.lastCoalesceKey };
}

export function isDirty<S>(history: History<S>): boolean {
  return history.currentId !== history.savedId;
}

/** The label of the step Undo would revert, or null. */
export function undoLabel<S>(history: History<S>): DictKey | null {
  return history.past[history.past.length - 1]?.label ?? null;
}

/** The label of the step Redo would re-apply, or null. */
export function redoLabel<S>(history: History<S>): DictKey | null {
  return history.future[history.future.length - 1]?.label ?? null;
}
