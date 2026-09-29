import { useEffect } from "react";
import { useCommands } from "./CommandsContext";
import { isTextFieldFocused, matchesShortcut } from "./shortcuts";

/**
 * One `keydown` listener on `window` for every registered command's shortcuts
 * (docs/design/editor-edit-commands.md, "Command registry"). Renders nothing; mount it once near the
 * root, inside <CommandsProvider>.
 */
export function ShortcutListener() {
  const { all } = useCommands();
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      // Text fields keep their own native undo/copy/paste — every shortcut here is skipped while one has
      // focus, except Escape (clearing a selection, closing a menu, cancelling an inline rename).
      if (isTextFieldFocused(e.target) && e.key !== "Escape") return;
      for (const cmd of all()) {
        if (!cmd.shortcuts || !cmd.enabled()) continue;
        if (cmd.shortcuts.some((s) => matchesShortcut(e, s))) {
          e.preventDefault();
          void cmd.run();
          return;
        }
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [all]);
  return null;
}
