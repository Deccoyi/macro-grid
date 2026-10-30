import type { Widget } from "@macro/renderer";
import { conditionVariables, templateVariables } from "../diagnostics/profileCheck";

/** The looks the editor can preview for a button that uses its own state (the "This button" variables). */
export type SelfPreviewState = "normal" | "pressed" | "busy" | "on" | "failed" | "success";

export const SELF_PREVIEW_STATES: SelfPreviewState[] = ["normal", "pressed", "busy", "on", "failed", "success"];

export const SELF_PREFIX = "self.";

/** Sample values for the five "This button" variables in a preview state. The canvas merges them into the variables a widget is drawn with. */
export function selfSample(state: SelfPreviewState): Record<string, unknown> {
  return {
    "self.toggled": state === "on",
    "self.busy": state === "busy",
    "self.pressed": state === "pressed",
    "self.lastResult": state === "failed" ? "Failed" : state === "success" ? "Success" : "",
    "self.lastError": state === "failed" ? "ProviderError" : "",
  };
}

/** True when a widget's own text or rules use a "This button" variable, so a preview state is worth offering. */
export function usesSelfVariables(widget: Widget): boolean {
  const names = [...templateVariables(widget.text)];
  for (const binding of Object.values(widget.dynamic ?? {})) {
    for (const c of binding.cases) conditionVariables(c.condition, names);
  }
  return names.some((n) => n.toLowerCase().startsWith(SELF_PREFIX));
}
