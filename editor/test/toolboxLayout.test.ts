import { describe, expect, it } from "vitest";
import { groupToolbox, type ToolboxEntry } from "../src/panels/toolboxLayout";

const labels = { builtIn: "Standard", other: "Other" };
const builtin = (name: string): ToolboxEntry<string> => ({ name, builtin: true, item: name });
const plugin = (name: string, pluginName: string, category?: string): ToolboxEntry<string> => ({ name, plugin: pluginName.toLowerCase(), pluginName, category, builtin: false, item: name });

describe("toolbox layout", () => {
  const entries = [builtin("Toggle"), builtin("Button"), plugin("Weather", "Zed", "Weather"), plugin("Gauge", "Alpha", "Gauges"), plugin("Dial", "Alpha", "Gauges"), plugin("Odd", "Beta")];

  it("keeps the built-in widgets first and untitled, and a group per plugin, in the plugin view", () => {
    const groups = groupToolbox(entries, "plugin", labels);

    expect(groups.map((g) => g.title)).toEqual([null, "Zed", "Alpha", "Beta"]);
    expect(groups[0]!.entries.map((e) => e.name)).toEqual(["Toggle", "Button"]);
  });

  it("groups by category, and sorts the groups and the widgets in them alphabetically, in the category view", () => {
    const groups = groupToolbox(entries, "category", labels);

    expect(groups.map((g) => g.title)).toEqual(["Standard", "Gauges", "Other", "Weather"]);
    expect(groups[0]!.entries.map((e) => e.name)).toEqual(["Button", "Toggle"]);
    expect(groups[1]!.entries.map((e) => e.name)).toEqual(["Dial", "Gauge"]);
  });

  it("leaves out a group with nothing in it", () => {
    expect(groupToolbox([builtin("Button")], "plugin", labels)).toHaveLength(1);
    expect(groupToolbox([], "category", labels)).toEqual([]);
  });
});
