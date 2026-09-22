import type { ConditionNode, DynamicBinding, Widget } from "@macro/renderer";

/**
 * Editor-only mirror of the server's DynamicRuleEvaluator, so the canvas can preview a dynamized
 * property against the one-shot variable snapshot without a round trip. The server's evaluation is
 * always the one that actually runs for real devices — this only needs to be "close enough" for WYSIWYG.
 */
type DynamizableStyleKey = "background" | "foreground" | "borderColor" | "animation";

export function evaluateWidgetDynamicStyle(widget: Widget, variables: Record<string, unknown>): Partial<Record<DynamizableStyleKey, string>> | undefined {
  if (!widget.dynamic) return undefined;
  let result: Partial<Record<DynamizableStyleKey, string>> | undefined;

  for (const [propertyPath, binding] of Object.entries(widget.dynamic)) {
    const styleKey = PROPERTY_TO_STYLE_KEY[propertyPath];
    if (!styleKey) continue;
    const value = evaluateBinding(binding, variables);
    if (value === undefined) continue;
    result ??= {};
    result[styleKey] = value;
  }
  return result;
}

const PROPERTY_TO_STYLE_KEY: Record<string, DynamizableStyleKey> = {
  "style.background": "background",
  "style.foreground": "foreground",
  "style.borderColor": "borderColor",
  "style.animation": "animation",
};

function evaluateBinding(binding: DynamicBinding, variables: Record<string, unknown>): string | undefined {
  for (const c of binding.cases) {
    if (evaluateNode(c.condition, variables)) return c.result;
  }
  return binding.default;
}

function evaluateNode(node: ConditionNode, variables: Record<string, unknown>): boolean {
  switch (node.kind) {
    case "and": return (node.children?.length ?? 0) > 0 && node.children!.every((c) => evaluateNode(c, variables));
    case "or": return (node.children ?? []).some((c) => evaluateNode(c, variables));
    case "xor": return (node.children ?? []).filter((c) => evaluateNode(c, variables)).length % 2 === 1;
    case "not": return node.children?.length === 1 && !evaluateNode(node.children[0]!, variables);
    default: return evaluateComparison(node, variables);
  }
}

function evaluateComparison(node: ConditionNode, variables: Record<string, unknown>): boolean {
  if (!node.variable || !node.operator || node.value === undefined) return false;
  const live = variables[node.variable];

  if (node.operator === "between") {
    const v = toNumber(live);
    const lo = Number(node.value);
    const hi = Number(node.value2);
    if (v === undefined || Number.isNaN(lo) || Number.isNaN(hi)) return false;
    return v >= Math.min(lo, hi) && v <= Math.max(lo, hi);
  }

  const actual = toNumber(live);
  const expected = Number(node.value);
  if (actual !== undefined && !Number.isNaN(expected)) {
    switch (node.operator) {
      case ">": return actual > expected;
      case ">=": return actual >= expected;
      case "<": return actual < expected;
      case "<=": return actual <= expected;
      case "==": return actual === expected;
      case "!=": return actual !== expected;
    }
  }

  const actualText = live === undefined || live === null ? "" : String(live);
  if (node.operator === "==") return actualText.toLowerCase() === node.value.toLowerCase();
  if (node.operator === "!=") return actualText.toLowerCase() !== node.value.toLowerCase();
  return false;
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === "number") return value;
  if (typeof value === "string" && value.trim() !== "" && !Number.isNaN(Number(value))) return Number(value);
  return undefined;
}
