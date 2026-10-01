import { describe, expect, it } from "vitest";
import type { VariableInfo } from "../src/api/types";
import { newCondition } from "../src/panels/dynamic/conditionEditing";
import { fitConditionToVariable, isInvalidNumber } from "../src/panels/dynamic/variableTypes";

const info = (patch: Partial<VariableInfo>): VariableInfo => ({ name: "v", description: "", example: "{v}", category: "c", ...patch });

describe("fitConditionToVariable", () => {
  it("starts a fresh row on a text variable with equal instead of greater than", () => {
    const c = newCondition();
    fitConditionToVariable(c, info({ type: "text" }));
    expect(c.operator).toBe("==");
  });

  it("does not touch the operator of a row that already has a value", () => {
    const c = { ...newCondition(), value: "abc", operator: ">" as const };
    fitConditionToVariable(c, info({ type: "text" }));
    expect(c.operator).toBe(">");
  });

  it("keeps greater than on a number variable", () => {
    const c = newCondition();
    fitConditionToVariable(c, info({ type: "number" }));
    expect(c.operator).toBe(">");
  });

  it("leaves a value-less operator and its empty value untouched", () => {
    const c = { ...newCondition(), operator: "unavailable" as const };
    fitConditionToVariable(c, info({ type: "boolean" }));
    expect(c.operator).toBe("unavailable");
    expect(c.value).toBe("");
  });

  it("sets true for a boolean when the operator moves back to a value operator", () => {
    const c = { ...newCondition(), operator: "==" as const };
    fitConditionToVariable(c, info({ type: "boolean" }));
    expect(c.value).toBe("true");
  });
});

describe("isInvalidNumber", () => {
  it("flags text that is not a number for a number variable only", () => {
    expect(isInvalidNumber(info({ type: "number" }), "abc")).toBe(true);
    expect(isInvalidNumber(info({ type: "number" }), "12.5")).toBe(false);
    expect(isInvalidNumber(info({ type: "number" }), "")).toBe(false);
    expect(isInvalidNumber(info({ type: "text" }), "abc")).toBe(false);
    expect(isInvalidNumber(undefined, "abc")).toBe(false);
  });
});
