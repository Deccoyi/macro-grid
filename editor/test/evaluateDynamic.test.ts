import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import type { ConditionNode } from "@macro/renderer";
import { evaluateNode } from "../src/grid/evaluateDynamic";

// Run from the editor folder (npm test; import.meta.url is not a file URL under jsdom). The same table is read by the server tests (DynamicRuleEvaluatorTests), so the preview and the real evaluator cannot drift apart.
const cases = JSON.parse(readFileSync(resolve(process.cwd(), "../tests/shared/dynamic-rule-cases.json"), "utf8")) as {
  name: string;
  variables: Record<string, unknown>;
  condition: ConditionNode;
  expected: boolean;
}[];

describe("dynamic rule evaluation (shared case table)", () => {
  it.each(cases)("$name", ({ variables, condition, expected }) => {
    expect(evaluateNode(condition, variables)).toBe(expected);
  });
});
