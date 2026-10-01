import { describe, expect, it } from "vitest";
import type { ConditionNode } from "@macro/renderer";
import { summarizeCondition } from "../src/panels/dynamic/conditionSummary";
import type { DictKey } from "../src/i18n/tr";
import { en } from "../src/i18n/en";

const t = (key: DictKey) => en[key] as string;
const compare = (variable: string, operator: ConditionNode["operator"], value?: string, value2?: string): ConditionNode => ({ kind: "compare", variable, operator, value, value2 });

describe("summarizeCondition", () => {
  it("says nothing without a condition", () => {
    expect(summarizeCondition(undefined, t)).toBe("");
  });

  it("names the variable, the comparison and the value", () => {
    expect(summarizeCondition(compare("obs.fps", "<", "30"), t)).toBe("obs.fps less than 30");
  });

  it("leaves out the value of an availability check", () => {
    expect(summarizeCondition(compare("obs.fps", "unavailable"), t)).toBe("obs.fps is unavailable");
  });

  it("shows both ends of a range", () => {
    expect(summarizeCondition(compare("cpu.load", "between", "10", "20"), t)).toBe("cpu.load between 10 – 20");
  });

  it("joins several comparisons and marks a negated one", () => {
    const node: ConditionNode = { kind: "or", children: [compare("a", "==", "1"), { kind: "not", children: [compare("b", "==", "2")] }] };
    expect(summarizeCondition(node, t)).toBe("a equal 1 OR is not b equal 2");
  });

  it("shows an ellipsis for a shape the editor cannot read", () => {
    const node: ConditionNode = { kind: "and", children: [{ kind: "or", children: [compare("a", "==", "1")] }] };
    expect(summarizeCondition(node, t)).toBe("…");
  });
});
