import { useEffect, useState } from "react";
import { useT } from "../i18n/I18nContext";
import { ContextMenu, type ContextMenuEntry } from "./ContextMenu";
import { fieldState, selectedText, textFieldAt, type FieldState, type TextField } from "./textFieldActions";

interface Open {
  x: number;
  y: number;
  field: TextField;
  state: FieldState;
  /** False only when the clipboard was read and is empty; an unreadable clipboard leaves Paste on. */
  clipboardHasText: boolean;
}

/** The editor's own right-click menu for text fields (Undo, Redo, Cut, Copy, Paste, Select all), in place of the browser's. Mounted once for the
 * whole window; it listens for the right-click itself. Paste reads the clipboard through the clipboard API and falls back to the browser's paste
 * command, which the embedded view may refuse (then the Ctrl+V shortcut still works in the field). */
export function TextFieldMenu() {
  const { t } = useT();
  const [open, setOpen] = useState<Open | null>(null);

  useEffect(() => {
    const onContextMenu = (e: MouseEvent) => {
      const field = textFieldAt(e.target);
      if (!field) return;
      e.preventDefault();
      setOpen({ x: e.clientX, y: e.clientY, field, state: fieldState(field), clipboardHasText: true });
      navigator.clipboard?.readText().then(
        (text) => setOpen((current) => (current && current.field === field ? { ...current, clipboardHasText: text.length > 0 } : current)),
        () => {},
      );
    };
    document.addEventListener("contextmenu", onContextMenu);
    return () => document.removeEventListener("contextmenu", onContextMenu);
  }, []);

  if (!open) return null;
  const { field, state } = open;
  const run = (action: () => void | Promise<void>) => () => {
    field.focus();
    void action();
  };

  const copyText = async () => {
    if (!document.execCommand("copy")) await navigator.clipboard?.writeText(selectedText(field)).catch(() => {});
  };
  const paste = async () => {
    try {
      const text = await navigator.clipboard.readText();
      // insertText keeps the field's undo history and fires the usual input event, so the form sees the change.
      if (text && !document.execCommand("insertText", false, text)) document.execCommand("paste");
    } catch {
      document.execCommand("paste"); // refused here: nothing more to do, the Ctrl+V shortcut still works in the field
    }
  };
  const selectAll = () => {
    if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) field.select();
    else document.execCommand("selectAll");
  };

  const items: ContextMenuEntry[] = [
    { label: t("edit.undo"), disabled: !state.editable, onSelect: run(() => void document.execCommand("undo")) },
    { label: t("edit.redo"), disabled: !state.editable, onSelect: run(() => void document.execCommand("redo")) },
    { divider: true },
    { label: t("edit.cut"), disabled: !state.editable || !state.hasSelection, onSelect: run(() => void document.execCommand("cut")) },
    { label: t("edit.copy"), disabled: !state.hasSelection, onSelect: run(copyText) },
    { label: t("edit.paste"), disabled: !state.editable || !open.clipboardHasText, onSelect: run(paste) },
    { divider: true },
    { label: t("edit.selectAll"), disabled: !state.hasText, onSelect: run(selectAll) },
  ];
  return <ContextMenu x={open.x} y={open.y} items={items} onClose={() => setOpen(null)} />;
}
