import { useCallback, useRef, useState } from "react";
import { Grid, PluginWidgetContext, WidgetView, gridArea, type Page, type Widget } from "@macro/renderer";
import { useT } from "../i18n/I18nContext";
import { canPlace, canPlaceExcluding, clamp, overlappingWidgets } from "./collision";
import { evaluateWidgetDynamicStyle, evaluateWidgetDynamicText } from "./evaluateDynamic";
import { usePluginWidgetPreview } from "./usePluginWidgetPreview";

interface EditorCanvasProps {
  page: Page;
  selectedIds: string[];
  onSelect: (ids: string[]) => void;
  onToggleSelect: (id: string) => void;
  onRectChange: (widgetId: string, rect: { x: number; y: number; w: number; h: number }) => void;
  onContextMenu: (x: number, y: number, widgetId: string) => void;
  variables: Record<string, unknown>;
}

type DragMode = "move" | "resize";
interface DragState {
  mode: DragMode;
  widgetId: string;
  startX: number;
  startY: number;
  startRect: { x: number; y: number; w: number; h: number };
  cellW: number;
  cellH: number;
}

/**
 * The editing surface: renders the page with the exact same @macro/renderer components the phone
 * uses (so "what you see is what you ship"), plus a light-DOM overlay per widget for selection,
 * dragging and resizing. The overlay never touches the widget's own shadow root.
 */
export function EditorCanvas({ page, selectedIds, onSelect, onToggleSelect, onRectChange, onContextMenu, variables }: EditorCanvasProps) {
  const { t } = useT();
  const pluginPreview = usePluginWidgetPreview(page.widgets, variables);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [previewRect, setPreviewRect] = useState<{ x: number; y: number; w: number; h: number } | null>(null);
  // Set while dragging a widget onto exactly one other widget that it can cleanly trade places with (the
  // grid may be completely full, so "just refuse the move" — the previous behavior — left no way to
  // rearrange anything). Only ever set for mode "move"; resizing into occupied space still just refuses.
  const [swapWithId, setSwapWithId] = useState<string | null>(null);
  const gap = `${page.gap ?? 10}px`;

  const beginDrag = useCallback(
    (mode: DragMode, widget: Widget, e: React.PointerEvent) => {
      const container = containerRef.current;
      if (!container) return;
      e.stopPropagation();
      (e.target as Element).setPointerCapture?.(e.pointerId);
      const bounds = container.getBoundingClientRect();
      setDrag({
        mode,
        widgetId: widget.id,
        startX: e.clientX,
        startY: e.clientY,
        startRect: { x: widget.x, y: widget.y, w: widget.w, h: widget.h },
        cellW: bounds.width / page.cols,
        cellH: bounds.height / page.rows,
      });
      setPreviewRect({ x: widget.x, y: widget.y, w: widget.w, h: widget.h });
      setSwapWithId(null);
    },
    [page.cols, page.rows],
  );

  const onPointerMove = useCallback(
    (e: React.PointerEvent) => {
      if (!drag) return;
      const dCellsX = Math.round((e.clientX - drag.startX) / drag.cellW);
      const dCellsY = Math.round((e.clientY - drag.startY) / drag.cellH);

      let next = { ...drag.startRect };
      if (drag.mode === "move") {
        next.x = clamp(drag.startRect.x + dCellsX, 0, page.cols - drag.startRect.w);
        next.y = clamp(drag.startRect.y + dCellsY, 0, page.rows - drag.startRect.h);
      } else {
        next.w = clamp(drag.startRect.w + dCellsX, 1, page.cols - drag.startRect.x);
        next.h = clamp(drag.startRect.h + dCellsY, 1, page.rows - drag.startRect.y);
      }

      if (canPlace(next, drag.widgetId, page.widgets, page.cols, page.rows)) {
        setPreviewRect(next);
        setSwapWithId(null);
        return;
      }

      // The cell(s) `next` covers are occupied. A resize into occupied space still just refuses (growing
      // a widget by pushing others around is a different, cascading-reflow feature); a plain move offers a
      // straight swap when it's blocked by exactly one other widget and trading places doesn't in turn
      // collide with anything else — the one case where "the grid is full" doesn't have to mean "you can't
      // rearrange anything".
      if (drag.mode === "move") {
        const blockers = overlappingWidgets(next, drag.widgetId, page.widgets);
        if (blockers.length === 1) {
          const other = blockers[0]!;
          const otherNewRect = { x: drag.startRect.x, y: drag.startRect.y, w: other.w, h: other.h };
          if (canPlaceExcluding(otherNewRect, new Set([drag.widgetId, other.id]), page.widgets, page.cols, page.rows)) {
            setPreviewRect(next);
            setSwapWithId(other.id);
            return;
          }
        }
      }
      // No valid move/resize/swap for this position — freeze the preview at wherever it last was, same
      // as always (leaving `previewRect`/`swapWithId` untouched, rather than snapping back to startRect,
      // keeps the ghost from jumping around while the pointer briefly crosses an invalid cell).
    },
    [drag, page.cols, page.rows, page.widgets],
  );

  const endDrag = useCallback(() => {
    // A plain click (no actual move/resize) still goes through pointerdown->pointerup here; only
    // commit — and mark the profile dirty — when the rect genuinely changed.
    if (drag && previewRect && !sameRect(previewRect, drag.startRect)) {
      onRectChange(drag.widgetId, previewRect);
      if (swapWithId) {
        const other = page.widgets.find((w) => w.id === swapWithId);
        if (other) onRectChange(swapWithId, { x: drag.startRect.x, y: drag.startRect.y, w: other.w, h: other.h });
      }
    }
    setDrag(null);
    setPreviewRect(null);
    setSwapWithId(null);
  }, [drag, previewRect, swapWithId, page.widgets, onRectChange]);

  return (
    <div
      ref={containerRef}
      style={{ position: "relative", width: "100%", height: "100%" }}
      onPointerMove={onPointerMove}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      onPointerDown={() => onSelect([])}
    >
      <PluginWidgetContext.Provider value={pluginPreview.context}>
      <Grid
        page={page}
        gap={gap}
        renderWidget={(widget) => {
          const isDraggingThis = drag?.widgetId === widget.id;
          const isSwapTarget = swapWithId === widget.id;
          const rect = isDraggingThis && previewRect ? previewRect : widget;
          const isSelected = selectedIds.includes(widget.id);
          return (
            <div style={{ position: "relative", width: "100%", height: "100%" }}>
              <WidgetView
                widget={pluginPreview.withRuntime(rect === widget ? widget : { ...widget, ...rect })}
                liveText={renderPreviewText(evaluateWidgetDynamicText(widget, variables), variables)}
                liveStyle={evaluateWidgetDynamicStyle(widget, variables)}
                haptics={false}
                // The real page, as the phone will show it; the frame ignores the pointer so the widget can still be selected and dragged.
                webInteractive={false}
                style={{ pointerEvents: "none", opacity: isDraggingThis || isSwapTarget ? 0.55 : 1 }}
              />
              <div
                onPointerDown={(e) => {
                  e.stopPropagation();
                  if (e.shiftKey) {
                    onToggleSelect(widget.id);
                    return;
                  }
                  if (!isSelected) onSelect([widget.id]);
                  beginDrag("move", widget, e);
                }}
                onContextMenu={(e) => {
                  e.preventDefault();
                  e.stopPropagation();
                  if (!isSelected) onSelect([widget.id]);
                  onContextMenu(e.clientX, e.clientY, widget.id);
                }}
                style={{
                  position: "absolute",
                  inset: 0,
                  cursor: "grab",
                  outline: isSelected ? "2px solid var(--ms-accent)" : "2px solid transparent",
                  outlineOffset: -2,
                  borderRadius: widget.style?.radius ?? 4,
                }}
              />
              {isSelected && (
                <div
                  onPointerDown={(e) => beginDrag("resize", widget, e)}
                  title={t("canvas.resize")}
                  style={{
                    position: "absolute",
                    right: -4,
                    bottom: -4,
                    width: 14,
                    height: 14,
                    borderRadius: 3,
                    background: "var(--ms-accent)",
                    cursor: "nwse-resize",
                  }}
                />
              )}
            </div>
          );
        }}
      />
      </PluginWidgetContext.Provider>

      {drag && previewRect && (
        <div
          style={{
            position: "absolute", inset: 0, pointerEvents: "none",
            display: "grid",
            gridTemplateColumns: `repeat(${page.cols}, 1fr)`,
            gridTemplateRows: `repeat(${page.rows}, 1fr)`,
            gap,
          }}
        >
          <div
            style={{
              gridArea: gridArea(previewRect),
              background: "var(--ms-accent-bg-muted)",
              border: "2px dashed var(--ms-accent)",
              borderRadius: 4,
            }}
          />
          {swapWithId && (() => {
            const other = page.widgets.find((w) => w.id === swapWithId);
            if (!other) return null;
            // Where `other` will land — the dragged widget's original slot, at other's own size — so the
            // swap reads as "these two trade places" rather than just "this one widget is moving".
            return (
              <div
                style={{
                  gridArea: gridArea({ x: drag.startRect.x, y: drag.startRect.y, w: other.w, h: other.h }),
                  background: "var(--ms-accent-bg-muted)",
                  border: "2px dashed var(--ms-accent)",
                  borderRadius: 4,
                }}
              />
            );
          })()}
        </div>
      )}
    </div>
  );
}

function sameRect(a: { x: number; y: number; w: number; h: number }, b: { x: number; y: number; w: number; h: number }): boolean {
  return a.x === b.x && a.y === b.y && a.w === b.w && a.h === b.h;
}

/**
 * Best-effort local substitution of {name|format} tokens using the one-shot variable snapshot, so a
 * template widget shows something close to what the server's Template.Render would produce, rather
 * than either the raw "{system.cpu}" text or (worse) an unformatted "2026-09-22T10:09:03.006Z". Only
 * approximates the server's number/date formatting — the server's own render always wins at runtime.
 */
function renderPreviewText(text: string | undefined, variables: Record<string, unknown>): string | undefined {
  if (!text || !text.includes("{")) return text;
  return text.replace(/\{\{|\}\}|\{([^{}|]+)(?:\|([^{}]*))?\}/g, (match, name: string | undefined, format: string | undefined) => {
    if (match === "{{") return "{";
    if (match === "}}") return "}";
    if (!name) return match;
    const value = variables[name.trim()];
    return value === undefined ? "" : formatPreviewValue(value, format);
  });
}

function formatPreviewValue(value: unknown, format: string | undefined): string {
  if (typeof value === "number") return formatNumber(value, format);
  if (typeof value === "string" && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(value)) {
    const date = new Date(value);
    if (!Number.isNaN(date.getTime())) return formatDate(date, format);
  }
  return String(value);
}

function formatNumber(value: number, format: string | undefined): string {
  const decimalsMatch = /^0\.(#+)$/.exec(format ?? "0.##");
  const maxDecimals = decimalsMatch ? decimalsMatch[1]!.length : 0;
  const fixed = value.toFixed(maxDecimals);
  return maxDecimals > 0 ? fixed.replace(/\.?0+$/, "") : fixed;
}

function formatDate(date: Date, format: string | undefined): string {
  const pad = (n: number) => n.toString().padStart(2, "0");
  const tokens: Record<string, string> = {
    HH: pad(date.getHours()),
    mm: pad(date.getMinutes()),
    ss: pad(date.getSeconds()),
    dd: pad(date.getDate()),
    MM: pad(date.getMonth() + 1),
    yyyy: date.getFullYear().toString(),
  };
  const pattern = format ?? "HH:mm";
  return pattern.replace(/HH|mm|ss|dd|MM|yyyy/g, (token) => tokens[token] ?? token);
}
