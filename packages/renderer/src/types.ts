/**
 * Mirrors server/src/MacroStation.Core/Model/Profile.cs and MacroStation.Protocol/Messages.cs.
 * Field names are camelCase because the server serializes with JsonSerializerDefaults.Web.
 * Keep this in sync by hand for now; a generated-types step can replace it later.
 */

export type WidgetType = "button" | "toggle" | "slider" | "knob" | "label" | "image" | "web" | "plugin-html";

export type Align = "left" | "center" | "right";
export type VAlign = "top" | "middle" | "bottom";
export type IconPosition = "top" | "left" | "right" | "bottom";
export type WidgetAnimation = "none" | "blink" | "pulse";

export interface WidgetStyle {
  background?: string;
  foreground?: string;
  align?: Align;
  vAlign?: VAlign;
  fontSize?: number;
  borderColor?: string;
  borderWidth?: number;
  radius?: number;
  /** Rendered icon, as an <img>-compatible URL (typically a data:image/svg+xml URI baked by the editor's icon picker). */
  icon?: string;
  /** Icon box size in px; the icon itself is a vector, so this needs no re-render — just a bigger/smaller <img>. Default 28. */
  iconSize?: number;
  /** Where the icon sits relative to the text. Default "top". */
  iconPosition?: IconPosition;
  /**
   * Editor-only bookkeeping: which named icon (e.g. a lucide icon name) `icon` was baked from, so the
   * editor can re-bake at a new color without the user re-picking. The renderer never reads this.
   */
  iconName?: string;
  /** A looping CSS animation on the widget itself (e.g. blinking red on a critical alarm). Default "none". */
  animation?: WidgetAnimation;
}

export type WidgetEventName = "press" | "release" | "longPress" | "doubleTap" | "toggleOn" | "toggleOff";

export type ConditionKind = "compare" | "and" | "or" | "xor" | "not";
export type CompareOperator = ">" | ">=" | "<" | "<=" | "==" | "!=" | "between";

/**
 * A boolean condition tree: a single comparison (kind "compare") or a combinator over children
 * (kind "and"/"or"/"xor"/"not"). Pure data — mirrors server DynamicRuleEvaluator's ConditionNode 1:1.
 */
export interface ConditionNode {
  kind: ConditionKind;
  /** Used when kind === "compare". */
  variable?: string;
  operator?: CompareOperator;
  value?: string;
  /** Second bound for the "between" operator only. */
  value2?: string;
  /** Used when kind is "and"/"or"/"xor" (any number) or "not" (exactly one). */
  children?: ConditionNode[];
}

export interface DynamicCase {
  condition: ConditionNode;
  result: string;
}

/** One dynamized property: resolves to the first matching case's result, or `default` if none match (server field name is "Default", camelCased). */
export interface DynamicBinding {
  cases: DynamicCase[];
  default?: string;
}

/** One action bound to a widget event. `settings` shape depends on `type` (see the action's own settings helper server-side). */
export interface ActionBinding {
  type: string;
  settings: Record<string, unknown>;
}

export interface Widget {
  id: string;
  type: WidgetType;
  x: number;
  y: number;
  w: number;
  h: number;
  text?: string;
  style?: WidgetStyle;
  /** User-written CSS; the renderer sanitizes it before use (see style/sanitizeCss). */
  customCss?: string;
  /** Type-specific settings, e.g. { min, max, step } for a slider or { src } for an image. */
  props?: Record<string, unknown>;
  /**
   * Event name -> ordered actions. The renderer itself never reads this (it only renders and reports
   * gestures via callbacks) — it's here so the editor and, later, the client's action runner can pass
   * a Widget through unchanged and round-trip this field with the server.
   */
  actions: Partial<Record<WidgetEventName, ActionBinding[]>>;
  /**
   * Property path (e.g. "style.background") to the rule that computes it from a live variable. The
   * renderer never evaluates this itself — the server resolves it and pushes the result via
   * widget.state (see WidgetState.style); the editor evaluates it locally for its own live preview.
   */
  dynamic?: Record<string, DynamicBinding>;
}

export interface Page {
  id: string;
  name: string;
  cols: number;
  rows: number;
  widgets: Widget[];
}

export interface Profile {
  id: string;
  name: string;
  pages: Page[];
}

/** Live push from the server (widget.state): only the fields that changed are present. */
export interface WidgetState {
  widgetId: string;
  text?: string;
  value?: number;
  active?: boolean;
  /** From a dynamized property (see DynamicBinding server-side): property name to resolved value ("animation" is one of WidgetAnimation, the rest are CSS colors). */
  style?: Partial<Record<"background" | "foreground" | "borderColor" | "animation", string>>;
}
