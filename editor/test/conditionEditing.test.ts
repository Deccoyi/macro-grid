import { describe, expect, it } from "vitest";
import type { CompareOperator } from "@macro/renderer";
import { fromConditionNode, isValueless, newCondition, toConditionNode, type EditCase } from "../src/panels/dynamic/conditionEditing";

const ALL: CompareOperator[] = [">", ">=", "<", "<=", "==", "!=", "between", "unavailable", "available"];

function single(operator: CompareOperator, negate = false): EditCase {
  return { combinator: "and", result: "x", conditions: [{ ...newCondition(), negate, variable: "v", operator, value: "5", value2: "9" }] };
}

describe("condition editing", () => {
  it("knows which operators carry no value", () => {
    expect(ALL.filter(isValueless)).toEqual(["unavailable", "available"]);
  });

  it.each(ALL)("round trips %s, plain and negated", (operator) => {
    for (const negate of [false, true]) {
      const node = toConditionNode(single(operator, negate));
      const back = fromConditionNode(node)!;
      expect(back).toHaveLength(1);
      expect(back[0]!.operator).toBe(operator);
      expect(back[0]!.negate).toBe(negate);
      expect(back[0]!.variable).toBe("v");
    }
  });

  it("writes no value for a value-less operator and a second value only for between", () => {
    expect(toConditionNode(single("unavailable"))).toEqual({ kind: "compare", variable: "v", operator: "unavailable", value: undefined, value2: undefined });
    expect(toConditionNode(single("between")).value2).toBe("9");
    expect(toConditionNode(single(">")).value2).toBeUndefined();
  });

  it("combines several conditions with the chosen combinator", () => {
    const edit = single("==");
    edit.combinator = "or";
    edit.conditions.push({ ...newCondition(), variable: "w", operator: "unavailable" });
    const node = toConditionNode(edit);
    expect(node.kind).toBe("or");
    expect(fromConditionNode(node)!.map((c) => c.operator)).toEqual(["==", "unavailable"]);
  });
});
