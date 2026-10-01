import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import type { PluginWidgetInfo } from "../api/types";

/**
 * The custom widgets plugins offer right now (GET /api/plugin-widgets), for the Toolbox, the Inspector and the canvas preview. Plugins are
 * installed, approved and switched off in a separate window, so the list is fetched again when this window gets focus.
 */
export function usePluginWidgets(): PluginWidgetInfo[] {
  const [list, setList] = useState<PluginWidgetInfo[]>([]);
  const refresh = useCallback(() => {
    api.pluginWidgets()
      .then((next) => setList((prev) => (prev.length === next.length && prev.every((w, i) => w.plugin === next[i]!.plugin && w.widget === next[i]!.widget) ? prev : next)))
      .catch(() => {});
  }, []);

  useEffect(() => {
    refresh();
    window.addEventListener("focus", refresh);
    const timer = setInterval(() => { if (!document.hidden) refresh(); }, 10_000);
    return () => { window.removeEventListener("focus", refresh); clearInterval(timer); };
  }, [refresh]);

  return list;
}
