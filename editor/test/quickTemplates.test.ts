import { describe, expect, it } from "vitest";
import type { VariableInfo } from "../src/api/types";
import { evaluateNode } from "../src/grid/evaluateDynamic";
import { newCase, toConditionNode } from "../src/panels/dynamic/conditionEditing";
import { applyTemplate, buildTemplate, templateKinds, variablesFor, type TemplateWords } from "../src/panels/dynamic/quickTemplates";

const words: TemplateWords = { on: "On", off: "Off", low: "Low", medium: "Medium", high: "High", unavailable: "N/A" };
const info = (name: string, type: VariableInfo["type"]): VariableInfo => ({ name, description: "", example: `{${name}}`, category: "c", type });

/** What the preview would pick for a variable snapshot: the first rule that matches. */
function firstResult(rules: ReturnType<typeof buildTemplate>, variables: Record<string, unknown>): string | undefined {
  return rules.find((r) => evaluateNode(toConditionNode(r), variables))?.result;
}

describe("quick templates", () => {
  it("offers grey-when-unavailable only for a color or a text result", () => {
    expect(templateKinds("color")).toContain("unavailable");
    expect(templateKinds("text")).toContain("unavailable");
    expect(templateKinds("icon")).not.toContain("unavailable");
    expect(templateKinds({ select: [{ value: "a", label: "A" }] })).not.toContain("unavailable");
  });

  it("filters the variables by the type a template needs", () => {
    const catalog = [info("b", "boolean"), info("n", "number"), info("t", "text")];
    expect(variablesFor("onOff", catalog).map((v) => v.name)).toEqual(["b"]);
    expect(variablesFor("thresholds", catalog).map((v) => v.name)).toEqual(["n"]);
    expect(variablesFor("unavailable", catalog)).toHaveLength(3);
  });

  it("on / off picks a color for true and for false", () => {
    const rules = buildTemplate("onOff", info("b", "boolean"), "color", words);
    expect(firstResult(rules, { b: true })).toBe("#15803d");
    expect(firstResult(rules, { b: false })).toBe("#374151");
  });

  it("low / medium / high checks the high threshold first", () => {
    const rules = buildTemplate("thresholds", info("n", "number"), "color", words);
    expect(firstResult(rules, { n: 95 })).toBe("#b91c1c");
    expect(firstResult(rules, { n: 80 })).toBe("#b91c1c");
    expect(firstResult(rules, { n: 60 })).toBe("#b45309");
    expect(firstResult(rules, { n: 10 })).toBe("#15803d");
  });

  it("uses the words for a text result, nothing for an icon and the first option for a choice (the second for off)", () => {
    expect(buildTemplate("onOff", info("b", "boolean"), "text", words).map((r) => r.result)).toEqual(["On", "Off"]);
    expect(buildTemplate("onOff", info("b", "boolean"), "icon", words).map((r) => r.result)).toEqual(["", ""]);
    expect(buildTemplate("onOff", info("b", "boolean"), { select: [{ value: "pulse", label: "Pulse" }] }, words).map((r) => r.result)).toEqual(["pulse", "pulse"]);
    expect(buildTemplate("onOff", info("b", "boolean"), { select: [{ value: "pulse", label: "Pulse" }, { value: "none", label: "None" }] }, words).map((r) => r.result)).toEqual(["pulse", "none"]);
  });

  it("grey when unavailable matches only a variable without a value", () => {
    const rules = buildTemplate("unavailable", info("x", "text"), "color", words);
    expect(firstResult(rules, {})).toBe("#374151");
    expect(firstResult(rules, { x: null })).toBe("#374151");
    expect(firstResult(rules, { x: "" })).toBeUndefined();
  });

  it("replaces the untouched first rule, puts unavailable on top and appends the others", () => {
    const onOff = buildTemplate("onOff", info("b", "boolean"), "color", words);
    const grey = buildTemplate("unavailable", info("b", "boolean"), "color", words);

    expect(applyTemplate([newCase()], onOff, "onOff")).toEqual(onOff);

    const base = newCase();
    const existing = [{ ...base, result: "#111111", conditions: [{ ...base.conditions[0]!, variable: "v", value: "1" }] }];
    expect(applyTemplate(existing, onOff, "onOff")).toEqual([...existing, ...onOff]);
    expect(applyTemplate(existing, grey, "unavailable")).toEqual([...grey, ...existing]);
  });
});
