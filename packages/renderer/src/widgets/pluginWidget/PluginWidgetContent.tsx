import { createContext, useContext, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from "react";
import type { Widget } from "../../types";
import { PlaceholderContent } from "../PlaceholderContent";
import type { PluginWidgetInstance, PluginWidgetRuntime } from "./runtime";
import type { PluginWidgetHost, PluginWidgetRuntimeInfo, PluginWidgetState, PluginWidgetStopReason, PluginWidgetUnavailable } from "./types";

/** What draws plugin widgets needs: the runtime that owns the workers and the host that talks to the server. Without it a plugin widget draws a placeholder. */
export interface PluginWidgetContextValue {
  runtime: PluginWidgetRuntime;
  host: PluginWidgetHost;
  /** False when plugin widgets are switched off on this device. */
  enabled?: boolean;
  texts?: Partial<PluginWidgetTexts>;
}

export const PluginWidgetContext = createContext<PluginWidgetContextValue | null>(null);

export interface PluginWidgetTexts {
  widget: string;
  restart: string;
  unavailable: Record<PluginWidgetUnavailable, string>;
  stopped: Record<PluginWidgetStopReason, string>;
}

export const DEFAULT_PLUGIN_WIDGET_TEXTS: PluginWidgetTexts = {
  widget: "Plugin widget",
  restart: "Restart",
  unavailable: {
    missing: "The plugin is not installed",
    disabled: "The plugin is switched off",
    needsApproval: "The plugin needs approval",
    incompatible: "The plugin does not fit this Macro Grid",
    invalid: "The widget was refused",
    noWidget: "The plugin has no such widget",
    unsupported: "This app cannot show plugin widgets",
    off: "Plugin widgets are off on this device",
  },
  stopped: {
    frozen: "Stopped: not responding",
    startTimeout: "Stopped: did not start",
    tooBusy: "Stopped: too much processor time",
    tooMany: "Paused: too many widgets on this page",
    crashed: "Stopped: crashed",
    failed: "Stopped: could not run",
  },
};

export function runtimeInfoOf(widget: Widget): PluginWidgetRuntimeInfo | undefined {
  const runtime = widget.props?.runtime;
  return runtime && typeof runtime === "object" ? (runtime as PluginWidgetRuntimeInfo) : undefined;
}

export interface PluginWidgetContentProps {
  widget: Widget;
  /** True while the widget's page is the one shown. A widget that is not shown is stopped (or paused when it keeps itself loaded). */
  live?: boolean;
}

/**
 * The cell of a `plugin-widget`: a container the worker's canvas is put in, the pointer forwarding, and the placeholder for a widget that cannot run.
 * The canvas is made and moved into the container by the runtime, never by React: a canvas can be handed to a worker only once.
 */
export function PluginWidgetContent({ widget, live = true }: PluginWidgetContentProps) {
  const context = useContext(PluginWidgetContext);
  const texts = { ...DEFAULT_PLUGIN_WIDGET_TEXTS, ...context?.texts };
  const info = runtimeInfoOf(widget);
  const label = info?.name ?? widget.text ?? texts.widget;

  const container = useRef<HTMLDivElement>(null);
  const instance = useRef<PluginWidgetInstance | null>(null);
  const [state, setState] = useState<PluginWidgetState>({ kind: "starting" });
  const keepLoaded = info?.options?.includes("keepLoaded") === true;
  const keptMounted = useRef(false);
  const settings = (widget.props?.settings as Record<string, unknown> | undefined) ?? EMPTY;
  const settingsKey = JSON.stringify(settings);
  const runnable = !!context && context.enabled !== false && !!info?.code && !info.unavailable;
  const mountWanted = runnable && (live || (keepLoaded && keptMounted.current));

  useEffect(() => {
    if (!mountWanted || !context || !info?.code || !container.current) return;
    const box = container.current;
    const rect = box.getBoundingClientRect();
    const created = context.runtime.mount({
      widgetId: widget.id,
      container: box,
      info,
      settings,
      host: context.host,
      width: Math.max(1, Math.round(rect.width) || 100),
      height: Math.max(1, Math.round(rect.height) || 100),
      dpr: window.devicePixelRatio || 1,
      onState: setState,
    });
    instance.current = created;
    keptMounted.current = keepLoaded;
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(() => {
      const r = box.getBoundingClientRect();
      created.resize(Math.max(1, Math.round(r.width)), Math.max(1, Math.round(r.height)), window.devicePixelRatio || 1);
    }) : null;
    observer?.observe(box);
    return () => {
      observer?.disconnect();
      created.dispose();
      instance.current = null;
    };
    // The worker is restarted only when the widget itself, its code or the device switch changes; settings and size are pushed into the running worker.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mountWanted, context?.runtime, context?.host, widget.id, info?.code]);

  useEffect(() => {
    instance.current?.setPaused(!live);
  }, [live]);

  useEffect(() => {
    instance.current?.updateSettings(settings);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsKey]);

  // ---- pointer input (interactive widgets only; moves are coalesced to one per frame) ----
  const pendingMove = useRef<{ x: number; y: number; real: boolean } | null>(null);
  const frame = useRef(0);
  const forward = (phase: "down" | "move" | "up" | "cancel", e: ReactPointerEvent<HTMLDivElement>) => {
    const box = container.current;
    if (!box || !info?.interactive) return;
    const rect = box.getBoundingClientRect();
    const scale = Math.min(2, window.devicePixelRatio || 1);
    const x = (e.clientX - rect.left) * scale;
    const y = (e.clientY - rect.top) * scale;
    if (phase === "move") {
      pendingMove.current = { x, y, real: e.nativeEvent.isTrusted };
      if (!frame.current)
        frame.current = requestAnimationFrame(() => {
          frame.current = 0;
          const m = pendingMove.current;
          pendingMove.current = null;
          if (m) instance.current?.pointer("move", m.x, m.y, m.real);
        });
      return;
    }
    instance.current?.pointer(phase, x, y, e.nativeEvent.isTrusted);
  };

  const reason: PluginWidgetUnavailable | null = !context ? "unsupported" : context.enabled === false ? "off" : !info ? "unsupported" : (info.unavailable ?? (info.code ? null : "unsupported"));
  if (reason) return <PlaceholderContent label={`${label}: ${texts.unavailable[reason]}`} />;

  return (
    <div
      className="ms-content ms-plugin-widget"
      style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden" }}
      onPointerDown={(e) => forward("down", e)}
      onPointerMove={(e) => forward("move", e)}
      onPointerUp={(e) => forward("up", e)}
      onPointerCancel={(e) => forward("cancel", e)}
    >
      <div ref={container} data-ms-plugin-canvas style={{ position: "absolute", inset: 0 }} />
      {state.kind === "stopped" && (
        <div className="ms-plugin-widget-stopped" style={{ position: "absolute", inset: 0, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 6, background: "rgba(0,0,0,0.55)", color: "#fff", fontSize: 12, textAlign: "center", padding: 6 }}>
          <span>{texts.stopped[state.reason]}</span>
          {state.reason !== "tooMany" && (
            <button
              type="button"
              onPointerDown={(e) => e.stopPropagation()}
              onPointerUp={(e) => e.stopPropagation()}
              onClick={() => instance.current?.restart()}
            >
              {texts.restart}
            </button>
          )}
        </div>
      )}
    </div>
  );
}

const EMPTY: Record<string, unknown> = {};
