/** Why a plugin widget cannot run (server side, `props.runtime.unavailable`), or a reason the renderer itself stopped it. */
export type PluginWidgetUnavailable =
  | "missing"
  | "disabled"
  | "needsApproval"
  | "incompatible"
  | "invalid"
  | "noWidget"
  /** The server did not send the widget's code (an older app or server). */
  | "unsupported"
  /** Plugin widgets are switched off on this device. */
  | "off"
  /** The device switched this plugin's widgets off because they crashed the app. */
  | "crashedOff";

/** What the server adds to a `plugin-widget` (`props.runtime`); never saved in a profile. */
export interface PluginWidgetRuntimeInfo {
  name?: string;
  /** The script, as an asset reference (`asset:<hash>`). */
  code?: string;
  /** Package image and font files by the name the widget uses, as asset references. */
  assets?: Record<string, string>;
  /** The most frames per second the widget may draw. */
  fps?: number;
  interactive?: boolean;
  /** The options the widget declared and the person approved (`keepLoaded`, `storage`, `notifications`). */
  options?: string[];
  /** Keys of the widget's settings that are `Variable` fields (the person's variable bindings). */
  variables?: string[];
  /** False for a plugin that is not verified (JavaScript plugins): stricter limits apply. */
  verified?: boolean;
  unavailable?: PluginWidgetUnavailable;
}

/** The lifecycle of one running widget as the cell shows it. */
export type PluginWidgetState =
  | { kind: "starting" }
  | { kind: "running" }
  | { kind: "paused" }
  | { kind: "stopped"; reason: PluginWidgetStopReason };

export type PluginWidgetStopReason =
  | "frozen"
  | "startTimeout"
  | "tooBusy"
  | "tooMany"
  | "crashed"
  | "failed";

/** An error a widget request or run can fail with; `code` is one of the server's short codes. */
export class PluginWidgetError extends Error {
  constructor(
    public readonly code: string,
    message?: string,
  ) {
    super(message ?? code);
  }
}

/**
 * What a place that draws plugin widgets (the phone app, the browser deck, the editor) gives the renderer: the connection to the server for
 * one widget, kept apart from the drawing so the same runtime works everywhere. All widget ids are the placed widget's id in the profile.
 */
import type { WidgetDataStore } from "./storage";

export interface PluginWidgetHost {
  /** "run": a device showing the deck. "edit": the editor's preview (a run is refused, pointer input goes to the editor). */
  readonly mode: "run" | "edit";
  readonly locale?: string;
  readonly theme?: "light" | "dark";
  /** The text of a script, or the data URI of an image or font, for an asset reference. */
  loadAsset(reference: string): Promise<string>;
  request(widgetId: string, data: unknown): Promise<unknown>;
  run(widgetId: string, action: string, settings: unknown, userGesture: boolean): Promise<void>;
  subscribe(widgetId: string, variables: string[]): void;
  ready(widgetId: string): void;
  reportError(widgetId: string, message: string): void;
  /** The widget's own small store, for a widget that declared the `storage` option. Absent where the host keeps none. */
  storage?: WidgetDataStore;
  /** Receives what the server pushes for one widget: changed variable values and events. Returns the way to stop listening. */
  listen(widgetId: string, listener: PluginWidgetListener): () => void;
}

export interface PluginWidgetListener {
  vars(values: Record<string, unknown>): void;
  event(name: string, data: unknown): void;
}

/** Numbers of section 6 of the plan. One place, so the tests and the code agree. */
export const PLUGIN_WIDGET_LIMITS = {
  /** Live workers on one device. */
  maxLive: 8,
  /** Live workers of plugins that are not verified. */
  maxLiveUnverified: 2,
  /** The sum of the frame-rate caps of all live widgets; above it every widget is scaled down. */
  frameBudget: 120,
  /** How long a widget counts as drawing after its last report of frames. */
  activeForMs: 12000,
  maxDpr: 2,
  pingEveryMs: 1000,
  frozenAfterMs: 3000,
  startTimeoutMs: 3000,
  restartAfterMs: 10000,
  busyLimit: 0.5,
  busyLimitUnverified: 0.25,
  maxRequestChars: 16 * 1024,
  requestsPerSecond: 10,
  maxSubscribed: 32,
  errorsPer10s: 5,
  /** A real touch grants one run for this long. */
  gestureMs: 1000,
} as const;
