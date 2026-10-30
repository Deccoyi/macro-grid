/** The editor never shows the browser's own menu (Back, Forward, Reload, Cut, Paste): its panels open their own menus, and text fields get TextFieldMenu. */
export function suppressBrowserMenu(target: Document = document): void {
  target.addEventListener("contextmenu", (e) => e.preventDefault());
}
