/** How the Toolbox lists its widgets: one group per plugin (in the order the plugins were listed), or as one flat list with no groups, A to Z. */
export type ToolboxView = "plugin" | "alphabetical";

/** The search box shows only when the Toolbox has more tiles than this (or while something is typed in it). */
export const SEARCH_MIN_TILES = 12;

export interface ToolboxEntry<T> {
  name: string;
  plugin?: string;
  pluginName?: string;
  builtin: boolean;
  item: T;
}

export interface ToolboxGroup<T> {
  id: string;
  /** Null for the built-in widgets in the plugin view and for the flat list: no header, nothing to fold. */
  title: string | null;
  plugin: boolean;
  entries: ToolboxEntry<T>[];
}

const byName = <T,>(a: ToolboxEntry<T>, b: ToolboxEntry<T>) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" });

/** Arranges the entries for a view. The built-in widgets come first in the plugin view; the alphabetical view is one flat list of everything. */
export function groupToolbox<T>(entries: ToolboxEntry<T>[], view: ToolboxView): ToolboxGroup<T>[] {
  if (view === "alphabetical") {
    return entries.length === 0 ? [] : [{ id: "all", title: null, plugin: false, entries: [...entries].sort(byName) }];
  }

  const builtIn = entries.filter((e) => e.builtin);
  const plugins = entries.filter((e) => !e.builtin);
  const groups: ToolboxGroup<T>[] = [];
  if (builtIn.length > 0) groups.push({ id: "builtin", title: null, plugin: false, entries: builtIn });
  for (const plugin of [...new Set(plugins.map((e) => e.plugin ?? ""))]) {
    const members = plugins.filter((e) => (e.plugin ?? "") === plugin);
    groups.push({ id: `plugin:${plugin}`, title: members[0]!.pluginName ?? plugin, plugin: true, entries: members });
  }
  return groups;
}
