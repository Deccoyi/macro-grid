import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ deleteFails: false, notices: [] as string[], alerts: [] as string[] }));

vi.mock("../src/i18n/I18nContext", () => ({ useT: () => ({ t: (key: string) => key }) }));
vi.mock("../src/dialogs/dialogStore", () => ({
  alertAsync: (message: string) => { mocks.alerts.push(message); return Promise.resolve(); },
  choiceAsync: () => Promise.resolve(null),
  confirmAsync: () => Promise.resolve(true),
}));
vi.mock("../src/state/statusNotice", () => ({ showStatusNotice: (key: string) => { mocks.notices.push(key); } }));
vi.mock("../src/api/client", () => ({
  api: {
    listProfiles: () => Promise.resolve([{ id: "a", name: "A" }, { id: "b", name: "B" }]),
    getProfile: (id: string) => Promise.resolve({ id, name: id, pages: [{ id: "p", name: "P", widgets: [] }] }),
    deleteProfile: () => (mocks.deleteFails ? Promise.reject(new Error("A restore point could not be made")) : Promise.resolve()),
  },
}));

import { useProfileDocument } from "../src/state/useProfileDocument";

describe("deleting a profile", () => {
  beforeEach(() => {
    mocks.deleteFails = false;
    mocks.notices.length = 0;
    mocks.alerts.length = 0;
  });

  it("shows a dialog when the server refuses because no restore point could be saved", async () => {
    mocks.deleteFails = true;
    const hook = renderHook(() => useProfileDocument());
    await waitFor(() => expect(hook.result.current.profile).not.toBeNull());

    let deleted = true;
    await act(async () => { deleted = await hook.result.current.deleteProfile("b"); });

    expect(deleted).toBe(false);
    expect(mocks.alerts).toEqual(["profile.deleteFailed"]);
    expect(mocks.notices).toEqual([]);
  });

  it("reports success without the refusal message", async () => {
    const hook = renderHook(() => useProfileDocument());
    await waitFor(() => expect(hook.result.current.profile).not.toBeNull());

    let deleted = false;
    await act(async () => { deleted = await hook.result.current.deleteProfile("b"); });

    expect(deleted).toBe(true);
    expect(mocks.alerts).toEqual([]);
  });
});
