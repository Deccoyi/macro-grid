import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { Profile } from "@macro/renderer";

const mocks = vi.hoisted(() => ({
  calls: [] as string[],
  choice: "rename" as string | null,
  restorePointFails: false,
  saved: [] as Profile[],
}));

vi.mock("../src/i18n/I18nContext", () => ({ useT: () => ({ t: (key: string) => key }) }));
vi.mock("../src/dialogs/dialogStore", () => ({
  alertAsync: () => { mocks.calls.push("alert"); return Promise.resolve(); },
  choiceAsync: () => Promise.resolve(mocks.choice),
  confirmAsync: () => Promise.resolve(true),
}));
vi.mock("../src/api/client", () => ({
  api: {
    listProfiles: () => Promise.resolve([{ id: "old", name: "Home" }]),
    getProfile: (id: string) => Promise.resolve({ id, name: "Home", pages: [{ id: "p", name: "P", widgets: [] }] }),
    createProfile: () => { mocks.calls.push("create"); return Promise.resolve({ id: "new" }); },
    saveProfile: (p: Profile) => { mocks.calls.push(`save:${p.id}`); mocks.saved.push(p); return Promise.resolve(); },
    createRestorePoint: () => {
      mocks.calls.push("restorePoint");
      return mocks.restorePointFails ? Promise.reject(new Error("no")) : Promise.resolve();
    },
  },
}));

import { useProfileDocument } from "../src/state/useProfileDocument";

const shared = { id: "shared", name: "Home", pages: [{ id: "p", name: "P", widgets: [] }] } as unknown as Profile;

async function ready() {
  const hook = renderHook(() => useProfileDocument());
  await waitFor(() => expect(hook.result.current.profile).not.toBeNull());
  mocks.calls.length = 0;
  return hook;
}

describe("import of a shared profile whose name exists", () => {
  beforeEach(() => {
    mocks.calls.length = 0;
    mocks.saved.length = 0;
    mocks.choice = "rename";
    mocks.restorePointFails = false;
  });

  it("Rename saves under a new id and never touches the existing profile", async () => {
    const { result } = await ready();
    await act(() => result.current.importProfileFromJson(shared));

    expect(mocks.calls).toEqual(["create", "save:new"]);
    expect(mocks.saved[0]!.name).toBe("Home_1");
  });

  it("Overwrite writes the import restore point before it saves over the existing profile", async () => {
    mocks.choice = "overwrite";
    const { result } = await ready();
    await act(() => result.current.importProfileFromJson(shared));

    expect(mocks.calls).toEqual(["restorePoint", "save:old"]);
  });

  it("Overwrite is refused when the restore point cannot be made", async () => {
    mocks.choice = "overwrite";
    mocks.restorePointFails = true;
    const { result } = await ready();
    await act(() => result.current.importProfileFromJson(shared));

    expect(mocks.calls).toEqual(["restorePoint", "alert"]);
  });
});
