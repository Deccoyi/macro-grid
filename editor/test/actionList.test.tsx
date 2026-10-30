import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { ActionBinding } from "@macro/renderer";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { ActionList } from "../src/panels/ActionList";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

const steps: ActionBinding[] = [
  { type: "gone.action", settings: {} },
  { type: "other.gone", settings: {} },
];

function draw(bindings: ActionBinding[], onChange = vi.fn()) {
  render(
    <PreferencesProvider>
      <ActionList bindings={bindings} actions={[]} pages={[]} profiles={[]} variableCatalog={[]} onChange={onChange} />
    </PreferencesProvider>,
  );
  return onChange;
}

describe("ActionList", () => {
  it("shows a note for an empty list", () => {
    draw([]);
    expect(screen.getByText(/No action bound/)).toBeTruthy();
  });

  it("lists the steps of a plain binding list and removes one", () => {
    const onChange = draw(steps);
    expect(screen.getByText("gone.action")).toBeTruthy();
    expect(screen.getByText("other.gone")).toBeTruthy();

    fireEvent.click(screen.getAllByTitle("Remove")[0]!);
    expect(onChange).toHaveBeenCalledWith([steps[1]]);
  });

  it("moves a step down", () => {
    const onChange = draw(steps);
    fireEvent.click(screen.getAllByTitle("Move down")[0]!);
    expect(onChange).toHaveBeenCalledWith([steps[1], steps[0]]);
  });
});
