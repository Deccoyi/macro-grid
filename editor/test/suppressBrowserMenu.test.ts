import { describe, expect, it } from "vitest";
import { suppressBrowserMenu } from "../src/suppressBrowserMenu";

function rightClick(el: Element): boolean {
  const e = new MouseEvent("contextmenu", { bubbles: true, cancelable: true });
  el.dispatchEvent(e);
  return e.defaultPrevented;
}

describe("suppressBrowserMenu", () => {
  it("blocks the browser menu on empty places and keeps it in text fields", () => {
    suppressBrowserMenu();
    document.body.innerHTML = '<div id="d"></div><input id="t" type="text"><textarea id="a"></textarea><input id="c" type="checkbox"><div id="e" contenteditable="true"></div>';
    const get = (id: string) => document.getElementById(id)!;
    expect(rightClick(get("d"))).toBe(true);
    expect(rightClick(get("c"))).toBe(true);
    expect(rightClick(get("t"))).toBe(false);
    expect(rightClick(get("a"))).toBe(false);
    expect(rightClick(get("e"))).toBe(false);
  });
});
