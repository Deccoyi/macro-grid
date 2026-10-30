export type TextField = HTMLInputElement | HTMLTextAreaElement | HTMLElement;

const NON_TEXT_INPUTS = ["button", "checkbox", "radio", "range", "color", "file", "image", "reset", "submit"];

/** The text field a right-click landed in (input, textarea or editable text), or null for anything else. */
export function textFieldAt(target: EventTarget | null): TextField | null {
  if (!(target instanceof HTMLElement)) return null;
  if (target instanceof HTMLTextAreaElement) return target;
  if (target instanceof HTMLInputElement) return NON_TEXT_INPUTS.includes(target.type) ? null : target;
  return target.closest<HTMLElement>('[contenteditable="true"], [contenteditable=""]');
}

export interface FieldState {
  editable: boolean;
  hasSelection: boolean;
  hasText: boolean;
}

/** The selected text of an input or textarea. Some input types (number, email) do not expose their selection range (the browser gives null),
 * so the page's own selection is read instead. */
export function selectedText(field: TextField): string {
  if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) {
    if (field.selectionStart === null || field.selectionEnd === null) return window.getSelection()?.toString() ?? "";
    return field.value.slice(field.selectionStart, field.selectionEnd);
  }
  return window.getSelection()?.toString() ?? "";
}

/** What the menu may offer for a field right now: Cut and Paste need an editable field, Cut and Copy a selection, Select all some text. */
export function fieldState(field: TextField): FieldState {
  if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) {
    // A password field never offers Cut or Copy.
    const secret = field instanceof HTMLInputElement && field.type === "password";
    return { editable: !field.readOnly && !field.disabled, hasSelection: !secret && selectedText(field).length > 0, hasText: field.value.length > 0 };
  }
  return { editable: true, hasSelection: selectedText(field).length > 0, hasText: (field.textContent ?? "").length > 0 };
}

let lastCopied = "";

/** The text of the last Copy or Cut done inside the editor. The embedded view refuses to let the page read the system clipboard, so the menu's
 * Paste inserts this instead; text copied in another program only pastes with Ctrl+V. */
export function rememberCopied(text: string): void {
  if (text.length > 0) lastCopied = text;
}

export function copiedText(): string {
  return lastCopied;
}
