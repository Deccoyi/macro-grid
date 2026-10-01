import { describe, expect, it } from "vitest";
import type { ActionBinding } from "@macro/renderer";
import { MAX_DEPTH, addElse, addIf, isWellFormed, layout, move, remove } from "../src/panels/actionFlow";

/** "if" "else" "end" are the markers; anything else is a plain step named by the text. */
const MARKER: Record<string, string> = { if: "core.if", else: "core.else", end: "core.endIf" };
const list = (...names: string[]): ActionBinding[] => names.map((n) => ({ type: MARKER[n] ?? `t.${n}`, settings: {} }));
const names = (bindings: ActionBinding[]) => bindings.map((b) => Object.entries(MARKER).find(([, v]) => v === b.type)?.[0] ?? b.type.slice(2)).join(" ");

describe("layout", () => {
  it("indents the steps of a block and pairs the markers", () => {
    const rows = layout(list("a", "if", "b", "else", "c", "end", "d"));
    expect(rows.map((r) => r.depth)).toEqual([0, 0, 1, 0, 1, 0, 0]);
    expect(rows[1]).toMatchObject({ kind: "if", elseIndex: 3, endIndex: 5 });
  });

  it("nests", () => {
    expect(layout(list("if", "if", "a", "end", "end")).map((r) => r.depth)).toEqual([0, 1, 2, 1, 0]);
  });

  it("marks an Otherwise or End with no If as stray", () => {
    const rows = layout(list("else", "a", "end"));
    expect(rows.map((r) => r.stray)).toEqual([true, undefined, true]);
  });
});

describe("isWellFormed", () => {
  it("accepts balanced blocks, with at most one Otherwise each", () => {
    expect(isWellFormed(list("if", "a", "else", "b", "end"))).toBe(true);
    expect(isWellFormed(list("if", "else", "else", "end"))).toBe(false);
  });

  it("rejects an open or stray marker and too deep a nesting", () => {
    expect(isWellFormed(list("if", "a"))).toBe(false);
    expect(isWellFormed(list("end"))).toBe(false);
    expect(isWellFormed(list("else"))).toBe(false);
    const deep = (n: number) => [...Array(n).fill("if"), ...Array(n).fill("end")];
    expect(isWellFormed(list(...deep(MAX_DEPTH)))).toBe(true);
    expect(isWellFormed(list(...deep(MAX_DEPTH + 1)))).toBe(false);
  });
});

describe("addIf and addElse", () => {
  it("adds an If with its End", () => {
    const next = addIf(list("a"));
    expect(names(next)).toBe("a if end");
    expect(next[1]!.settings).toEqual({ when: "condition" });
  });

  it("adds an Otherwise before the End, once", () => {
    const withElse = addElse(list("if", "a", "end"), 0);
    expect(names(withElse)).toBe("if a else end");
    expect(names(addElse(withElse, 0))).toBe("if a else end");
  });

  it("does nothing for a row that is not an If", () => {
    expect(names(addElse(list("a", "if", "end"), 0))).toBe("a if end");
  });
});

describe("remove", () => {
  it("takes an If with its Otherwise and End and keeps the steps inside", () => {
    expect(names(remove(list("a", "if", "b", "else", "c", "end", "d"), 1))).toBe("a b c d");
  });

  it("removes a plain step or an Otherwise alone", () => {
    expect(names(remove(list("if", "a", "else", "b", "end"), 1))).toBe("if else b end");
    expect(names(remove(list("if", "a", "else", "b", "end"), 2))).toBe("if a b end");
  });

  it("never removes an End alone", () => {
    expect(names(remove(list("if", "a", "end"), 2))).toBe("if a end");
  });
});

describe("move", () => {
  it("swaps two plain steps", () => {
    expect(names(move(list("a", "b"), 1, -1))).toBe("b a");
  });

  it("moves a whole block past a step and past another block", () => {
    expect(names(move(list("if", "a", "end", "b"), 0, 1))).toBe("b if a end");
    expect(names(move(list("if", "a", "end", "if", "b", "end"), 3, -1))).toBe("if b end if a end");
    expect(names(move(list("if", "a", "end", "if", "b", "end"), 0, 1))).toBe("if b end if a end");
  });

  it("never moves an Otherwise or an End on its own", () => {
    const l = list("if", "a", "else", "b", "end");
    expect(move(l, 2, -1)).toBe(l);
    expect(move(l, 4, -1)).toBe(l);
  });

  it("lets a step cross a marker of its own block", () => {
    expect(names(move(list("if", "a", "end"), 1, -1))).toBe("a if end");
    expect(names(move(list("if", "a", "end"), 1, 1))).toBe("if end a");
  });

  it("moves an If whose End is missing nowhere", () => {
    const l = list("a", "if", "b");
    expect(move(l, 1, -1)).toBe(l);
  });

  it("takes a block out of its parent when it moves past the parent's marker", () => {
    expect(names(move(list("if", "if", "a", "end", "end"), 1, -1))).toBe("if a end if end");
  });

  it("does nothing at the ends of the list", () => {
    const l = list("a", "b");
    expect(move(l, 0, -1)).toBe(l);
    expect(move(l, 1, 1)).toBe(l);
  });
});
