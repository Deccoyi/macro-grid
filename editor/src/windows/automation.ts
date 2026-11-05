import type { ActionBinding } from "@macro/renderer";
import type { AutomationRuleDef, AutomationTriggerKind, PairedDeviceInfo } from "../api/types";
import type { DictKey } from "../i18n/tr";
import { newCondition, toConditionNode } from "../panels/dynamic/conditionEditing";
import { summarizeCondition } from "../panels/dynamic/conditionSummary";

type Translate = (key: DictKey, ...args: string[]) => string;

export type RuleProblem = "name" | "time" | "condition" | "variable" | "threshold" | "text";

/** Steps that need a device to act on (they change a page or a profile); only a rule started by a device has one. */
const DEVICE_STEPS = new Set(["core.page", "core.profile"]);
/** Steps that press keys by themselves; the server refuses them inside a rule. */
const KEY_STEPS = new Set(["core.hotkey", "core.typeText"]);

/** The longest minimum time between two starts of a change rule; the server holds the same number. */
export const AUTOMATION_MAX_MIN_INTERVAL = 3600;

const TIME_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

/** A fresh rule of the given kind, with an id no other rule has. Time rules start at 08:00 on every day. */
export function newRule(kind: AutomationTriggerKind, existing: readonly AutomationRuleDef[], name: string): AutomationRuleDef {
  const taken = new Set(existing.map((r) => r.id));
  let id = "";
  do id = `rule-${Math.random().toString(16).slice(2, 10)}`; while (taken.has(id));
  return {
    id,
    name,
    enabled: true,
    trigger: {
      kind,
      condition: kind === "variable" ? toConditionNode({ combinator: "and", conditions: [newCondition()], result: "" }) : null,
      time: kind === "time" ? "08:00" : null,
      days: [],
      deviceId: null,
      ...(kind === "variableChange" ? { variable: "", mode: "any" as const, from: null, to: null, threshold: null, minIntervalSeconds: 0 } : {}),
    },
    actions: [],
    cooldownSeconds: 0,
  };
}

/** Replaces one rule (matched by id), keeping the order. */
export function withChange(list: readonly AutomationRuleDef[], id: string, change: Partial<AutomationRuleDef>): AutomationRuleDef[] {
  return list.map((r) => (r.id === id ? { ...r, ...change } : r));
}

/** What keeps a rule from being saved yet, or null. The server checks the same things; this lets an unfinished rule wait on the page. */
export function ruleProblem(rule: AutomationRuleDef): RuleProblem | null {
  if (!rule.name.trim()) return "name";
  const trigger = rule.trigger;
  if (trigger.kind === "time" && !TIME_PATTERN.test(trigger.time ?? "")) return "time";
  if (trigger.kind === "variable" && !conditionComplete(trigger.condition)) return "condition";
  if (trigger.kind === "variableChange") {
    if (!trigger.variable) return "variable";
    if (trigger.mode === "crosses" && (trigger.threshold === null || trigger.threshold === undefined || !Number.isFinite(trigger.threshold))) return "threshold";
    // An empty string is a text choice that is not typed yet; null means "any".
    if (trigger.mode === "contains" ? !trigger.to : trigger.to === "") return "text";
  }
  return null;
}

function conditionComplete(node: AutomationRuleDef["trigger"]["condition"]): boolean {
  if (!node) return false;
  if (node.kind === "compare" || node.kind === undefined) return !!node.variable && !!node.operator;
  return (node.children?.length ?? 0) > 0 && node.children!.every(conditionComplete);
}

/** One line that says when the rule starts. */
export function triggerSummary(rule: AutomationRuleDef, devices: readonly PairedDeviceInfo[], t: Translate): string {
  const trigger = rule.trigger;
  if (trigger.kind === "variable") return t("automation.summary.variable", summarizeCondition(trigger.condition ?? undefined, t));
  if (trigger.kind === "variableChange") {
    const name = trigger.variable || "?";
    if (trigger.mode === "crosses") return t("automation.summary.crosses", name, String(trigger.threshold ?? ""));
    if (trigger.mode === "contains") return t("automation.summary.contains", name, trigger.to ?? "");
    if (trigger.to) return t("automation.summary.becomes", name, trigger.to);
    return t("automation.summary.change", name);
  }
  if (trigger.kind === "time") {
    const days = trigger.days.length === 0 || trigger.days.length === 7
      ? t("automation.days.every")
      : [...trigger.days].sort((a, b) => a - b).map((d) => t(`automation.day.${d}` as DictKey)).join(", ");
    return t("automation.summary.time", trigger.time ?? "", days);
  }
  const device = trigger.deviceId ? devices.find((d) => d.id === trigger.deviceId)?.name ?? trigger.deviceId : t("automation.device.any");
  return t("automation.summary.device", device);
}

export type RuleNote = "needsDevice" | "keys";

/** The notes shown under a rule's steps: a step that has no device to act on, and a step that presses keys (refused by the server). */
export function stepNotes(rule: AutomationRuleDef): RuleNote[] {
  const notes: RuleNote[] = [];
  const steps: ActionBinding[] = rule.actions;
  if (rule.trigger.kind !== "deviceConnect" && steps.some((s) => DEVICE_STEPS.has(s.type))) notes.push("needsDevice");
  if (steps.some((s) => KEY_STEPS.has(s.type))) notes.push("keys");
  return notes;
}

/** Turns a day on or off, keeping the list sorted and without repeats. */
export function toggleDay(days: readonly number[], day: number): number[] {
  return days.includes(day) ? days.filter((d) => d !== day) : [...days, day].sort((a, b) => a - b);
}
