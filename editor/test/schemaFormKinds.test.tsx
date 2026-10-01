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

describe("Duration field", () => {
  const field: SettingField = { key: "d", label: "Wait", kind: "Duration", min: 0, max: 5000 };

  it("saves milliseconds for the unit typed in, and keeps the stored value when the unit changes", () => {
    const { onChange, container } = draw([field], { d: 1000 });
    const number = container.querySelector("input")!;
    const unit = container.querySelector("select")!;
    expect((unit as HTMLSelectElement).value).toBe("s");
    fireEvent.change(number, { target: { value: "2" } });
    expect(onChange).toHaveBeenLastCalledWith({ d: 2000 });
    fireEvent.change(unit, { target: { value: "ms" } });
    expect(onChange).toHaveBeenCalledTimes(1);
    expect(number.value).toBe("1000");
  });

  it("clamps to the maximum when the box loses focus", () => {
    const { onChange, container } = draw([field], { d: 6000 });
    fireEvent.blur(container.querySelector("input")!);
    expect(onChange).toHaveBeenCalledWith({ d: 5000 });
  });
});
