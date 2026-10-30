import { describe, expect, it } from "vitest";
import type { Profile } from "@macro/renderer";
import type { PluginWidgetInfo } from "../src/api/types";
import { saveSummary } from "../src/diagnostics/saveSummary";

const info = (plugin: string, verified: boolean): PluginWidgetInfo => ({ plugin, pluginName: plugin.toUpperCase(), widget: "w", name: "W", size: { w: 1, h: 1 }, fps: 15, interactive: false, options: [], verified });

function profileWith(pages: { name: string; plugins: string[] }[]): Profile {
  return {
    id: "p1",
    name: "P",
    pages: pages.map((page, i) => ({
      id: `pg${i}`, name: page.name, cols: 4, rows: 4, gap: 0, padding: 0,
      widgets: page.plugins.map((plugin, j) => ({ id: `w${i}-${j}`, type: "plugin-widget", x: 0, y: 0, w: 1, h: 1, props: { plugin, widget: "w" } })),
    })),
  } as unknown as Profile;
}

describe("save summary", () => {
  it("says nothing for a profile without plugin widgets", () => {
    expect(saveSummary(profileWith([{ name: "A", plugins: [] }]), [])).toEqual([]);
  });

  it("gives the total and one line per page, as information", () => {
    const list = saveSummary(profileWith([{ name: "A", plugins: ["a", "a"] }, { name: "B", plugins: ["a"] }]), [info("a", true)]);

    expect(list.map((d) => d.code)).toEqual(["I200", "I201", "I201"]);
    expect(list.every((d) => d.severity === "info")).toBe(true);
    expect(list[0]!.messageArgs).toEqual(["3", "2"]);
  });

  it("marks a page with more widgets than recommended", () => {
    const list = saveSummary(profileWith([{ name: "A", plugins: Array(9).fill("a") }]), [info("a", true)]);

    expect(list.map((d) => d.code)).toEqual(["I200", "I202"]);
  });

  it("marks more widgets of one plugin that is not verified than recommended, for each plugin on its own", () => {
    const list = saveSummary(profileWith([{ name: "A", plugins: ["a", "a", "a", "b", "b"] }]), [info("a", false), info("b", false)]);

    const over = list.filter((d) => d.code === "I203");
    expect(over).toHaveLength(1);
    expect(over[0]!.messageArgs).toEqual(["A", "3", "A", "2"]);
  });
});
