import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { StatusEntry } from "../src/api/types";

let shown: Record<string, boolean> = {};
vi.mock("../src/preferences/PreferencesContext", () => ({ usePreferences: () => ({ statusBarPlugins: shown }) }));
vi.mock("../src/i18n/I18nContext", () => ({ useT: () => ({ t: (k: string) => k, tn: (k: string) => k, lang: "en", setLang: () => {} }) }));
vi.mock("../src/state/statusNotice", () => ({ useStatusNotice: () => null }));
vi.mock("../src/state/pluginUpdates", () => ({ usePluginUpdateCount: () => 0 }));
vi.mock("../src/api/client", () => ({ api: { openToolWindow: () => {}, openPluginSettingsWindow: () => {} } }));

import { StatusBar } from "../src/components/StatusBar";

const entry = (pluginId: string, id: string, text: string): StatusEntry => ({ pluginId, id, text, level: "Ok", updatedAt: "" }) as StatusEntry;

afterEach(cleanup);

describe("StatusBar plugin items", () => {
  const items = [entry("core", "devices", "2 devices"), entry("obs", "conn", "OBS connected")];

  it("hides plugin items by default and always shows the core items", () => {
    shown = {};
    render(<StatusBar items={items} />);
    expect(screen.getByText("2 devices")).toBeTruthy();
    expect(screen.queryByText("OBS connected")).toBeNull();
  });

  it("shows the items of a plugin the person switched on", () => {
    shown = { obs: true };
    render(<StatusBar items={items} />);
    expect(screen.getByText("OBS connected")).toBeTruthy();
  });
});
