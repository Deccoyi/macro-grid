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
import { PluginWidgetContent } from "./pluginWidget/PluginWidgetContent";
import { WebContent, type WebBlocked, type WebTexts } from "./WebContent";

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
  /** A `web` widget only: the address this device shows instead of the widget's own (set by a button; from widget.state). Empty or absent means the widget's own. */
  webUrl?: string;
  /** A `web` widget only: a higher number than before loads the page again. */
  webReload?: number;
  /** A `web` widget only: false in the editor, so the widget can still be selected and dragged over the page. Default true. */
  webInteractive?: boolean;
  /** A `web` widget only: false draws the host name instead of the page (not the shown page of the grid, app in the background). Default true. */
  webLive?: boolean;
  /** A `web` widget only: host names to refuse besides the built-in rule, for example the server's own address. */
  webBlockedHosts?: readonly string[];
  /** A `web` widget only: replaces the English words of the placeholder. */
  webTexts?: Partial<WebTexts>;
  /** A `web` widget only: draw a placeholder with a button instead of the page (the site crashed the app, or the widget waits with "Tap to load"). */
  webBlocked?: WebBlocked;
  /** A `plugin-widget` only: false when its page is not the one shown (its worker is stopped, or paused when it keeps itself loaded). Default true. */
  pluginLive?: boolean;
  /** Variables this widget uses that have no value now (from widget.state). A non-empty list fades the widget; the editor preview passes it too. */
  unavailable?: readonly string[];
  /** When set, a small warning mark sits in the corner of an unavailable widget and this is called with the variable names when it is pressed. */
  onUnavailableTap?: (names: readonly string[]) => void;
  /** Accessible name and tooltip of the warning mark. */
  unavailableLabel?: string;
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
  webUrl,
  webReload,
  webInteractive,
  webLive,
  webBlockedHosts,
  webTexts,
  webBlocked,
  pluginLive,
  unavailable,
  onUnavailableTap,
  unavailableLabel,
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

  const faded = unavailable !== undefined && unavailable.length > 0;
  const hostStyle: CSSProperties = {
    touchAction: interactive && widget.type !== "web" ? "none" : "auto",
    cursor: interactive ? "pointer" : "default",
    ...style,
    ...(faded ? { opacity: Number(style?.opacity ?? 1) * UNAVAILABLE_OPACITY } : null),
  };

  const view = (
    <ShadowHost
      baseCss={baseCss}
      customCss={customCss}
      active={liveActive}
      className={className}
      // A web page scrolls and zooms inside its own frame: the widget's press handling must not claim its touches.
      style={hostStyle}
      onPointerDown={interactive ? gesture.onPointerDown : undefined}
      onPointerUp={interactive ? gesture.onPointerUp : undefined}
      onPointerCancel={interactive ? gesture.onPointerCancel : undefined}
    >
      {widget.type === "web" ? (
        <WebContent
          url={webUrl ? webUrl : typeof widget.props?.url === "string" ? widget.props.url : undefined}
          reload={webReload}
          interactive={webInteractive}
          live={webLive}
          blockedHosts={webBlockedHosts}
          texts={webTexts}
          blocked={webBlocked}
        />
      ) : widget.type === "plugin-widget" ? (
        <PluginWidgetContent widget={widget} live={pluginLive} />
      ) : (
        renderContent(widget, text, effectiveStyle, liveValue, onValueChange, onValueCommit)
      )}
    </ShadowHost>
  );

  if (!faded || !onUnavailableTap) return view;
  // The mark is a sibling of the (faded) widget, not part of it, so it keeps full strength and never takes the widget's own touches.
  return (
    <div style={{ position: "relative", width: "100%", height: "100%" }}>
      {view}
      <button
        type="button"
        title={unavailableLabel}
        aria-label={unavailableLabel}
        onPointerDown={(e) => e.stopPropagation()}
        onClick={(e) => {
          e.stopPropagation();
          onUnavailableTap(unavailable);
        }}
        style={{
          position: "absolute",
          top: 4,
          right: 4,
          width: 20,
          height: 20,
          padding: 0,
          border: "none",
          borderRadius: "50%",
          background: "#facc15",
          color: "#1a1a1a",
          font: "700 13px/20px system-ui, sans-serif",
          textAlign: "center",
          cursor: "pointer",
        }}
      >
        !
      </button>
    </div>
  );
}

/** How strongly an unavailable widget is faded (1 is untouched). Steady, never blinking. */
const UNAVAILABLE_OPACITY = 0.4;

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
