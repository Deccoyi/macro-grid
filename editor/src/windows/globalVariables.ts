import type { UserVariableDef, UserVariableType } from "../api/types";

export const USER_PREFIX = "user.";

export type NameProblem = "empty" | "format" | "tooLong" | "duplicate";

/** Why a new variable name cannot be used, or null. Mirrors the server rule: a letter, then letters, digits or "_"; unique ignoring case. */
export function nameProblem(name: string, existing: readonly string[], maxLength: number): NameProblem | null {
  if (!name) return "empty";
  if (name.length > maxLength) return "tooLong";
  if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(name)) return "format";
  const lower = name.toLowerCase();
  if (existing.some((n) => n.toLowerCase() === lower)) return "duplicate";
  return null;
}

/** The start value the person typed, as the server wants it. `undefined` means the text does not fit the type; `null` means no start value. */
export function parseStartValue(type: UserVariableType, text: string): string | number | boolean | null | undefined {
  if (type === "text") return text;
  const trimmed = text.trim();
  if (trimmed === "") return null;
  if (type === "number") {
    const n = Number(trimmed);
    return /^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(trimmed) && Number.isFinite(n) ? n : undefined;
  }
  if (trimmed === "true") return true;
  if (trimmed === "false") return false;
  return undefined;
}

/** The start value as the text shown in its input. */
export function startValueText(v: UserVariableDef): string {
  return v.initial === undefined || v.initial === null ? "" : String(v.initial);
}

export function newVariable(name: string, type: UserVariableType): UserVariableDef {
  return { name, type, initial: type === "text" ? "" : null, keep: false, description: "" };
}

/** Replaces one variable in the list (matched by name), keeping the order. */
export function withChange(list: readonly UserVariableDef[], name: string, change: Partial<UserVariableDef>): UserVariableDef[] {
  return list.map((v) => (v.name === name ? { ...v, ...change } : v));
}

/** How the widgets that use a variable are listed in the delete question: a few names, then "and N more". */
export function usageSummary(uses: readonly { pageName: string; widgetName: string }[], max = 6): { lines: string[]; more: number } {
  return { lines: uses.slice(0, max).map((u) => `${u.pageName} / ${u.widgetName}`), more: Math.max(0, uses.length - max) };
}
