import { act, fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { OptionsQuery, OptionsResult, SettingField } from "../src/api/types";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { SchemaForm, clearDependents } from "../src/panels/actionForms/SchemaForm";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

const fields: SettingField[] = [
  { key: "source", label: "Source", kind: "Text" },
  { key: "panel", label: "Panel", kind: "Text", dependsOn: ["source"] },
  { key: "fileName", label: "Sound", kind: "Text", dependsOn: ["source", "panel"] },
  { key: "volume", label: "Volume", kind: "Text" },
];
const all = { source: "a", panel: "p", fileName: "f", "fileName＠label": "F", "panel＠label": "P", volume: "7" };

describe("clearDependents", () => {
  it("source clears panel and fileName with their labels, nothing else", () => {
    expect(clearDependents({ ...all, source: "b" }, fields, ["source"])).toEqual({ source: "b", volume: "7" });
  });
  it("panel clears only fileName", () => {
    expect(clearDependents({ ...all, panel: "q" }, fields, ["panel"])).toEqual({ source: "a", panel: "q", "panel＠label": "P", volume: "7" });
  });
  it("is transitive through a field that depends only on a cleared one", () => {
    const chain: SettingField[] = [
      { key: "a", label: "A", kind: "Text" },
      { key: "b", label: "B", kind: "Text", dependsOn: ["a"] },
      { key: "c", label: "C", kind: "Text", dependsOn: ["b"] },
    ];
    expect(clearDependents({ a: "2", b: "x", c: "y" }, chain, ["a"])).toEqual({ a: "2" });
  });
  it("leaves unrelated fields alone", () => {
    expect(clearDependents({ ...all, volume: "9" }, fields, ["volume"])).toEqual({ ...all, volume: "9" });
  });
});

describe("SchemaForm", () => {
  it("clears dependents when a parent field is edited", () => {
    const onChange = vi.fn();
    render(
      <PreferencesProvider>
        <SchemaForm fields={fields} values={all} onChange={onChange} fetchOptions={() => Promise.resolve({ options: [] })} />
      </PreferencesProvider>,
    );
    fireEvent.change(screen.getAllByRole("textbox")[0]!, { target: { value: "b" } });
    expect(onChange).toHaveBeenCalledWith({ source: "b", volume: "7" });
  });
});

describe("searchable field refetch", () => {
  it("shows the new parent's list at once with the typed query cleared", async () => {
    const searchable: SettingField[] = [
      { key: "source", label: "Source", kind: "Text" },
      { key: "sound", label: "Sound", kind: "Select", optionsSource: "sounds", searchable: true, dependsOn: ["source"] },
    ];
    const calls: { values: Record<string, unknown>; query?: OptionsQuery; resolve: (r: OptionsResult) => void }[] = [];
    const fetchOptions = (_s: string, values: Record<string, unknown>, query?: OptionsQuery) =>
      new Promise<OptionsResult>((resolve) => { calls.push({ values, query, resolve }); });
    const ui = (source: string) => (
      <PreferencesProvider>
        <SchemaForm fields={searchable} values={{ source }} onChange={() => {}} fetchOptions={fetchOptions} />
      </PreferencesProvider>
    );
    const { rerender } = render(ui("a"));
    const box = () => screen.getByRole("combobox") as HTMLInputElement;
    fireEvent.focus(box());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [{ value: "1", label: "ONE" }] }); });
    fireEvent.change(box(), { target: { value: "on" } });
    expect(box().value).toBe("on");

    rerender(ui("b"));
    const last = calls[calls.length - 1]!;
    expect(last.values.source).toBe("b");
    expect(last.query).toMatchObject({ q: "", offset: 0 });
    expect(screen.queryAllByRole("option")).toHaveLength(0);
    await act(async () => { last.resolve({ options: [{ value: "2", label: "TWO" }] }); });
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["TWO"]);
    expect(box().value).toBe("");
  });
});
