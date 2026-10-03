import type { Diagnostic } from "./types";

/** What marks a widget in the editor: the worst severity among its lines, and the text of every line (errors first). */
export interface WidgetProblems {
  level: "error" | "warning";
  texts: string[];
}

/** The widgets of one profile that error or warning lines are about. Each widget has its own list, in Error List order, errors before warnings;
 * information does not mark a widget. An error makes the marker red, warnings alone make it yellow. */
export function problemWidgets(diagnostics: Diagnostic[], profileId: string, textOf: (d: Diagnostic) => string): Map<string, WidgetProblems> {
  const errors = new Map<string, string[]>();
  const warnings = new Map<string, string[]>();
  for (const d of diagnostics) {
    const target = d.target;
    if ((d.severity !== "error" && d.severity !== "warning") || !target?.widgetId || target.profileId !== profileId) continue;
    const into = d.severity === "error" ? errors : warnings;
    const list = into.get(target.widgetId);
    if (list) list.push(textOf(d)); else into.set(target.widgetId, [textOf(d)]);
  }
  const marked = new Map<string, WidgetProblems>();
  for (const [id, texts] of errors) marked.set(id, { level: "error", texts: [...texts, ...(warnings.get(id) ?? [])] });
  for (const [id, texts] of warnings) if (!marked.has(id)) marked.set(id, { level: "warning", texts });
  return marked;
}
