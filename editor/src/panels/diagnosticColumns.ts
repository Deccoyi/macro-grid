/** The columns of the Diagnostic Messages table, in order. The width of each is what the person dragged it to, kept per person in this window's storage. */
export const DIAGNOSTIC_COLUMNS = ["severity", "code", "description", "source", "screen", "location", "details"] as const;
export type DiagnosticColumn = (typeof DIAGNOSTIC_COLUMNS)[number];

export const DEFAULT_COLUMN_WIDTHS: Record<DiagnosticColumn, number> = { severity: 90, code: 70, description: 420, source: 150, screen: 130, location: 100, details: 70 };
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
export function gridTemplate(widths: Record<DiagnosticColumn, number>, columns: readonly DiagnosticColumn[] = DIAGNOSTIC_COLUMNS): string {
  return `${columns.map((c) => `${widths[c]}px`).join(" ")} 1fr`;
}

/** The width the table needs at least, so a narrow panel scrolls sideways instead of squeezing the columns. */
export function tableMinWidth(widths: Record<DiagnosticColumn, number>, columns: readonly DiagnosticColumn[] = DIAGNOSTIC_COLUMNS): number {
  return columns.reduce((sum, c) => sum + widths[c], 0) + 20;
}

const HIDDEN_KEY = "macro-grid.editor.diagnosticHiddenColumns";

/** The columns the person turned off, kept per person in this window's storage. */
export function loadHiddenColumns(): DiagnosticColumn[] {
  try {
    const saved = JSON.parse(localStorage.getItem(HIDDEN_KEY) ?? "[]") as unknown;
    return Array.isArray(saved) ? DIAGNOSTIC_COLUMNS.filter((c) => saved.includes(c)) : [];
  } catch {
    return [];
  }
}

export function saveHiddenColumns(hidden: readonly DiagnosticColumn[]): void {
  try {
    localStorage.setItem(HIDDEN_KEY, JSON.stringify(hidden));
  } catch {
    // Storage can be unavailable; the choice then lasts until the window closes.
  }
}

/** The columns shown, in table order. The description always stays, so the table never goes blank. */
export function visibleColumns(hidden: readonly DiagnosticColumn[]): DiagnosticColumn[] {
  return DIAGNOSTIC_COLUMNS.filter((c) => c === "description" || !hidden.includes(c));
}

/** The width a column needs for its widest text (header included): `measure` gives the pixel width of a text, `extra` is the padding and icons around it. */
export function fitWidth(texts: readonly string[], measure: (text: string) => number, extra: number): number {
  return clampWidth(Math.max(0, ...texts.map(measure)) + extra);
}
