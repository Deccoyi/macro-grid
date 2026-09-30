import { PLUGIN_WIDGET_LIMITS, type Profile } from "@macro/renderer";
import type { PluginWidgetInfo } from "../api/types";
import type { Diagnostic } from "./types";

/** The source id of the messages made at every save; a new save replaces the old ones. */
export const SAVE_SUMMARY_SOURCE = "save-summary";

/**
 * What the person is told after every save about the plugin widgets in the profile: how many there are and whether a page has more than is
 * recommended. It is information, not a limit: the right number depends on the device that shows the page, so the wording says "recommended".
 * Only the widgets of one page run at the same time (the page that is shown), so the numbers are counted per page.
 */
export function saveSummary(profile: Profile, available: PluginWidgetInfo[]): Diagnostic[] {
  const recommended = PLUGIN_WIDGET_LIMITS.maxLive;
  const recommendedPerPlugin = PLUGIN_WIDGET_LIMITS.maxLiveUnverified;
  const list: Diagnostic[] = [];
  let total = 0;
  let pagesWithWidgets = 0;

  for (const page of profile.pages) {
    const widgets = page.widgets.filter((w) => w.type === "plugin-widget");
    if (widgets.length === 0) continue;
    total += widgets.length;
    pagesWithWidgets++;
    const target = { profileId: profile.id, pageId: page.id };
    const over = widgets.length > recommended;
    list.push({
      id: `page:${page.id}`,
      source: SAVE_SUMMARY_SOURCE,
      severity: "info",
      code: over ? "I202" : "I201",
      messageKey: over ? "diag.save.pageOver" : "diag.save.page",
      messageArgs: [page.name, String(widgets.length), String(recommended)],
      target,
    });

    // A plugin that is not verified is held to fewer widgets at once, for each plugin.
    const perPlugin = new Map<string, { name: string; count: number }>();
    for (const w of widgets) {
      const plugin = typeof w.props?.plugin === "string" ? w.props.plugin : "";
      const info = available.find((a) => a.plugin === plugin && a.widget === w.props?.widget);
      if (!info || info.verified) continue;
      const entry = perPlugin.get(plugin) ?? { name: info.pluginName, count: 0 };
      entry.count++;
      perPlugin.set(plugin, entry);
    }
    for (const [plugin, { name, count }] of perPlugin) {
      if (count <= recommendedPerPlugin) continue;
      list.push({
        id: `page:${page.id}:${plugin}`,
        source: SAVE_SUMMARY_SOURCE,
        severity: "info",
        code: "I203",
        messageKey: "diag.save.pluginOver",
        messageArgs: [page.name, String(count), name, String(recommendedPerPlugin)],
        target,
      });
    }
  }

  if (total > 0) {
    list.unshift({
      id: "total",
      source: SAVE_SUMMARY_SOURCE,
      severity: "info",
      code: "I200",
      messageKey: "diag.save.total",
      messageArgs: [String(total), String(pagesWithWidgets)],
      target: { profileId: profile.id },
    });
  }
  return list;
}
