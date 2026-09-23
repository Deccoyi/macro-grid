import { useMemo, type CSSProperties } from "react";
import { usePressGesture } from "../interaction/usePressGesture";
import { sanitizeWidgetCss } from "../style/sanitizeCss";
import { ShadowHost } from "../style/ShadowHost";
import { widgetBaseCss } from "../style/widgetBaseCss";
import type { Widget, WidgetStyle } from "../types";
import { ButtonContent } from "./ButtonContent";
import { ImageContent } from "./ImageContent";
import { KnobContent } from "./KnobContent";
import { PlaceholderContent } from "./PlaceholderContent";
import { SliderContent } from "./SliderContent";

export interface WidgetViewProps {
  widget: Widget;
  /** Overrides widget.text; comes from the server's widget.state push for template-driven widgets. */
  liveText?: string;
  /** Overrides the toggle's on/off look; comes from widget.state. */
  liveActive?: boolean;
  /** Current slider/knob value; comes from widget.state or local drag state. */
  liveValue?: number;
  /** Resolved dynamic-style overrides (background/foreground/borderColor) from widget.state; merged on top of widget.style. */
  liveStyle?: Partial<Pick<WidgetStyle, "background" | "foreground" | "borderColor" | "icon">> & { animation?: string };
  onPress?: () => void;
  onRelease?: () => void;
  onLongPress?: () => void;
  onDoubleTap?: () => void;
  onValueChange?: (value: number) => void;
  onValueCommit?: (value: number) => void;
  haptics?: boolean;
  className?: string;
  style?: CSSProperties;
}

// A label is pure display; every other type reacts to touch (even "web"/"plugin-html", which may
// still bind a press action alongside their embedded content).
const NON_INTERACTIVE_TYPES = new Set<Widget["type"]>(["label"]);

/** Renders one widget: sanitizes its CSS, wires up press/long-press/double-tap, and dispatches to the right content by type. */
export function WidgetView({
  widget,
  liveText,
  liveActive,
  liveValue,
  liveStyle,
  onPress,
  onRelease,
  onLongPress,
  onDoubleTap,
  onValueChange,
  onValueCommit,
  haptics,
  className,
  style,
}: WidgetViewProps) {
  const effectiveStyle = useMemo<WidgetStyle | undefined>(
    // liveStyle.animation is resolved server-side as a plain string (like background/foreground already
    // are); it's only ever one of WidgetAnimation's values in practice, so this cast is safe.
    () => (liveStyle ? ({ ...widget.style, ...liveStyle } as WidgetStyle) : widget.style),
    [widget.style, liveStyle],
  );
  const baseCss = useMemo(() => widgetBaseCss(effectiveStyle), [effectiveStyle]);
  const { css: customCss } = useMemo(() => sanitizeWidgetCss(widget.customCss), [widget.customCss]);
  const interactive = !NON_INTERACTIVE_TYPES.has(widget.type);
  const gesture = usePressGesture({ onPress, onRelease, onLongPress, onDoubleTap, haptics });

  const text = liveText ?? widget.text ?? "";

  return (
    <ShadowHost
      baseCss={baseCss}
      customCss={customCss}
      active={liveActive}
      className={className}
      style={{ touchAction: interactive ? "none" : "auto", cursor: interactive ? "pointer" : "default", ...style }}
      onPointerDown={interactive ? gesture.onPointerDown : undefined}
      onPointerUp={interactive ? gesture.onPointerUp : undefined}
      onPointerCancel={interactive ? gesture.onPointerCancel : undefined}
    >
      {renderContent(widget, text, effectiveStyle, liveValue, onValueChange, onValueCommit)}
    </ShadowHost>
  );
}

function renderContent(
  widget: Widget,
  text: string,
  effectiveStyle: WidgetStyle | undefined,
  liveValue: number | undefined,
  onValueChange: ((value: number) => void) | undefined,
  onValueCommit: ((value: number) => void) | undefined,
) {
  switch (widget.type) {
    case "image":
      return <ImageContent text={text} src={typeof widget.props?.src === "string" ? widget.props.src : undefined} />;
    case "slider":
      return <SliderContent text={text} value={liveValue} props={widget.props} onChange={onValueChange} onCommit={onValueCommit} />;
    case "knob":
      return <KnobContent text={text} value={liveValue} props={widget.props} onChange={onValueChange} onCommit={onValueCommit} />;
    case "web":
      return <PlaceholderContent label={text || "Web"} />;
    case "plugin-html":
      return <PlaceholderContent label={text || "Plugin"} />;
    case "button":
    case "toggle":
    case "label":
    default:
      return (
        <ButtonContent
          text={text}
          icon={effectiveStyle?.icon}
          iconSize={effectiveStyle?.iconSize}
          iconPosition={effectiveStyle?.iconPosition}
        />
      );
  }
}
