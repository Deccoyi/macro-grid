import { describe, expect, it } from "vitest";
import { suppressBrowserMenu } from "../src/suppressBrowserMenu";
import { fieldState, selectedText, textFieldAt } from "../src/panels/textFieldActions";

function rightClick(el: Element): boolean {
  const e = new MouseEvent("contextmenu", { bubbles: true, cancelable: true });
  el.dispatchEvent(e);
  return e.defaultPrevented;
}

describe("browser menu", () => {
  it("is blocked everywhere", () => {
    suppressBrowserMenu();
    document.body.innerHTML = '<div id="d"></div><input id="t" type="text">';
    expect(rightClick(document.getElementById("d")!)).toBe(true);
    expect(rightClick(document.getElementById("t")!)).toBe(true);
  });

  it("finds the text field under a click and what its menu may offer", () => {
    document.body.innerHTML =
      '<input id="t" value="hello"><input id="r" value="x" readonly><input id="c" type="checkbox"><div id="e" contenteditable="true"><b id="b">hi</b></div><div id="d"></div>';
    const get = (id: string) => document.getElementById(id)!;
    expect(textFieldAt(get("t"))).toBe(get("t"));
    expect(textFieldAt(get("c"))).toBeNull();
    expect(textFieldAt(get("d"))).toBeNull();
    expect(textFieldAt(get("b"))).toBe(get("e"));

    const input = get("t") as HTMLInputElement;
    expect(fieldState(input)).toEqual({ editable: true, hasSelection: false, hasText: true });
    input.setSelectionRange(1, 3);
    expect(fieldState(input).hasSelection).toBe(true);
    expect(fieldState(get("r") as HTMLInputElement).editable).toBe(false);
  });
});

describe("fields that hide their selection range", () => {
  it("reads the page selection for a number input (selectionStart is null there)", () => {
    document.body.innerHTML = '<input id="n" type="number" value="12">';
    const input = document.getElementById("n") as HTMLInputElement;
    expect(input.selectionStart).toBeNull();
    expect(fieldState(input).hasSelection).toBe(false);
    const original = window.getSelection;
    window.getSelection = (() => ({ toString: () => "12" })) as unknown as typeof window.getSelection;
    try {
      expect(fieldState(input).hasSelection).toBe(true);
      expect(selectedText(input)).toBe("12");
    } finally {
      window.getSelection = original;
    }
  });
});

describe("remembered copy", () => {
  it("keeps the last non-empty copy for Paste", async () => {
    const { copiedText, rememberCopied } = await import("../src/panels/textFieldActions");
    expect(copiedText()).toBe("");
    rememberCopied("abc");
    rememberCopied("");
    expect(copiedText()).toBe("abc");
  });
});
