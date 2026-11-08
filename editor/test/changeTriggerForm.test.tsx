import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { AutomationTrigger, VariableInfo } from "../src/api/types";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

vi.mock("../src/panels/VariablePicker", () => ({
  VariablePicker: ({ catalog, onInsert }: { catalog: VariableInfo[]; onInsert: (name: string) => void }) => (
    <div>{catalog.map((v) => <button key={v.name} type="button" onClick={() => onInsert(v.name)}>pick {v.name}</button>)}</div>
  ),
}));

import { PreferencesProvider } from "../src/preferences/PreferencesContext";
import { ChangeTriggerForm } from "../src/windows/ChangeTriggerForm";

const catalog: VariableInfo[] = [
  { name: "obs.live", description: "", example: "", category: "obs", type: "boolean" },
  { name: "obs.scene", description: "", example: "", category: "obs", type: "text" },
];

describe("ChangeTriggerForm", () => {
  it("clears the filters when another variable is picked", () => {
    const calls: Partial<AutomationTrigger>[] = [];
    const trigger: AutomationTrigger = { kind: "variableChange", variable: "obs.live", mode: "any", from: "true", to: null, days: [] };
    render(<PreferencesProvider><ChangeTriggerForm trigger={trigger} catalog={catalog} onChange={(c) => calls.push(c)} /></PreferencesProvider>);

    fireEvent.click(screen.getByText("pick obs.scene"));
    expect(calls).toEqual([{ variable: "obs.scene", mode: "any", from: null, to: null, threshold: null }]);

    calls.length = 0;
    fireEvent.click(screen.getByText("pick obs.live")); // the variable already chosen: nothing changes
    expect(calls).toEqual([]);
  });

  it("shows no filter controls for a new rule", () => {
    const trigger: AutomationTrigger = { kind: "variableChange", variable: "obs.scene", mode: "any", from: null, to: null, days: [] };
    render(<PreferencesProvider><ChangeTriggerForm trigger={trigger} catalog={catalog} onChange={() => {}} /></PreferencesProvider>);
    expect(screen.queryByRole("combobox")).toBeNull();
    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.queryByText("Remove filter")).toBeNull();
  });

  it("keeps an old filter and removes it only on request", () => {
    const calls: Partial<AutomationTrigger>[] = [];
    const trigger: AutomationTrigger = { kind: "variableChange", variable: "obs.scene", mode: "contains", from: null, to: "Game", days: [] };
    render(<PreferencesProvider><ChangeTriggerForm trigger={trigger} catalog={catalog} onChange={(c) => calls.push(c)} /></PreferencesProvider>);
    expect(calls).toEqual([]);
    fireEvent.click(screen.getByText("Remove filter"));
    expect(calls).toEqual([{ mode: "any", from: null, to: null, threshold: null }]);
  });
});
