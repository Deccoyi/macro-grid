import type { CompareOperator, ConditionKind, ConditionNode } from "@macro/renderer";

/** One row in the editor UI: a single comparison, optionally negated. */
export interface EditCondition {
  negate: boolean;
  variable: string;
  operator: CompareOperator;
  value: string;
  value2: string;
}

/** One "if" branch as the UI edits it: a flat list of conditions combined with one combinator (only meaningful when there's more than one). */
export interface EditCase {
  combinator: "and" | "or" | "xor";
  conditions: EditCondition[];
  result: string;
}

export function newCondition(): EditCondition {
  return { negate: false, variable: "", operator: ">", value: "", value2: "" };
}

export function newCase(defaultResult = "#c0392b"): EditCase {
  return { combinator: "and", conditions: [newCondition()], result: defaultResult };
}

export function toConditionNode(edit: EditCase): ConditionNode {
  const nodes = edit.conditions.map(conditionToNode);
  if (nodes.length === 1) return nodes[0]!;
  return { kind: edit.combinator, children: nodes };
}

function conditionToNode(c: EditCondition): ConditionNode {
  const compare: ConditionNode = {
    kind: "compare",
    variable: c.variable,
    operator: c.operator,
    value: c.value,
    value2: c.operator === "between" ? c.value2 : undefined,
  };
  return c.negate ? { kind: "not", children: [compare] } : compare;
}

/**
 * Reverses toConditionNode for editing an existing binding. Only understands the shapes the editor
 * itself produces (a bare compare, a negated compare, or a flat and/or/xor of those) — anything more
 * deeply nested (only possible via hand-written JSON or a future plugin) is reported as unsupported so
 * the caller can fall back to a read-only view instead of silently corrupting it on save.
 */
export function fromConditionNode(node: ConditionNode): EditCase["conditions"] | null {
  const asCondition = (n: ConditionNode): EditCondition | null => {
    if (n.kind === "compare") {
      return { negate: false, variable: n.variable ?? "", operator: n.operator ?? ">", value: n.value ?? "", value2: n.value2 ?? "" };
    }
    if (n.kind === "not" && n.children?.length === 1 && n.children[0]!.kind === "compare") {
      const inner = n.children[0]!;
      return { negate: true, variable: inner.variable ?? "", operator: inner.operator ?? ">", value: inner.value ?? "", value2: inner.value2 ?? "" };
    }
    return null;
  };

  const direct = asCondition(node);
  if (direct) return [direct];

  const isCombinator = (k: ConditionKind) => k === "and" || k === "or" || k === "xor";
  if (isCombinator(node.kind) && node.children) {
    const conditions = node.children.map(asCondition);
    if (conditions.every((c): c is EditCondition => c !== null)) return conditions;
  }
  return null;
}

export function combinatorOf(node: ConditionNode): EditCase["combinator"] {
  return node.kind === "or" || node.kind === "xor" ? node.kind : "and";
}
