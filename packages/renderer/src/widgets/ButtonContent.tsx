import type { CSSProperties } from "react";
import type { IconPosition } from "../types";

/** Marks an icon image (inside its SVG) as a plain shape that may be recoloured: the renderer then paints it with the text colour. */
export const ICON_TINT_MARKER = "data-mg-tint";

/** True for an icon the editor baked as a recolourable shape. Older icons carry their own colour and are drawn as they are. */
export function isTintableIcon(icon: string | undefined): boolean {
  return icon !== undefined && icon.startsWith("data:image/svg+xml") && icon.includes(ICON_TINT_MARKER);
}

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

function tintStyle(icon: string): CSSProperties {
  const mask = `url("${icon.replace(/"/g, "%22")}")`;
  return {
    display: "block",
    backgroundColor: "currentColor",
    WebkitMaskImage: mask,
    maskImage: mask,
    WebkitMaskRepeat: "no-repeat",
    maskRepeat: "no-repeat",
    WebkitMaskPosition: "center",
    maskPosition: "center",
    WebkitMaskSize: "contain",
    maskSize: "contain",
  };
}

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
        {icon && (isTintableIcon(icon) ? (
          // The shape is a mask painted with the text colour (currentColor), so a dynamic text colour recolours the icon too.
          <span className="ms-icon ms-icon-tint" aria-hidden="true" style={{ ...tintStyle(icon), width: iconSize, height: iconSize, margin: 0, flex: "none" }} />
        ) : (
          <img className="ms-icon" src={icon} alt="" aria-hidden="true" style={{ width: iconSize, height: iconSize, margin: 0, flex: "none" }} />
        ))}
        {text && <span className="ms-text">{text}</span>}
      </div>
    </div>
  );
}
