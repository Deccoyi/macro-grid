import { useEffect, useMemo, useRef, useState } from "react";
import { PluginWidgetRuntime, type PluginWidgetContextValue, type PluginWidgetRuntimeInfo, type PluginWidgetTexts, type Widget } from "@macro/renderer";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { usePluginWidgets } from "../state/usePluginWidgets";
import { EditorPluginWidgetHost } from "./EditorPluginWidgetHost";

/**
 * Everything the canvas needs to show plugin widgets running: the runtime, the editor host, the words for the placeholders, and a function that gives a
 * placed widget its `props.runtime` (the server adds that to layouts it sends to devices; the editor asks for it per plugin widget).
 */
export function usePluginWidgetPreview(widgets: Widget[], variables: Record<string, unknown>) {
  const { t, lang } = useT();
  const available = usePluginWidgets();
  const runtime = useMemo(() => new PluginWidgetRuntime(), []);
  const host = useMemo(() => new EditorPluginWidgetHost(lang, (id, message) => console.warn("Plugin widget", id, message)), [lang]);
  useEffect(() => () => runtime.dispose(), [runtime]);

  const [runtimes, setRuntimes] = useState<Record<string, PluginWidgetRuntimeInfo>>({});
  const asked = useRef(new Set<string>());
  const wanted = useMemo(
    () => [...new Set(widgets.filter((w) => w.type === "plugin-widget").map((w) => `${w.props?.plugin}\n${w.props?.widget}`))],
    [widgets],
  );
  // A changed list of plugin widgets (a plugin approved, reloaded, removed) makes the earlier answers stale.
  useEffect(() => {
    asked.current.clear();
    setRuntimes({});
  }, [available]);
  useEffect(() => {
    for (const key of wanted) {
      if (asked.current.has(key)) continue;
      asked.current.add(key);
      const [plugin, widget] = key.split("\n") as [string, string];
      api
        .pluginWidgetRuntime(plugin, widget)
        .then((info) => setRuntimes((r) => ({ ...r, [key]: info })))
        .catch(() => setRuntimes((r) => ({ ...r, [key]: { unavailable: "missing" } })));
    }
  }, [wanted, available]);

  useEffect(() => host.setVariables(variables), [host, variables]);

  const texts = useMemo<PluginWidgetTexts>(() => {
    const k = (key: string) => t(("pw." + key) as DictKey);
    return {
      widget: k("plugin"),
      restart: k("restart"),
      unavailable: {
        missing: k("unavailable.missing"), disabled: k("unavailable.disabled"), needsApproval: k("unavailable.needsApproval"),
        incompatible: k("unavailable.incompatible"), invalid: k("unavailable.invalid"), noWidget: k("unavailable.noWidget"),
        unsupported: k("unavailable.unsupported"), off: k("unavailable.off"),
      },
      stopped: {
        frozen: k("stopped.frozen"), startTimeout: k("stopped.startTimeout"), tooBusy: k("stopped.tooBusy"),
        tooMany: k("stopped.tooMany"), crashed: k("stopped.crashed"), failed: k("stopped.failed"),
      },
    };
  }, [t]);

  const context = useMemo<PluginWidgetContextValue>(() => ({ runtime, host, texts }), [runtime, host, texts]);

  /** The widget as the renderer draws it: with `props.runtime`, and the host told which plugin widget it is. */
  const withRuntime = (widget: Widget): Widget => {
    if (widget.type !== "plugin-widget") return widget;
    const key = `${widget.props?.plugin}\n${widget.props?.widget}`;
    const info = available.find((a) => a.plugin === widget.props?.plugin && a.widget === widget.props?.widget);
    const settings = (widget.props?.settings as Record<string, unknown> | undefined) ?? {};
    if (info) host.register(widget.id, { info, settings });
    // Until the server answers the widget waits; one whose plugin is not in the list at all says it is missing.
    const runtimeInfo: PluginWidgetRuntimeInfo | undefined = runtimes[key] ?? (info ? undefined : { unavailable: "missing" });
    return { ...widget, props: { ...widget.props, ...(runtimeInfo ? { runtime: runtimeInfo } : {}) } };
  };

  return { context, withRuntime };
}
