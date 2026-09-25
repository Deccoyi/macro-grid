import type { Widget } from "@macro/renderer";

type Rect = Pick<Widget, "x" | "y" | "w" | "h">;

function overlaps(a: Rect, b: Rect): boolean {
  return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

function fitsOnGrid(rect: Rect, cols: number, rows: number): boolean {
  return rect.x >= 0 && rect.y >= 0 && rect.x + rect.w <= cols && rect.y + rect.h <= rows;
}

/** True if `rect` (belonging to `widgetId`, or a brand new widget when omitted) can be placed without overlapping any other widget. */
export function canPlace(rect: Rect, widgetId: string | undefined, widgets: Widget[], cols: number, rows: number): boolean {
  if (!fitsOnGrid(rect, cols, rows)) return false;
  return widgets.every((w) => w.id === widgetId || !overlaps(rect, w));
}

/** First free cell (row-major scan) that fits a widget of size w×h, or null if the page is full. */
export function findFreeCell(w: number, h: number, widgets: Widget[], cols: number, rows: number): { x: number; y: number } | null {
  for (let y = 0; y <= rows - h; y++) {
    for (let x = 0; x <= cols - w; x++) {
      if (canPlace({ x, y, w, h }, undefined, widgets, cols, rows)) return { x, y };
    }
  }
  return null;
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
