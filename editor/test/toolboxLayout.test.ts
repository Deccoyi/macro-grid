import { describe, expect, it } from "vitest";
import { groupToolbox, type ToolboxEntry } from "../src/panels/toolboxLayout";

const builtin = (name: string): ToolboxEntry<string> => ({ name, builtin: true, item: name });
const plugin = (name: string, pluginName: string): ToolboxEntry<string> => ({ name, plugin: pluginName.toLowerCase(), pluginName, builtin: false, item: name });

describe("toolbox layout", () => {
  const entries = [builtin("Toggle"), builtin("Button"), plugin("Weather", "Zed"), plugin("Gauge", "Alpha"), plugin("Dial", "Alpha")];

  it("keeps the built-in widgets first and untitled, and a group per plugin, in the plugin view", () => {
    const groups = groupToolbox(entries, "plugin");

    expect(groups.map((g) => g.title)).toEqual([null, "Zed", "Alpha"]);
    expect(groups[0]!.entries.map((e) => e.name)).toEqual(["Toggle", "Button"]);
    expect(groups[2]!.entries.map((e) => e.name)).toEqual(["Gauge", "Dial"]);
  });

  it("lists everything in one untitled group, A to Z, built-in and plugin widgets mixed, in the alphabetical view", () => {
    const groups = groupToolbox(entries, "alphabetical");

    expect(groups).toHaveLength(1);
    expect(groups[0]!.title).toBeNull();
    expect(groups[0]!.entries.map((e) => e.name)).toEqual(["Button", "Dial", "Gauge", "Toggle", "Weather"]);
  });

  it("leaves out a group with nothing in it", () => {
    expect(groupToolbox([builtin("Button")], "plugin")).toHaveLength(1);
    expect(groupToolbox([], "plugin")).toEqual([]);
    expect(groupToolbox([], "alphabetical")).toEqual([]);
  });
});
