import { describe, expect, it } from "vitest";
import { nameProblem, newVariable, parseStartValue, startValueText, usageSummary, withChange } from "../src/windows/globalVariables";

describe("global variable names", () => {
  it("accepts a letter followed by letters, digits and underscores", () => {
    expect(nameProblem("count_1", [], 40)).toBeNull();
  });

  it.each([["", "empty"], ["1abc", "format"], ["a b", "format"], ["a.b", "format"], ["_x", "format"], ["é", "format"]])("refuses %j", (name, why) => {
    expect(nameProblem(name, [], 40)).toBe(why);
  });

  it("refuses a name that is too long or taken, ignoring case", () => {
    expect(nameProblem("a".repeat(41), [], 40)).toBe("tooLong");
    expect(nameProblem("Count", ["count"], 40)).toBe("duplicate");
  });
});

describe("start values", () => {
  it("keeps text as typed, even empty", () => {
    expect(parseStartValue("text", " hi ")).toBe(" hi ");
    expect(parseStartValue("text", "")).toBe("");
  });

  it("turns an empty number or boolean into no value", () => {
    expect(parseStartValue("number", "  ")).toBeNull();
    expect(parseStartValue("boolean", "")).toBeNull();
  });

  it("parses numbers strictly", () => {
    expect(parseStartValue("number", "12.5")).toBe(12.5);
    expect(parseStartValue("number", "-3")).toBe(-3);
    expect(parseStartValue("number", "1e3")).toBe(1000);
    expect(parseStartValue("number", "abc")).toBeUndefined();
    expect(parseStartValue("number", "1,5")).toBeUndefined();
    expect(parseStartValue("number", "Infinity")).toBeUndefined();
  });

  it("parses booleans", () => {
    expect(parseStartValue("boolean", "true")).toBe(true);
    expect(parseStartValue("boolean", "false")).toBe(false);
    expect(parseStartValue("boolean", "maybe")).toBeUndefined();
  });

  it("shows a missing start value as empty text", () => {
    expect(startValueText(newVariable("a", "number"))).toBe("");
    expect(startValueText({ ...newVariable("a", "number"), initial: 0 })).toBe("0");
  });
});

describe("list helpers", () => {
  it("changes one variable and keeps the order", () => {
    const list = [newVariable("a", "number"), newVariable("b", "text")];
    const next = withChange(list, "b", { keep: true });
    expect(next.map((v) => v.name)).toEqual(["a", "b"]);
    expect(next[1]!.keep).toBe(true);
    expect(list[1]!.keep).toBe(false);
  });

  it("summarises the uses with a cut", () => {
    const uses = Array.from({ length: 9 }, (_, i) => ({ pageName: "P", widgetName: "W" + i }));
    const summary = usageSummary(uses, 6);
    expect(summary.lines).toHaveLength(6);
    expect(summary.more).toBe(3);
  });
});
