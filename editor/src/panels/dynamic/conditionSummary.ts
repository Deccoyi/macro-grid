import type { ConditionNode } from "@macro/renderer";
import type { DictKey } from "../../i18n/tr";
import { fromConditionNode, isValueless, type EditCondition } from "./conditionEditing";

type Translate = (key: DictKey) => string;

/** One line that says what a condition tests, e.g. "obs.fps less than 30 AND not audio.muted equal true". A condition the editor cannot read is shown as "…". */
export function summarizeCondition(node: ConditionNode | undefined, t: Translate): string {
  if (!node) return "";
  const conditions = fromConditionNode(node);
  if (!conditions) return "…";
  const joiner = node.kind === "or" || node.kind === "xor" ? t(`dynamic.combinator.${node.kind}`) : t("dynamic.combinator.and");
  return conditions.map((c) => summarizeOne(c, t)).filter(Boolean).join(` ${joiner} `);
}

function summarizeOne(c: EditCondition, t: Translate): string {
  if (!c.variable) return "";
  const parts = [c.negate ? t("dynamic.negate") : "", c.variable, t(`dynamic.operator.${c.operator}`)];
  if (!isValueless(c.operator)) parts.push(c.operator === "between" ? `${c.value} – ${c.value2}` : c.value);
  return parts.filter(Boolean).join(" ");
}
