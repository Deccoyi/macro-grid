import type { Diagnostic } from "./types";

/** The widgets of one profile that an error line is about, with the line's text (the first one when a widget has several). Warnings and
 * information do not mark a widget: the red marker is for what is broken. */
export function problemWidgets(diagnostics: Diagnostic[], profileId: string, textOf: (d: Diagnostic) => string): Map<string, string> {
  const marked = new Map<string, string>();
  for (const d of diagnostics) {
    const target = d.target;
    if (d.severity !== "error" || !target?.widgetId || target.profileId !== profileId || marked.has(target.widgetId)) continue;
    marked.set(target.widgetId, textOf(d));
  }
  return marked;
}
