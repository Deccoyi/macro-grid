/** The columns of the Diagnostic Messages table, in order. The width of each is what the person dragged it to, kept per person in this window's storage. */
export const DIAGNOSTIC_COLUMNS = ["severity", "code", "description", "source", "screen", "location"] as const;
export type DiagnosticColumn = (typeof DIAGNOSTIC_COLUMNS)[number];

export const DEFAULT_COLUMN_WIDTHS: Record<DiagnosticColumn, number> = { severity: 90, code: 70, description: 420, source: 150, screen: 130, location: 100 };
export const MIN_COLUMN_WIDTH = 40;
const KEY = "macro-grid.editor.diagnosticColumns";

export function clampWidth(width: number): number {
  return Math.max(MIN_COLUMN_WIDTH, Math.min(1600, Math.round(width)));
}

export function loadColumnWidths(): Record<DiagnosticColumn, number> {
  try {
    const saved = JSON.parse(localStorage.getItem(KEY) ?? "{}") as Record<string, unknown>;
    const widths = { ...DEFAULT_COLUMN_WIDTHS };
    for (const column of DIAGNOSTIC_COLUMNS) if (typeof saved[column] === "number") widths[column] = clampWidth(saved[column] as number);
    return widths;
  } catch {
    return { ...DEFAULT_COLUMN_WIDTHS };
  }
}

export function saveColumnWidths(widths: Record<DiagnosticColumn, number>): void {
  try {
    localStorage.setItem(KEY, JSON.stringify(widths));
  } catch {
    // Storage can be unavailable; the widths then last until the window closes.
  }
}

/** The grid template for a row: every column keeps exactly the width it was dragged to, and an empty last track takes the rest of a wide panel,
 * so widening a column pushes the ones after it to the right (and scrolls) instead of squeezing the description. */
export function gridTemplate(widths: Record<DiagnosticColumn, number>): string {
  return `${DIAGNOSTIC_COLUMNS.map((c) => `${widths[c]}px`).join(" ")} 1fr`;
}

/** The width the table needs at least, so a narrow panel scrolls sideways instead of squeezing the columns. */
export function tableMinWidth(widths: Record<DiagnosticColumn, number>): number {
  return DIAGNOSTIC_COLUMNS.reduce((sum, c) => sum + widths[c], 0) + 20;
}
