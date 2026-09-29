import { describe, expect, it } from "vitest";
import type { Profile, Widget } from "@macro/renderer";
import { clearWebUrls, collectWebHosts } from "../src/state/webUrls";

const widget = (over: Partial<Widget>): Widget => ({ id: "w", type: "button", x: 0, y: 0, w: 1, h: 1, actions: {}, ...over });

function profile(): Profile {
  return {
    id: "p",
    name: "P",
    pages: [
      {
        id: "a",
        name: "A",
        cols: 4,
        rows: 3,
        widgets: [
          widget({ id: "chat", type: "web", props: { url: "https://chat.example.org/room?token=secret" } }),
          widget({
            id: "b",
            actions: {
              press: [
                { type: "core.web", settings: { widgetId: "chat", mode: "set", url: "https://stats.example.org/x" } },
                { type: "core.web", settings: { widgetId: "chat", mode: "reset" } },
                { type: "core.hotkey", settings: { url: "https://not-a-web-action.example.org/" } },
              ],
            },
          }),
        ],
      },
    ],
  } as unknown as Profile;
}

describe("web addresses in a profile", () => {
  it("lists host names only, from widgets and from Change web page buttons", () => {
    const hosts = collectWebHosts(profile());

    expect(hosts).toEqual(["chat.example.org", "stats.example.org"]);
    expect(hosts.join(" ")).not.toContain("secret");
  });

  it("clears every address and leaves the widgets and buttons", () => {
    const p = profile();

    clearWebUrls(p);

    expect(collectWebHosts(p)).toEqual([]);
    expect(p.pages[0]!.widgets).toHaveLength(2);
    expect(p.pages[0]!.widgets[1]!.actions.press).toHaveLength(3);
  });
});
