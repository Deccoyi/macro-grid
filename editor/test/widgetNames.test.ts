import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import type { Widget } from "@macro/renderer";
import { defaultWidgetName, ensureWidgetNames, nameForPage, uniqueWidgetName } from "../src/state/widgetNames";

// Run from the editor folder (npm test). The same table is read by the server tests (WidgetNamesTests).
const cases = JSON.parse(readFileSync(resolve(process.cwd(), "../tests/shared/widget-name-cases.json"), "utf8")) as {
  name: string;
  widgets: { type: string; name?: string }[];
  expected: string[];
}[];

const widget = (type: string, name?: string): Widget => ({ id: Math.random().toString(36), type, x: 0, y: 0, w: 1, h: 1, name, style: {}, actions: {} }) as Widget;

describe("widget names (shared case table)", () => {
  it.each(cases)("$name", ({ widgets, expected }) => {
    const list = widgets.map((w) => widget(w.type, w.name));
    ensureWidgetNames(list);
    expect(list.map((w) => w.name)).toEqual(expected);
  });
});

describe("names for widgets joining a page", () => {
  it("keeps a free name and suffixes a taken one (also within the batch)", () => {
    const page = [widget("button", "Play")];
    const incoming = [widget("button", "Stop"), widget("button", "Play"), widget("button", "play")];
    nameForPage(incoming, page);
    expect(incoming.map((w) => w.name)).toEqual(["Stop", "Play_2", "play_3"]);
  });

  it("gives an unnamed widget a default", () => {
    const incoming = [widget("label")];
    nameForPage(incoming, [widget("label", "Label_1")]);
    expect(incoming[0]!.name).toBe("Label_2");
  });

  it("helpers", () => {
    expect(defaultWidgetName("button", ["button_1"])).toBe("Button_2");
    expect(uniqueWidgetName("Go", ["GO"])).toBe("Go_2");
  });
});
