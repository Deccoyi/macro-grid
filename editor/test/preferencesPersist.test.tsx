import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AppPreferences } from "../src/api/types";

const saved: AppPreferences[] = [];
const server = vi.hoisted(() => ({ prefs: { dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" } as unknown as AppPreferences }));

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve(server.prefs),
    savePreferences: (p: AppPreferences) => {
      saved.push(p);
      return Promise.resolve();
    },
  },
}));

import { PreferencesProvider, usePreferences } from "../src/preferences/PreferencesContext";

const wrapper = ({ children }: { children: ReactNode }) => <PreferencesProvider>{children}</PreferencesProvider>;

describe("preferences", () => {
  beforeEach(() => {
    saved.length = 0;
  });

  it("keeps a closed hint when another preference is saved in the same tick (the second change must not start from the old copy)", async () => {
    const { result } = renderHook(() => usePreferences(), { wrapper });
    await waitFor(() => expect(result.current.loaded).toBe(true));

    act(() => {
      result.current.dismissNotice("web.experimental");
      result.current.setDockLayoutJson("{\"layout\":1}");
    });

    const last = saved[saved.length - 1]!;
    expect(last.dismissedNotices).toEqual({ "web.experimental": true });
    expect(last.dockLayoutJson).toBe("{\"layout\":1}");
    expect(result.current.dismissedNotices["web.experimental"]).toBe(true);
  });
});
