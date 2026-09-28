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

/** Like `canPlace`, but ignoring several widgets at once — used when checking where a widget being
 * displaced by a drag-swap would land, which must ignore both the dragged widget's old slot (about to be
 * vacated) and the displaced widget's own old slot (it's the one moving). */
export function canPlaceExcluding(rect: Rect, excludeIds: Set<string>, widgets: Widget[], cols: number, rows: number): boolean {
  if (!fitsOnGrid(rect, cols, rows)) return false;
  return widgets.every((w) => excludeIds.has(w.id) || !overlaps(rect, w));
}

/** Every widget (other than `excludeId`) whose rect intersects `rect` — used to find what a drag target
 * is colliding with, to offer a swap instead of just refusing the move. */
export function overlappingWidgets(rect: Rect, excludeId: string, widgets: Widget[]): Widget[] {
  return widgets.filter((w) => w.id !== excludeId && overlaps(rect, w));
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

/** Free cells for a whole batch of widgets being pasted at once, each reserving its spot before the next
 * one is searched for (so two pasted widgets never land on top of each other) — or null if even one of
 * them has nowhere left to go, so a multi-widget paste can be refused as a whole rather than landing only
 * some of what was copied. */
export function findFreeCellsForBatch(sizes: { w: number; h: number }[], widgets: Rect[], cols: number, rows: number): { x: number; y: number }[] | null {
  const placed: Rect[] = [...widgets];
  const results: { x: number; y: number }[] = [];
  for (const size of sizes) {
    let found: { x: number; y: number } | null = null;
    for (let y = 0; found === null && y <= rows - size.h; y++) {
      for (let x = 0; x <= cols - size.w; x++) {
        const rect = { x, y, w: size.w, h: size.h };
        if (fitsOnGrid(rect, cols, rows) && placed.every((p) => !overlaps(rect, p))) { found = { x, y }; break; }
      }
    }
    if (!found) return null;
    results.push(found);
    placed.push({ ...found, w: size.w, h: size.h });
  }
  return results;
}
