import type { DictKey } from "../i18n/tr";

/**
 * One user-facing action reachable from a context menu, a top menu and a keyboard shortcut alike
 * (docs/design/editor-edit-commands.md, "Command registry") — the point of the registry is that all
 * three paths run exactly the same `run()`.
 */
export interface Command {
  /** "edit.undo", "edit.copy", ... — stable, never shown directly. */
  id: string;
  labelKey: DictKey;
  /** Overrides `labelKey` when the label carries live state, e.g. "Undo Move widget". */
  label?: () => string;
  /** Display and matching form: "Ctrl+Z", "Ctrl+Shift+4", "Delete", "F2". Menus show the first. */
  shortcuts?: string[];
  enabled: () => boolean;
  run: () => void | Promise<void>;
}
