import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { InspectBackupResult } from "../src/api/types";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { RestoreContent, restoreItemId, restoreWarningText } from "../src/dialogs/RestoreContent";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

type Inspected = NonNullable<InspectBackupResult["result"]>;

const inspected: Inspected = {
  id: "abc",
  manifest: { createdAt: "2026-09-30T10:00:00Z", serverVersion: "1.2.3", reason: "manual" },
  items: [
    { kind: "profile", key: "p1", name: "Streaming", state: "different", names: ["Main"], herePages: 1, hereWidgets: 2, backupPages: 2, backupWidgets: 5 },
    { kind: "profile", key: "p2", name: "Gaming", state: "new", names: [], backupPages: 1, backupWidgets: 3 },
    { kind: "preferences", key: "", name: "", state: "same", names: [] },
  ],
  warnings: [{ code: "missingFile", args: ["Main", "Button", "C:\\sounds\\a.wav"] }, { code: "somethingNew", args: [] }],
};

describe("RestoreContent", () => {
  it("lists only what differs, ticks it, and lets a tick be removed", () => {
    const ticked = new Set(inspected.items.filter((i) => i.state !== "same").map(restoreItemId));
    render(<PreferencesProvider><RestoreContent inspected={inspected} ticked={ticked} /></PreferencesProvider>);

    expect(screen.getByText(/Profile: Streaming/)).toBeTruthy();
    expect(screen.getByText(/Profile: Gaming/)).toBeTruthy();
    expect(screen.queryByText(/Preferences/)).toBeNull();

    const boxes = screen.getAllByRole("checkbox");
    expect(boxes).toHaveLength(2);
    fireEvent.click(boxes[0]!);
    expect(ticked.has("profile:p1")).toBe(false);
    expect(ticked.has("profile:p2")).toBe(true);
  });

  it("words the automation rules item and says they come back switched off", () => {
    const withRules: Inspected = { ...inspected, items: [{ kind: "automation", key: "", name: "", state: "different", names: [], added: 1, changed: 2 }] };
    render(<PreferencesProvider><RestoreContent inspected={withRules} ticked={new Set()} /></PreferencesProvider>);

    expect(screen.getByText(/Automation rules/)).toBeTruthy();
    expect(screen.getByText(/1 added, 2 changed. They come back switched off./)).toBeTruthy();
  });

  it("shows the fixed passwords line, a worded warning and an unknown code as it is", () => {
    render(<PreferencesProvider><RestoreContent inspected={inspected} ticked={new Set()} /></PreferencesProvider>);

    expect(screen.getByText(/Passwords and phone pairings are not in a backup/)).toBeTruthy();
    expect(screen.getByText(/C:\\sounds\\a.wav/)).toBeTruthy();
    expect(screen.getByText("somethingNew")).toBeTruthy();
  });

  it("words a warning through the translate function", () => {
    const text = restoreWarningText((key, ...args) => `${key}|${args.join(",")}`, { code: "missingPlugin", args: ["OBS"] });
    expect(text).toBe("restore.warn.missingPlugin|OBS");
  });
});
