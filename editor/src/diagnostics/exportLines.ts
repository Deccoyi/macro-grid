import type { Diagnostic } from "./types";

/** One Error List row written out as text for the export (the server redacts and adds its own lines). */
export interface ExportLine {
  severity: string;
  code: string;
  message: string;
  source: string;
  count: number;
  page?: string;
  widget?: string;
}

/** The editor's own lines for the export. The server's lines are left out here: the server adds them itself, with their times. */
export function toExportLines(diagnostics: Diagnostic[], text: (d: Diagnostic) => string, editorLabel: string): ExportLine[] {
  return diagnostics
    .filter((d) => d.origin !== "server")
    .map((d) => ({
      severity: d.severity,
      code: d.code,
      message: text(d),
      source: d.sourceName ?? editorLabel,
      count: d.count ?? 1,
      page: d.target?.pageId,
      widget: d.target?.widgetId,
    }));
}
