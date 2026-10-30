import { describe, expect, it, vi } from "vitest";
import { PluginWidgetError } from "@macro/renderer";
import type { PluginWidgetInfo } from "../src/api/types";
import { EditorPluginWidgetHost } from "../src/grid/EditorPluginWidgetHost";

vi.mock("../src/api/client", () => ({
  api: {
    pluginWidgetAsset: vi.fn(async (hash: string) => `code of ${hash}`),
    pluginWidgetRequest: vi.fn(async (_p: string, _w: string, settings: unknown, data: unknown) => ({ data: { settings, data } })),
  },
}));

const info: PluginWidgetInfo = {
  plugin: "gp", pluginName: "Gauge plugin", widget: "gauge", name: "Gauge", size: { w: 2, h: 2 }, fps: 15, interactive: false, options: [], verified: true,
  settings: [{ key: "source", label: "Value", kind: "Variable" }],
};

function setup(settings: Record<string, unknown> = { source: "sys.cpu" }) {
  const host = new EditorPluginWidgetHost("en", vi.fn());
  host.register("w1", { info, settings });
  const seen: Record<string, unknown>[] = [];
  host.listen("w1", { vars: (v) => seen.push(v), event: () => undefined });
  return { host, seen };
}

describe("editor plugin widget host", () => {
  it("previews with mode edit and never lets a widget run an action", async () => {
    const { host } = setup();

    expect(host.mode).toBe("edit");
    await expect(host.run()).rejects.toMatchObject({ code: "editor" });
  });

  it("gives a widget its own plugin's variables and the ones bound in its settings, and nothing else", () => {
    const { host, seen } = setup();
    host.setVariables({ "gp.count": 3, "sys.cpu": 41, "sys.secret": "no", "other.value": 1 });

    host.subscribe("w1", ["gp.count", "sys.cpu", "sys.secret", "other.value"]);

    expect(seen).toEqual([{ "gp.count": 3, "sys.cpu": 41 }]);
  });

  it("pushes only what changed after a new snapshot", () => {
    const { host, seen } = setup();
    host.setVariables({ "gp.count": 1 });
    host.subscribe("w1", ["gp.count"]);

    host.setVariables({ "gp.count": 1 });
    host.setVariables({ "gp.count": 2 });

    expect(seen).toEqual([{ "gp.count": 1 }, { "gp.count": 2 }]);
  });

  it("sends a request to the plugin with the widget's settings and turns an error into a coded error", async () => {
    const { host } = setup();

    await expect(host.request("w1", { op: "x" })).resolves.toEqual({ settings: { source: "sys.cpu" }, data: { op: "x" } });
    await expect(host.request("unknown", 1)).rejects.toBeInstanceOf(PluginWidgetError);
  });

  it("loads a widget script by its asset hash", async () => {
    const { host } = setup();

    await expect(host.loadAsset("asset:abc")).resolves.toBe("code of abc");
  });
});
