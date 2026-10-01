import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

import { DismissibleNote } from "../src/panels/fields/controls";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

const renderNote = () =>
  render(
    <PreferencesProvider>
      <DismissibleNote id="web.experimental">Hint text</DismissibleNote>
    </PreferencesProvider>,
  );

describe("DismissibleNote", () => {
  beforeEach(() => {
    saved.length = 0;
  });

  it("asks before closing: the X alone does not close or save anything", async () => {
    renderNote();
    await waitFor(() => expect(screen.getByText("Hint text")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Close" }));

    expect(screen.getByText("Hint text")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Close for now" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Do not show again" })).toBeInTheDocument();
    expect(saved).toHaveLength(0);
  });

  it("'Close for now' hides it without saving a preference", async () => {
    renderNote();
    await waitFor(() => expect(screen.getByText("Hint text")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    fireEvent.click(screen.getByRole("button", { name: "Close for now" }));

    expect(screen.queryByText("Hint text")).not.toBeInTheDocument();
    expect(saved).toHaveLength(0);
  });

  it("'Do not show again' hides it and stores the id", async () => {
    renderNote();
    await waitFor(() => expect(screen.getByText("Hint text")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "Close" }));
    fireEvent.click(screen.getByRole("button", { name: "Do not show again" }));

    expect(screen.queryByText("Hint text")).not.toBeInTheDocument();
    await waitFor(() => expect(saved[saved.length - 1]?.dismissedNotices).toEqual({ "web.experimental": true }));
  });
});
