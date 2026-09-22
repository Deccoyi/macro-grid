import type { WidgetStyle } from "../types";

/**
 * The widget's own Style fields (background, border, alignment, ...), rendered as a stylesheet
 * that lives INSIDE the shadow root — not as inline `style` on the host element. Inline styles
 * always win the cascade, which would make it impossible for the user's own customCss (also
 * injected into the shadow root, after this one) to override anything via normal specificity.
 */
/** Applied when the widget doesn't set its own value, so a freshly added widget reads as a button against the canvas instead of being invisible (transparent, 0 radius). */
const DEFAULT_BACKGROUND = "#2d3136";
const DEFAULT_FOREGROUND = "#e6e7ea";
const DEFAULT_RADIUS = 8;

const ANIMATION_DURATIONS: Record<Exclude<WidgetStyle["animation"], "none" | undefined>, string> = {
  blink: "1s step-start",
  pulse: "1.4s ease-in-out",
};

export function widgetBaseCss(style: WidgetStyle | undefined): string {
  const host: string[] = [
    "display: block",
    "width: 100%",
    "height: 100%",
    "box-sizing: border-box",
    "overflow: hidden",
  ];

  host.push(`background: ${style?.background ?? DEFAULT_BACKGROUND}`);
  host.push(`color: ${style?.foreground ?? DEFAULT_FOREGROUND}`);
  if (style?.borderWidth) host.push(`border: ${style.borderWidth}px solid ${style.borderColor ?? "currentColor"}`);
  host.push(`border-radius: ${style?.radius ?? DEFAULT_RADIUS}px`);
  if (style?.fontSize) host.push(`font-size: ${style.fontSize}px`);
  if (style?.animation && style.animation !== "none") host.push(`animation: ms-${style.animation} ${ANIMATION_DURATIONS[style.animation]} infinite`);

  const content: string[] = [
    "display: flex",
    "width: 100%",
    "height: 100%",
    "box-sizing: border-box",
    "padding: 8px",
    "font-weight: 600",
    "white-space: pre-line",
    "word-break: break-word",
    "text-align: " + (style?.align ?? "center"),
    "align-items: " + verticalAlignToFlex(style?.vAlign),
    "justify-content: " + horizontalAlignToFlex(style?.align),
  ];

  return [
    `:host { ${host.join("; ")}; }`,
    `:host([data-active]) { box-shadow: inset 0 0 0 3px currentColor; }`,
    `.ms-content { ${content.join("; ")}; }`,
    SHARED_CONTENT_CSS,
  ].join("\n");
}

/** Layout for the per-type content components (ButtonContent, SliderContent, ...), independent of the widget's own colors. */
const SHARED_CONTENT_CSS = `
@keyframes ms-blink { 0%, 49% { opacity: 1; } 50%, 100% { opacity: .25; } }
@keyframes ms-pulse { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.045); } }
.ms-content-inner { display: flex; align-items: center; max-width: 100%; min-width: 0; }
.ms-icon { width: 28px; height: 28px; object-fit: contain; }
.ms-text { overflow-wrap: anywhere; }
.ms-slider, .ms-knob, .ms-image { flex-direction: column; gap: 6px; }
.ms-range { width: 100%; touch-action: pan-x; }
.ms-knob-dial { width: 60%; aspect-ratio: 1; touch-action: none; }
.ms-knob-track { fill: none; stroke: currentColor; opacity: .25; stroke-width: 6; }
.ms-knob-needle { stroke: currentColor; stroke-width: 6; stroke-linecap: round; }
.ms-image-img { max-width: 100%; max-height: 100%; object-fit: contain; flex: 1; min-height: 0; }
.ms-placeholder { opacity: .6; font-style: italic; }
`;

function verticalAlignToFlex(vAlign: WidgetStyle["vAlign"]): string {
  if (vAlign === "top") return "flex-start";
  if (vAlign === "bottom") return "flex-end";
  return "center";
}

function horizontalAlignToFlex(align: WidgetStyle["align"]): string {
  if (align === "left") return "flex-start";
  if (align === "right") return "flex-end";
  return "center";
}
