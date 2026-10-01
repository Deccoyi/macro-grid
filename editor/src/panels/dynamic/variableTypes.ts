import type { VariableInfo, VariableType } from "../../api/types";
import type { DictKey } from "../../i18n/tr";
import { isValueless, type EditCondition } from "./conditionEditing";

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

/** Drop an operator or value the variable's type cannot use. `picked` is true when the variable was just chosen (not when only the operator changed): only then is a typed value discarded or a fresh row's operator reset. */
export function fitConditionToVariable(condition: EditCondition, info: VariableInfo | undefined, picked = false): void {
  if (isValueless(condition.operator)) return;
  const input = valueInputFor(info);
  // A text variable picked on a fresh row (nothing typed yet) starts with "equal": ordering means nothing for text.
  if (picked && info?.type === "text" && input.kind === "free" && condition.value === "" && condition.operator === ">") condition.operator = "==";
  if (!allowsOrdering(input) && condition.operator !== "==" && condition.operator !== "!=") condition.operator = "==";
  if (input.kind === "boolean") {
    const normalized = normalizeBoolText(condition.value);
    condition.value = normalized === "true" || normalized === "false" ? normalized : "true";
  } else if (input.kind === "choice") {
    if (!input.values.includes(condition.value)) condition.value = input.values[0]!;
  } else if (picked && info?.type === "number" && condition.value !== "" && Number.isNaN(Number(condition.value))) {
    condition.value = "";
  }
}

/** True when a number variable is compared with text that is not a number (kept as typed, only marked). */
export function isInvalidNumber(info: VariableInfo | undefined, value: string): boolean {
  return info?.type === "number" && value.trim() !== "" && Number.isNaN(Number(value));
}
