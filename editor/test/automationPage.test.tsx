import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const setAutomation = vi.fn(() => Promise.resolve());
const rule = {
  id: "r1", name: "Morning", enabled: true, cooldownSeconds: 0, actions: [],
  trigger: { kind: "time", time: "07:30", days: [1, 2], condition: null, deviceId: null },
};

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
    getAutomation: () => Promise.resolve({
      paused: false, rules: [rule], status: { r1: { running: false, lastResult: "none", runs: 0 } },
      limits: { maxRules: 50, maxNameLength: 60, maxSteps: 20, maxCooldownSeconds: 86400, maxComparisons: 20 },
    }),
    setAutomation: (...args: unknown[]) => (setAutomation as unknown as (...a: unknown[]) => Promise<void>)(...args),
    listDevices: () => Promise.resolve([]),
    listActions: () => Promise.resolve([]),
    listProfiles: () => Promise.resolve([]),
    variableCatalog: () => Promise.resolve([]),
    runAutomationRule: () => Promise.resolve(),
  },
}));

import { AutomationPage } from "../src/windows/AutomationPage";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

describe("AutomationPage", () => {
  it("lists a rule with when it starts and sends the whole list when the pause is ticked", async () => {
    render(<PreferencesProvider><AutomationPage /></PreferencesProvider>);

    expect(await screen.findByText("Morning")).toBeTruthy();
    expect(screen.getByText(/At 07:30, Mon, Tue/)).toBeTruthy();
    expect(screen.getByText(/1 of 50 rules/)).toBeTruthy();

    fireEvent.click(screen.getByLabelText("Pause all rules"));
    await waitFor(() => expect(setAutomation).toHaveBeenCalledWith([rule], true));
  });

  it("keeps an unfinished rule on the page instead of sending it", async () => {
    setAutomation.mockClear();
    render(<PreferencesProvider><AutomationPage /></PreferencesProvider>);
    await screen.findByText("Morning");

    fireEvent.click(screen.getByText("When a value turns true"));

    expect(await screen.findByText("Choose a value to watch to save the rule.")).toBeTruthy();
    expect(setAutomation).not.toHaveBeenCalled();
  });
});
