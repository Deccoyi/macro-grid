import { useEffect, useState } from "react";
import { useT } from "../i18n/I18nContext";
import { ContextMenu, type ContextMenuEntry } from "./ContextMenu";
import { copiedText, fieldState, rememberCopied, selectedText, textFieldAt, type FieldState, type TextField } from "./textFieldActions";

interface Open {
  x: number;
  y: number;
  field: TextField;
  state: FieldState;
  /** What reading the system clipboard said: its text, nothing in it, or a refusal (the embedded view never allows it). */
  clipboard: "text" | "empty" | "refused";
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
      setOpen({ x: e.clientX, y: e.clientY, field, state: fieldState(field), clipboard: "refused" });
      navigator.clipboard?.readText().then(
        (text) => setOpen((current) => (current && current.field === field ? { ...current, clipboard: text.length > 0 ? "text" : "empty" } : current)),
        () => {},
      );
    };
    // Every Copy and Cut in the editor (menu or Ctrl+C / Ctrl+X) is remembered, for Paste when the system clipboard cannot be read.
    const onCopy = (e: ClipboardEvent) => {
      const field = textFieldAt(e.target);
      rememberCopied(field ? selectedText(field) : (window.getSelection()?.toString() ?? ""));
    };
    document.addEventListener("contextmenu", onContextMenu);
    document.addEventListener("copy", onCopy);
    document.addEventListener("cut", onCopy);
    return () => {
      document.removeEventListener("contextmenu", onContextMenu);
      document.removeEventListener("copy", onCopy);
      document.removeEventListener("cut", onCopy);
    };
  }, []);

  if (!open) return null;
  const { field, state } = open;
  const run = (action: () => void | Promise<void>) => () => {
    field.focus();
    void action();
  };

  const copyText = async () => {
    if (!document.execCommand("copy")) {
      rememberCopied(selectedText(field));
      await navigator.clipboard?.writeText(selectedText(field)).catch(() => {});
    }
  };
  const paste = async () => {
    // insertText keeps the field's undo history and fires the usual input event, so the form sees the change.
    const insert = (text: string) => text.length > 0 && document.execCommand("insertText", false, text);
    try {
      if (insert(await navigator.clipboard.readText())) return;
    } catch {
      // The embedded view refuses clipboard reads: use what was last copied inside the editor.
    }
    insert(copiedText());
  };
  // Only when the clipboard cannot be read and nothing was copied here is Paste unavailable; Ctrl+V still works natively in the field.
  const pasteNeedsShortcut = open.clipboard === "refused" && copiedText().length === 0;
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
    { label: pasteNeedsShortcut ? `${t("edit.paste")} (Ctrl+V)` : t("edit.paste"), disabled: !state.editable || open.clipboard === "empty" || pasteNeedsShortcut, onSelect: run(paste) },
    { divider: true },
    { label: t("edit.selectAll"), disabled: !state.hasText, onSelect: run(selectAll) },
  ];
  return <ContextMenu x={open.x} y={open.y} items={items} onClose={() => setOpen(null)} />;
}
