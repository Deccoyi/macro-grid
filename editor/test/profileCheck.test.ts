import { describe, expect, it } from "vitest";
import type { Profile, Widget } from "@macro/renderer";
import type { ActionInfo } from "../src/api/types";
import { checkProfile, templateVariables, type CheckCatalogs } from "../src/diagnostics/profileCheck";

const action = (type: string, extra: Partial<ActionInfo> = {}): ActionInfo => ({ type, displayName: type, category: "x", ...extra });

const widget = (over: Partial<Widget>): Widget => ({ id: "w1", type: "button", name: "Button_1", x: 0, y: 0, w: 1, h: 1, actions: {}, ...over });

const profile = (widgets: Widget[]): Profile =>
  ({ id: "p1", name: "P", pages: [{ id: "pg1", name: "Main", cols: 4, rows: 3, widgets }, { id: "pg2", name: "Two", cols: 4, rows: 3, widgets: [widget({ id: "web1", type: "web", name: "Web_1" })] }] }) as unknown as Profile;

const catalogs = (over: Partial<CheckCatalogs> = {}): CheckCatalogs => ({
  actions: [action("core.page"), action("core.profile"), action("core.web"), action("a.b", { fields: [
    { key: "mode", label: "Mode", kind: "Select", options: [{ value: "x", label: "X" }] },
    { key: "level", label: "Level", kind: "Number", min: 0, max: 10 },
    { key: "text", label: "Text", kind: "Text", allowVariables: true },
  ] })],
  variableNames: new Set(["system.cpu"]), liveVariableNames: new Set(["live.one"]), pluginWidgets: [], profileIds: new Set(["p1", "p2"]), ...over,
});

const run = (widgets: Widget[], c = catalogs()) => checkProfile(profile(widgets), c, (e) => e);

describe("templateVariables", () => {
  it("finds names in tokens and skips escaped braces", () => {
    expect(templateVariables("{a} {{b}} {c|0.#|--}")).toEqual(["a", "c"]);
    expect(templateVariables(undefined)).toEqual([]);
  });
});

describe("checkProfile", () => {
  it("is quiet for a healthy profile", () => {
    expect(run([widget({ text: "{system.cpu} {live.one}", actions: { press: [{ type: "core.page", settings: { mode: "goto", pageId: "pg2" } }] } })])).toEqual([]);
  });

  it("flags an unknown action type with a target", () => {
    const [d] = run([widget({ actions: { press: [{ type: "gone.x", settings: {} }, { type: "core.page", settings: {} }] } })]);
    expect(d).toMatchObject({ code: "E210", severity: "error", id: "E210:w1:press:0", source: "profile-check", target: { widgetId: "w1", event: "press", actionIndex: 0 } });
  });

  it("flags an uninstalled plugin widget", () => {
    const d = run([widget({ type: "plugin-widget", props: { plugin: "p", widget: "w" } })]);
    expect(d.map((x) => x.code)).toEqual(["E211"]);
    expect(run([widget({ type: "plugin-widget", props: { plugin: "p", widget: "w" } })], catalogs({ pluginWidgets: [{ plugin: "p", widget: "w" } as never] }))).toEqual([]);
  });

  it("flags jumps to a page, profile or web widget that is missing", () => {
    const d = run([widget({ actions: { press: [
      { type: "core.page", settings: { mode: "goto", pageId: "nope" } },
      { type: "core.page", settings: { mode: "next", pageId: "nope" } },
      { type: "core.profile", settings: { profileId: "zzz" } },
      { type: "core.web", settings: { widgetId: "w1" } },
      { type: "core.web", settings: { widgetId: "web1" } },
    ] } })]);
    expect(d.map((x) => `${x.code}:${x.target?.actionIndex}`)).toEqual(["E220:0", "E220:2", "E220:3"]);
  });

  it("flags invalid settings of a described action but not empty ones", () => {
    const d = run([widget({ actions: { press: [{ type: "a.b", settings: { mode: "bad", level: 11 } }, { type: "a.b", settings: {} }] } })]);
    expect(d.map((x) => `${x.code}:${x.id.split(":").pop()}`)).toEqual(["W221:mode", "W221:level"]);
  });

  it("flags variables nobody provides, in text, rules and action text", () => {
    const d = run([widget({
      text: "{gone.a}",
      dynamic: { "style.background": { cases: [{ condition: { kind: "and", children: [{ kind: "compare", variable: "gone.b", operator: "==", value: "1" }] }, result: "red" }] } },
      actions: { press: [{ type: "a.b", settings: { text: "x {gone.c}" } }] },
    })]);
    expect(d.map((x) => x.code)).toEqual(["W230", "W230", "W230"]);
    expect(new Set(d.map((x) => x.id)).size).toBe(3);
  });

  it("gives a missing user variable its own code", () => {
    const user = catalogs({ variableNames: new Set(["system.cpu", "user.count"]) });
    expect(run([widget({ text: "{user.count} {user.gone}" })], user).map((x) => x.code)).toEqual(["W231"]);
    expect(run([widget({ text: "{USER.Gone}" })], user).map((x) => x.code)).toEqual(["W231"]);
  });

  it("flags a Set variable action whose variable does not exist", () => {
    const c = catalogs({ actions: [action("core.setVariable")], variableNames: new Set(["user.count"]) });
    const d = run([widget({ actions: { press: [
      { type: "core.setVariable", settings: { variable: "user.count", mode: "add" } },
      { type: "core.setVariable", settings: { variable: "user.gone", mode: "reset" } },
      { type: "core.setVariable", settings: {} },
    ] } })], c);
    expect(d.map((x) => `${x.code}:${x.target?.actionIndex}`)).toEqual(["E220:1"]);
  });
});
