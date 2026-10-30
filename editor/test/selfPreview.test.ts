import { describe, expect, it } from "vitest";
import type { Widget } from "@macro/renderer";
import { evaluateWidgetDynamicStyle } from "../src/grid/evaluateDynamic";
import { SELF_PREVIEW_STATES, selfSample, usesSelfVariables } from "../src/grid/selfPreview";

const widget = (over: Partial<Widget>): Widget => ({ id: "w1", type: "button", x: 0, y: 0, w: 1, h: 1, actions: {}, ...over });

describe("selfSample", () => {
  it("the resting look has every flag off and an empty result", () => {
    expect(selfSample("normal")).toEqual({ "self.toggled": false, "self.busy": false, "self.pressed": false, "self.lastResult": "", "self.lastError": "" });
  });

  it("each state sets only its own values", () => {
    expect(selfSample("pressed")["self.pressed"]).toBe(true);
    expect(selfSample("busy")["self.busy"]).toBe(true);
    expect(selfSample("on")["self.toggled"]).toBe(true);
    expect(selfSample("failed")).toMatchObject({ "self.lastResult": "Failed", "self.lastError": "ProviderError" });
    expect(selfSample("success")).toMatchObject({ "self.lastResult": "Success", "self.lastError": "" });
    expect(SELF_PREVIEW_STATES).toHaveLength(6);
  });
});

describe("usesSelfVariables", () => {
  it("is true for a self name in the text or in a rule, false otherwise", () => {
    expect(usesSelfVariables(widget({ text: "{self.lastError}" }))).toBe(true);
    expect(usesSelfVariables(widget({ dynamic: { "style.background": { cases: [{ condition: { kind: "compare", variable: "self.busy", operator: "==", value: "true" }, result: "red" }] } } }))).toBe(true);
    expect(usesSelfVariables(widget({ text: "{system.cpu}" }))).toBe(false);
  });
});

describe("evaluating a rule on a This button variable", () => {
  const w = widget({ dynamic: { "style.background": { cases: [
    { condition: { kind: "compare", variable: "self.lastResult", operator: "==", value: "Failed" }, result: "red" },
    { condition: { kind: "compare", variable: "self.busy", operator: "==", value: "true" }, result: "orange" },
  ] } } });

  it("picks the look of each preview state", () => {
    expect(evaluateWidgetDynamicStyle(w, selfSample("normal"))).toBeUndefined();
    expect(evaluateWidgetDynamicStyle(w, selfSample("busy"))).toEqual({ background: "orange" });
    expect(evaluateWidgetDynamicStyle(w, selfSample("failed"))).toEqual({ background: "red" });
  });
});
