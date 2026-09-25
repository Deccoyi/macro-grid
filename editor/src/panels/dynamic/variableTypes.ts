import type { VariableInfo, VariableType } from "../../api/types";
import type { DictKey } from "../../i18n/tr";
import type { EditCondition } from "./conditionEditing";

export const VARIABLE_TYPE_KEYS: Record<VariableType, DictKey> = {
  text: "variable.type.text",
  number: "variable.type.number",
  boolean: "variable.type.boolean",
  duration: "variable.type.duration",
  dateTime: "variable.type.dateTime",
};

/** How a condition row takes its value: a true/false choice, a list of fixed values, or free input. */
export type ValueInput = { kind: "boolean" } | { kind: "choice"; values: string[] } | { kind: "free"; unit?: string };

export function valueInputFor(info: VariableInfo | undefined): ValueInput {
  if (info?.type === "boolean") return { kind: "boolean" };
  if (info?.values && info.values.length > 0) return { kind: "choice", values: info.values };
  return { kind: "free", unit: info?.type === "number" ? (info.unit ?? undefined) : undefined };
}

/** Only == and != mean anything for a boolean or a fixed-choice value. */
export function allowsOrdering(input: ValueInput): boolean {
  return input.kind === "free";
}

/** "true"/"1" -> "true", "false"/"0" -> "false" (the server accepts both forms); anything else is kept as typed. */
export function normalizeBoolText(value: string): string {
  const v = value.trim().toLowerCase();
  if (v === "true" || v === "1") return "true";
  if (v === "false" || v === "0") return "false";
  return value;
}

/** After picking another variable, drop an operator or value the new variable's type cannot use. */
export function fitConditionToVariable(condition: EditCondition, info: VariableInfo | undefined): void {
  const input = valueInputFor(info);
  if (!allowsOrdering(input) && condition.operator !== "==" && condition.operator !== "!=") condition.operator = "==";
  if (input.kind === "boolean") {
    const normalized = normalizeBoolText(condition.value);
    condition.value = normalized === "true" || normalized === "false" ? normalized : "true";
  } else if (input.kind === "choice") {
    if (!input.values.includes(condition.value)) condition.value = input.values[0]!;
  } else if (info?.type === "number" && condition.value !== "" && Number.isNaN(Number(condition.value))) {
    condition.value = "";
  }
}
