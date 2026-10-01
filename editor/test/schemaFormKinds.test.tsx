import { fireEvent, render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { SettingField } from "../src/api/types";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { SchemaForm } from "../src/panels/actionForms/SchemaForm";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

function draw(fields: SettingField[], values: Record<string, unknown>) {
  const onChange = vi.fn();
  const view = render(
    <PreferencesProvider>
      <SchemaForm fields={fields} values={values} onChange={onChange} fetchOptions={() => Promise.resolve({ options: [], error: null })} />
    </PreferencesProvider>,
  );
  return { onChange, ...view };
}

describe("Hotkey field", () => {
  const field: SettingField = { key: "k", label: "Shortcut", kind: "Hotkey" };

  it("shows the saved combo and saves a pressed one", () => {
    const { onChange, container } = draw([field], { k: "ctrl+s" });
    const input = container.querySelector("input")!;
    expect(input.value).toBe("ctrl+s");
    fireEvent.focus(input);
    fireEvent.keyDown(input, { code: "ControlLeft" });
    fireEvent.keyDown(input, { code: "ShiftLeft" });
    fireEvent.keyDown(input, { code: "KeyS" });
    expect(onChange).toHaveBeenCalledWith({ k: "ctrl+shift+s" });
  });
});
