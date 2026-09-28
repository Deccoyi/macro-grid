/**
 * Matches a keydown event against a shortcut string like "Ctrl+Z", "Ctrl+Shift+4", "Delete" or "F2"
 * (docs/design/editor-edit-commands.md, "Command registry"). Windows only, so Ctrl is the one modifier —
 * no Cmd/Meta handling. No Ctrl+Alt combinations are ever registered (Ctrl+Alt is AltGr on the Turkish Q
 * layout), so this doesn't need to treat Alt specially there.
 */
export function matchesShortcut(e: KeyboardEvent, shortcut: string): boolean {
  const parts = shortcut.split("+");
  const key = parts[parts.length - 1]!;
  const mods = new Set(parts.slice(0, -1).map((p) => p.toLowerCase()));
  if (mods.has("ctrl") !== e.ctrlKey) return false;
  if (mods.has("shift") !== e.shiftKey) return false;
  if (mods.has("alt") !== e.altKey) return false;
  // A single-letter key ("Z", "D") matches case-insensitively; a named key (Delete, Insert, Backspace,
  // F2, Escape) matches KeyboardEvent.key exactly.
  return key.length === 1 ? e.key.toLowerCase() === key.toLowerCase() : e.key === key;
}

/** True while focus is in something with its own native text editing (an input, textarea, select or
 * contenteditable) — shortcuts other than Escape must not fire there so the field's own undo/copy/paste
 * keeps working (docs/design/editor-edit-commands.md, "Command registry"). */
export function isTextFieldFocused(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  return target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.tagName === "SELECT" || target.isContentEditable;
}
