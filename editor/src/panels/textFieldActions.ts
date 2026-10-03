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

/** What the menu may offer for a field right now: Cut and Paste need an editable field, Cut and Copy a selection, Select all some text. */
export function fieldState(field: TextField): FieldState {
  if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) {
    const start = field.selectionStart ?? 0;
    const end = field.selectionEnd ?? 0;
    // A password field never offers Cut or Copy.
    const secret = field instanceof HTMLInputElement && field.type === "password";
    return { editable: !field.readOnly && !field.disabled, hasSelection: !secret && end > start, hasText: field.value.length > 0 };
  }
  const text = window.getSelection()?.toString() ?? "";
  return { editable: true, hasSelection: text.length > 0, hasText: (field.textContent ?? "").length > 0 };
}

export function selectedText(field: TextField): string {
  if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) return field.value.slice(field.selectionStart ?? 0, field.selectionEnd ?? 0);
  return window.getSelection()?.toString() ?? "";
}
