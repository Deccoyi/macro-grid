import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const setUserVariables = vi.fn(() => Promise.resolve());
const variable = { name: "score", type: "number", initial: null, keep: false, description: "" };

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
    listProfiles: () => Promise.resolve([]),
    getAutostart: () => Promise.resolve({ enabled: false }),
    getUserVariables: () => Promise.resolve({ variables: [variable], limits: { maxCount: 200, maxNameLength: 40, maxDescriptionLength: 200, maxTextLength: 1024 } }),
    setUserVariables: (...args: unknown[]) => (setUserVariables as unknown as (...a: unknown[]) => Promise<void>)(...args),
    userVariableUsage: () => Promise.resolve([]),
    automationUsage: () => Promise.resolve([]),
  },
}));

import { PreferencesWindow } from "../src/windows/PreferencesWindow";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

describe("PreferencesWindow", () => {
  it("shows the delete confirmation of a global variable and deletes it once confirmed", async () => {
    render(<PreferencesProvider><PreferencesWindow /></PreferencesProvider>);

    fireEvent.click((await screen.findAllByText("Global Variable List"))[0]!);
    fireEvent.click(await screen.findByLabelText("Delete variable"));

    fireEvent.click(await screen.findByRole("button", { name: /^(Delete|OK|Yes|Confirm)$/ }));
    await waitFor(() => expect(setUserVariables).toHaveBeenCalledWith([]));
  });
});
