import type { CSSProperties } from "react";
import type { IconPosition } from "../types";

export interface ButtonContentProps {
  text: string;
  icon?: string;
  /** Icon box size in px. The icon is a vector (typically an SVG data URI), so resizing needs no re-render. Default 28. */
  iconSize?: number;
  /** Where the icon sits relative to the text. Default "top". */
  iconPosition?: IconPosition;
}

const FLEX_DIRECTION: Record<IconPosition, CSSProperties["flexDirection"]> = {
  top: "column",
  bottom: "column-reverse",
  left: "row",
  right: "row-reverse",
};

/**
 * Shared visual for button, toggle and label widgets: an optional icon positioned around the
 * (possibly multi-line) text.
 *
 * The icon+text pair lives in its own inner flex box (`.ms-content-inner`) that only ever controls
 * icon-to-text layout (row/column by iconPosition). The OUTER `.ms-content` box — whose
 * justify-content/align-items come from the widget's own align/vAlign — always stays row-direction
 * and just positions that inner box as a single unit. Flipping `.ms-content` itself to
 * `flex-direction: column` for a top/bottom icon (the previous approach) swapped its main/cross axes,
 * which silently swapped the meaning of justify-content and align-items — vertical align settings
 * moved the block horizontally instead. Keeping the two boxes separate avoids that entirely.
 */
export function ButtonContent({ text, icon, iconSize = 28, iconPosition = "top" }: ButtonContentProps) {
  const row = iconPosition === "left" || iconPosition === "right";
  return (
    <div className="ms-content">
      <div className="ms-content-inner" style={icon ? { flexDirection: FLEX_DIRECTION[iconPosition], gap: row ? 8 : 4 } : undefined}>
        {icon && <img className="ms-icon" src={icon} alt="" aria-hidden="true" style={{ width: iconSize, height: iconSize, margin: 0, flex: "none" }} />}
        {text && <span className="ms-text">{text}</span>}
      </div>
    </div>
  );
}
