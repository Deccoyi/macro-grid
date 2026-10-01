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
    { key: "tags", label: "Tags", kind: "MultiSelect", options: [{ value: "a", label: "A" }, { value: "b", label: "B" }] },
    { key: "wait", label: "Wait", kind: "Duration", min: 100, max: 5000 },
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

  it("holds a duration to its range in milliseconds", () => {
    const d = run([widget({ actions: { press: [{ type: "a.b", settings: { wait: 50 } }, { type: "a.b", settings: { wait: 6000 } }, { type: "a.b", settings: { wait: 2000 } }] } })]);
    expect(d.map((x) => `${x.code}:${x.id.split(":").pop()}`)).toEqual(["W221:wait", "W221:wait"]);
  });

  it("flags a multi-select value that is not an option", () => {
    const d = run([widget({ actions: { press: [{ type: "a.b", settings: { tags: ["a", "z"] } }, { type: "a.b", settings: { tags: ["a", "b"] } }] } })]);
    expect(d.map((x) => `${x.code}:${x.id.split(":").pop()}`)).toEqual(["W221:tags"]);
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

  it("warns when a This button variable is used in an action or a slider variable, but not in the button's own text or rules", () => {
    const self = catalogs({ variableNames: new Set(["system.cpu", "self.busy"]) });
    expect(run([widget({ text: "{self.busy}", dynamic: { "style.background": { cases: [{ condition: { kind: "compare", variable: "self.busy", operator: "==", value: "true" }, result: "red" }] } } })], self)).toEqual([]);
    expect(run([widget({ actions: { press: [{ type: "a.b", settings: { text: "x {self.busy}" } }] } })], self).map((x) => x.code)).toEqual(["W232"]);
    expect(run([widget({ type: "slider", props: { valueVariable: "self.busy" } })], self).map((x) => x.code)).toEqual(["W232"]);
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

  describe("logic steps", () => {
    const c = catalogs({ actions: [action("core.if"), action("core.else"), action("core.endIf"), action("core.stop"), action("core.setVariable")], variableNames: new Set(["user.on", "self.busy"]) });
    const step = (type: string, settings: Record<string, unknown> = {}) => ({ type, settings });
    const cond = (variable: string) => ({ when: "condition", condition: { kind: "compare", variable, operator: "==", value: "1" } });
    const codes = (steps: ReturnType<typeof step>[]) => run([widget({ actions: { press: steps } })], c).map((x) => x.code);

    it("accepts a well formed block", () => {
      expect(codes([step("core.if", cond("user.on")), step("core.setVariable", { variable: "user.on" }), step("core.else"), step("core.endIf"), step("core.stop")])).toEqual([]);
      expect(codes([step("core.if", { when: "previousFailed" }), step("core.endIf")])).toEqual([]);
    });

    it("flags a list that is not well formed once, on the first logic row", () => {
      const found = run([widget({ actions: { press: [step("core.stop"), step("core.if", { when: "previousOk" }), step("core.else"), step("core.else")] } })], c);
      expect(found.map((x) => `${x.code}:${x.target?.actionIndex}`)).toEqual(["W233:1"]);
    });

    it("flags an If that tests a condition and has none", () => {
      expect(codes([step("core.if", { when: "condition" }), step("core.endIf")])).toEqual(["W234"]);
      expect(codes([step("core.if", { when: "condition", condition: { kind: "compare", variable: "", operator: "==" } }), step("core.endIf")])).toEqual(["W234"]);
    });

    it("checks the variables of the condition", () => {
      expect(codes([step("core.if", cond("user.gone")), step("core.endIf")])).toEqual(["W231"]);
      expect(codes([step("core.if", cond("some.plugin")), step("core.endIf")])).toEqual(["W230"]);
      expect(codes([step("core.if", cond("self.busy")), step("core.endIf")])).toEqual(["W232"]);
    });
  });
});
