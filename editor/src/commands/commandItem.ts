import type { ContextMenuItem } from "../panels/ContextMenu";
import type { Command } from "./types";

/** Turns a registered command into a context-menu row: label, first shortcut right-aligned, disabled
 * when `enabled()` is false (docs/design/editor-edit-commands.md, "Command registry"). */
export function commandItem(cmd: Command, t: (key: Command["labelKey"], ...args: string[]) => string): ContextMenuItem {
  return {
    label: cmd.label ? cmd.label() : t(cmd.labelKey),
    shortcut: cmd.shortcuts?.[0],
    disabled: !cmd.enabled(),
    onSelect: () => { void cmd.run(); },
  };
}
