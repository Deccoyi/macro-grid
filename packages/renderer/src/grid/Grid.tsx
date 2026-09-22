import type { CSSProperties, ReactNode } from "react";
import type { Page, Widget } from "../types";

export interface GridProps {
  page: Pick<Page, "cols" | "rows" | "widgets" | "gap" | "padding" | "alignment">;
  renderWidget: (widget: Widget) => ReactNode;
  /** CSS gap between cells. Defaults to `page.gap` (px) if set, else a CSS variable so a host page can theme it without either. */
  gap?: string;
  className?: string;
  style?: CSSProperties;
}

/**
 * Lays widgets out on a CSS grid from their x/y/w/h cell coordinates. This is the one piece of
 * layout math shared by the editor canvas and the phone client — both must snap widgets to the
 * exact same cells, or a layout built in the editor would look different on the phone.
 */
export function Grid({ page, renderWidget, gap, className, style }: GridProps) {
  return (
    <div
      className={className}
      style={{
        display: "grid",
        gridTemplateColumns: `repeat(${page.cols}, 1fr)`,
        gridTemplateRows: `repeat(${page.rows}, 1fr)`,
        gap: gap ?? (page.gap !== undefined ? `${page.gap}px` : "var(--ms-grid-gap, 10px)"),
        padding: page.padding ? `${page.padding}px` : undefined,
        boxSizing: "border-box",
        placeContent: page.alignment ?? "center",
        width: "100%",
        height: "100%",
        ...style,
      }}
    >
      {page.widgets.map((widget) => (
        <div key={widget.id} style={{ gridArea: gridArea(widget), minWidth: 0, minHeight: 0 }}>
          {renderWidget(widget)}
        </div>
      ))}
    </div>
  );
}

/** 1-based CSS grid-area string from a widget's 0-based cell coordinates and span. */
export function gridArea(widget: Pick<Widget, "x" | "y" | "w" | "h">): string {
  return `${widget.y + 1} / ${widget.x + 1} / span ${widget.h} / span ${widget.w}`;
}
