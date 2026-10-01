import type { VariableInfo } from "../../api/types";
import { newCondition, type EditCase } from "./conditionEditing";
import type { ResultKind } from "./DynamizeModal";

/** Ready-made rule sets that depend only on a variable's type, never on a plugin. */
export type TemplateKind = "onOff" | "thresholds" | "unavailable";

/** The short words a text result uses; the caller fills them from the i18n dictionary. */
export interface TemplateWords {
  on: string;
  off: string;
  low: string;
  medium: string;
  high: string;
  unavailable: string;
}

const COLORS = { on: "#15803d", off: "#374151", high: "#b91c1c", medium: "#b45309", low: "#15803d", grey: "#374151" } as const;

/** Which templates make sense for a result: "grey when unavailable" needs a value that can say "grey", so it is hidden for a fixed choice or an icon. */
export function templateKinds(resultKind: ResultKind): TemplateKind[] {
  return typeof resultKind === "object" || resultKind === "icon" ? ["onOff", "thresholds"] : ["onOff", "thresholds", "unavailable"];
}

/** The variables a template can be built for: a boolean for on/off, a number for thresholds, anything for unavailable. */
export function variablesFor(kind: TemplateKind, catalog: VariableInfo[]): VariableInfo[] {
  if (kind === "onOff") return catalog.filter((v) => v.type === "boolean");
  if (kind === "thresholds") return catalog.filter((v) => v.type === "number");
  return catalog;
}

function result(resultKind: ResultKind, color: string, word: string): string {
  if (typeof resultKind === "object") return resultKind.select[0]?.value ?? "";
  if (resultKind === "color") return color;
  return resultKind === "text" ? word : "";
}

function rule(variable: string, operator: EditCase["conditions"][number]["operator"], value: string, resultValue: string): EditCase {
  return { combinator: "and", conditions: [{ ...newCondition(), variable, operator, value }], result: resultValue };
}

/** The rules of a template, in the order they must be checked (the first match wins). */
export function buildTemplate(kind: TemplateKind, variable: VariableInfo, resultKind: ResultKind, words: TemplateWords): EditCase[] {
  const name = variable.name;
  switch (kind) {
    case "onOff":
      return [rule(name, "==", "true", result(resultKind, COLORS.on, words.on)), rule(name, "==", "false", result(resultKind, COLORS.off, words.off))];
    case "thresholds":
      return [
        rule(name, ">=", "80", result(resultKind, COLORS.high, words.high)),
        rule(name, ">=", "50", result(resultKind, COLORS.medium, words.medium)),
        rule(name, "<", "50", result(resultKind, COLORS.low, words.low)),
      ];
    case "unavailable":
      return [rule(name, "unavailable", "", result(resultKind, COLORS.grey, words.unavailable))];
  }
}

function isPristine(cases: EditCase[]): boolean {
  const only = cases.length === 1 ? cases[0]! : undefined;
  return !!only && only.conditions.length === 1 && !only.conditions[0]!.variable && !only.conditions[0]!.value;
}

/** Adds a template to the rule list: it replaces the untouched first rule, "unavailable" goes to the top (it must win), the others are appended. */
export function applyTemplate(cases: EditCase[], built: EditCase[], kind: TemplateKind): EditCase[] {
  if (isPristine(cases)) return built;
  return kind === "unavailable" ? [...built, ...cases] : [...cases, ...built];
}
