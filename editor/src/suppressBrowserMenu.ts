/** True for the places where the browser's own menu is useful (copy, paste, spell check): text fields and editable text. */
function isTextTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable || target.closest("[contenteditable=\"true\"], [contenteditable=\"\"]")) return true;
  if (target instanceof HTMLTextAreaElement) return true;
  if (!(target instanceof HTMLInputElement)) return false;
  return !["button", "checkbox", "radio", "range", "color", "file", "image", "reset", "submit"].includes(target.type);
}

/** The editor never shows the browser's own menu (Back, Forward, Reload): its panels open their own menus. Text fields keep the normal one so paste works. */
export function suppressBrowserMenu(target: Document = document): void {
  target.addEventListener("contextmenu", (e) => {
    if (!isTextTarget(e.target)) e.preventDefault();
  });
}
