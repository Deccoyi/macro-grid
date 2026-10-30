import { describe, expect, it } from "vitest";
import type { AutomationRuleDef } from "../src/api/types";
import { newRule, ruleProblem, stepNotes, toggleDay, triggerSummary, withChange } from "../src/windows/automation";

const t = (key: string, ...args: string[]) => (args.length === 0 ? key : `${key}|${args.join(",")}`);

describe("automation helpers", () => {
  it("makes rules with ids that are not taken", () => {
    const first = newRule("time", [], "A");
    const second = newRule("time", [first], "B");
    expect(second.id).not.toBe(first.id);
    expect(first.trigger.time).toBe("08:00");
  });

  it("holds an unfinished rule back from saving", () => {
    expect(ruleProblem(newRule("variable", [], "A"))).toBe("condition");
    expect(ruleProblem(newRule("time", [], " "))).toBe("name");
    const rule = newRule("time", [], "A");
    expect(ruleProblem(rule)).toBeNull();
    expect(ruleProblem(withChange([rule], rule.id, { trigger: { ...rule.trigger, time: "7:30" } })[0]!)).toBe("time");
  });

  it("accepts a variable rule once a variable is chosen", () => {
    const rule = newRule("variable", [], "A");
    rule.trigger.condition = { kind: "compare", variable: "user.x", operator: ">", value: "1" };
    expect(ruleProblem(rule)).toBeNull();
  });

  it("says when a rule starts", () => {
    const rule = newRule("time", [], "A");
    expect(triggerSummary(rule, [], t)).toBe("automation.summary.time|08:00,automation.days.every");
    rule.trigger.days = [3, 1];
    expect(triggerSummary(rule, [], t)).toContain("automation.day.1, automation.day.3");
    const device = newRule("deviceConnect", [], "D");
    expect(triggerSummary(device, [], t)).toBe("automation.summary.device|automation.device.any");
  });

  it("notes steps with no device and steps that press keys", () => {
    const rule: AutomationRuleDef = { ...newRule("time", [], "A"), actions: [{ type: "core.page", settings: {} }, { type: "core.hotkey", settings: {} }] };
    expect(stepNotes(rule)).toEqual(["needsDevice", "keys"]);
    expect(stepNotes({ ...rule, trigger: { ...rule.trigger, kind: "deviceConnect" } })).toEqual(["keys"]);
  });

  it("toggles days in order", () => {
    expect(toggleDay([1, 3], 2)).toEqual([1, 2, 3]);
    expect(toggleDay([1, 2, 3], 2)).toEqual([1, 3]);
  });
});
