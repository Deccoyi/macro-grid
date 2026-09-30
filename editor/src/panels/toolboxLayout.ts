/** How the Toolbox lists its widgets: one group per plugin (in the order the plugins were listed), or by category, alphabetically. */
export type ToolboxView = "plugin" | "category";

/** The search box shows only when the Toolbox has more tiles than this (or while something is typed in it). */
export const SEARCH_MIN_TILES = 12;

export interface ToolboxEntry<T> {
  name: string;
  /** The manifest's category of a plugin widget. */
  category?: string | null;
  plugin?: string;
  pluginName?: string;
  builtin: boolean;
  item: T;
}

export interface ToolboxGroup<T> {
  id: string;
  /** Null for the built-in widgets in the plugin view: they are the untitled first group, as they always were. */
  title: string | null;
  plugin: boolean;
  entries: ToolboxEntry<T>[];
}

const byName = <T,>(a: ToolboxEntry<T>, b: ToolboxEntry<T>) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" });

/** Arranges the entries for a view. The built-in widgets always come first; in the category view every group and every widget in it is in alphabetical order. */
export function groupToolbox<T>(entries: ToolboxEntry<T>[], view: ToolboxView, labels: { builtIn: string; other: string }): ToolboxGroup<T>[] {
  const builtIn = entries.filter((e) => e.builtin);
  const plugins = entries.filter((e) => !e.builtin);
  const groups: ToolboxGroup<T>[] = [];

  if (view === "plugin") {
    if (builtIn.length > 0) groups.push({ id: "builtin", title: null, plugin: false, entries: builtIn });
    for (const plugin of [...new Set(plugins.map((e) => e.plugin ?? ""))]) {
      const members = plugins.filter((e) => (e.plugin ?? "") === plugin);
      groups.push({ id: `plugin:${plugin}`, title: members[0]!.pluginName ?? plugin, plugin: true, entries: members });
    }
    return groups;
  }

  if (builtIn.length > 0) groups.push({ id: "builtin", title: labels.builtIn, plugin: false, entries: [...builtIn].sort(byName) });
  const categories = new Map<string, ToolboxEntry<T>[]>();
  for (const entry of plugins) {
    const category = entry.category?.trim() || labels.other;
    categories.set(category, [...(categories.get(category) ?? []), entry]);
  }
  for (const category of [...categories.keys()].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }))) {
    groups.push({ id: `category:${category}`, title: category, plugin: true, entries: categories.get(category)!.sort(byName) });
  }
  return groups;
}
