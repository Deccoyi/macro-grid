import { describe, expect, it } from "vitest";
import type { ActionBinding } from "@macro/renderer";
import { MAX_DEPTH, addElse, carryCollapsed, addIf, insertAt, isWellFormed, laneStacks, layout, move, moveTo, remove } from "../src/panels/actionFlow";

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

  it("hops a step over a whole block instead of landing inside it", () => {
    expect(names(move(list("a", "if", "b", "end"), 0, 1))).toBe("if b end a");
    expect(names(move(list("if", "b", "end", "a"), 3, -1))).toBe("a if b end");
    expect(names(move(list("if", "b", "else", "c", "end", "a"), 5, -1))).toBe("a if b else c end");
    expect(names(move(list("a", "if", "b", "else", "c", "end"), 0, 1))).toBe("if b else c end a");
  });

  it("hops over a nested block as one unit and stays in its own lane", () => {
    expect(names(move(list("if", "a", "if", "b", "end", "c", "end"), 1, 1))).toBe("if if b end a c end");
    expect(names(move(list("if", "if", "b", "end", "a", "end"), 4, -1))).toBe("if a if b end end");
    expect(names(move(list("if", "x", "if", "if", "b", "end", "end", "end"), 1, 1))).toBe("if if if b end end x end");
  });

  it("does nothing at the ends of the list", () => {
    const l = list("a", "b");
    expect(move(l, 0, -1)).toBe(l);
    expect(move(l, 1, 1)).toBe(l);
  });
});

describe("laneStacks", () => {
  it("tells which lane every insertion point is in", () => {
    const stacks = laneStacks(list("a", "if", "b", "else", "c", "end", "d"));
    expect(stacks.map((s) => s.join("/"))).toEqual(["", "", "if", "if", "else", "else", "", ""]);
  });
});

describe("insertAt", () => {
  it("inserts inside an empty If, inside an Otherwise, and after a block", () => {
    expect(names(insertAt(list("if", "end"), 1, "t.x"))).toBe("if x end");
    expect(names(insertAt(list("if", "a", "else", "end"), 3, "t.x"))).toBe("if a else x end");
    expect(names(insertAt(list("if", "end"), 2, "t.x"))).toBe("if end x");
  });

  it("brings an End with an If, and refuses one that nests too deep", () => {
    expect(names(insertAt(list("a"), 0, "core.if"))).toBe("if end a");
    const deep = list("if", "if", "if", "end", "end", "end");
    expect(insertAt(deep, 3, "core.if")).toBe(deep);
  });
});

describe("moveTo", () => {
  it("moves a step into a block, out of it and past another block", () => {
    expect(names(moveTo(list("a", "if", "end"), 0, 2))).toBe("if a end");
    expect(names(moveTo(list("if", "a", "else", "b", "end"), 1, 3))).toBe("if else a b end");
    expect(names(moveTo(list("if", "a", "end", "b"), 1, 4))).toBe("if end b a");
    expect(names(moveTo(list("a", "if", "end"), 0, 3))).toBe("if end a");
  });

  it("moves a whole block", () => {
    expect(names(moveTo(list("if", "a", "end", "b"), 0, 4))).toBe("b if a end");
    expect(names(moveTo(list("a", "if", "b", "end"), 1, 0))).toBe("if b end a");
  });

  it("refuses a drop into itself, onto its own place, or onto an Otherwise or End", () => {
    const l = list("if", "a", "end", "b");
    expect(moveTo(l, 0, 2)).toBe(l);
    expect(moveTo(l, 1, 1)).toBe(l);
    expect(moveTo(l, 1, 2)).toBe(l);
    expect(moveTo(l, 2, 0)).toBe(l);
  });

  it("refuses a block that would nest too deep", () => {
    const l = list("if", "if", "if", "end", "end", "end", "if", "end");
    expect(moveTo(l, 6, 3)).toBe(l);
  });
});

describe("carryCollapsed", () => {
  it("keeps a collapsed If collapsed when it is moved, and when steps are added or removed before it", () => {
    const l = list("a", "if", "b", "end", "c");
    expect([...carryCollapsed(l, move(l, 1, -1), new Set([1]))]).toEqual([0]);
    expect([...carryCollapsed(l, move(l, 1, 1), new Set([1]))]).toEqual([2]);
    expect([...carryCollapsed(l, moveTo(l, 1, 5), new Set([1]))]).toEqual([2]);
    expect([...carryCollapsed(l, insertAt(l, 0, "x"), new Set([1]))]).toEqual([2]);
  });

  it("drops a collapsed If that was removed", () => {
    const l = list("a", "if", "b", "end");
    expect(carryCollapsed(l, remove(l, 1), new Set([1])).size).toBe(0);
  });
});
