import { useCallback, useRef, useState } from "react";
import { createHistory, recordChange, redo as redoHistory, undo as undoHistory, type History } from "./history";

/**
 * A plain object (a plugin settings form's `values`) with its own undo/redo history — independent of the
 * open profile's (docs/design/editor-edit-commands.md's shared stack covers the profile document only; a
 * plugin's settings are a separate persistence domain, its own `settings.json` on the server, see
 * docs/plugin-authoring.md). Every change is one snapshot, capped the same way the document's history is
 * (`HISTORY_LIMIT` in history.ts) — no coalescing here, so this stays a small, self-contained hook rather
 * than another copy of the document's coalesce-window bookkeeping.
 *
 * Used by both places a plugin's settings form appears — PluginSettingsWindow.tsx (opened from the menu
 * bar or a status item) and PluginTreeItemProperties.tsx (the Properties tool window) — so Ctrl+Z/Ctrl+Y
 * undoes a plugin settings edit in either one.
 */
export function useUndoableValues(initial: Record<string, unknown>) {
  const [values, setValues] = useState(initial);
  const historyRef = useRef<History<Record<string, unknown>>>(createHistory());

  /** Loads a different object with a clean history — call this when the form is pointed at a different
   * plugin/item, not when the person edits a field. */
  const reset = useCallback((next: Record<string, unknown>) => {
    historyRef.current = createHistory();
    setValues(next);
  }, []);

  const set = useCallback((next: Record<string, unknown>) => {
    setValues((prev) => {
      historyRef.current = recordChange(historyRef.current, prev, "undo.edit", { now: Date.now() });
      return next;
    });
  }, []);

  const undo = useCallback(() => {
    setValues((current) => {
      const result = undoHistory(historyRef.current, current);
      if (!result) return current;
      historyRef.current = result.history;
      return result.state;
    });
  }, []);

  const redo = useCallback(() => {
    setValues((current) => {
      const result = redoHistory(historyRef.current, current);
      if (!result) return current;
      historyRef.current = result.history;
      return result.state;
    });
  }, []);

  return { values, set, reset, undo, redo };
}
