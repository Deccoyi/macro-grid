import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { OptionsQuery, OptionsResult, SettingField } from "../src/api/types";

vi.mock("../src/api/client", () => ({
  api: {
    getPreferences: () => Promise.resolve({ dismissedNotices: {}, dockLayoutJson: "", previewProfiles: [], collapsedInspectorSections: {}, dockLayoutProfiles: [], theme: "dark", language: "en" }),
    savePreferences: () => Promise.resolve(),
    getLanguagePack: () => Promise.reject(new Error("404")),
    listLanguagePacks: () => Promise.resolve([]),
  },
}));

import { iconSrc, resolveOptionIcon } from "../src/api/optionIcon";
import { SchemaForm, type WidgetIconControl } from "../src/panels/actionForms/SchemaForm";
import { PreferencesProvider } from "../src/preferences/PreferencesContext";

const field: SettingField = { key: "voice", label: "Voice", kind: "Select", optionsSource: "voices", searchable: true };

type Call = { source: string; query?: OptionsQuery; resolve: (r: OptionsResult) => void; reject: (e: unknown) => void };

function setup(values: Record<string, unknown> = {}, widgetIcon?: WidgetIconControl) {
  const calls: Call[] = [];
  const fetchOptions = vi.fn((source: string, _values: Record<string, unknown>, query?: OptionsQuery) =>
    new Promise<OptionsResult>((resolve, reject) => { calls.push({ source, query, resolve, reject }); }));
  const onChange = vi.fn();
  const view = render(
    <PreferencesProvider>
      <SchemaForm fields={[field]} values={values} onChange={onChange} fetchOptions={fetchOptions} widgetIcon={widgetIcon} />
    </PreferencesProvider>,
  );
  const input = () => screen.getByRole("combobox") as HTMLInputElement;
  return { calls, fetchOptions, onChange, input, ...view };
}

const opt = (value: string, extra: object = {}) => ({ value, label: value.toUpperCase(), ...extra });
const flush = () => act(async () => { await Promise.resolve(); });

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("SearchSelect", () => {
  it("debounces typing by 250 ms and aborts the previous request", async () => {
    const { calls, input } = setup();
    await flush();
    expect(calls).toHaveLength(1); // the stored value is checked once on mount
    fireEvent.focus(input());
    expect(calls).toHaveLength(2);
    const opening = calls[1]!.query!.signal!;

    fireEvent.change(input(), { target: { value: "a" } });
    fireEvent.change(input(), { target: { value: "ab" } });
    await act(async () => { vi.advanceTimersByTime(249); });
    expect(calls).toHaveLength(2);
    await act(async () => { vi.advanceTimersByTime(1); });
    expect(calls).toHaveLength(3);
    expect(calls[2]!.query).toMatchObject({ q: "ab", offset: 0, limit: 50 });
    expect(opening.aborted).toBe(true);
  });

  it("ignores a stale response that arrives after a newer one", async () => {
    const { calls, input } = setup();
    fireEvent.focus(input());
    fireEvent.change(input(), { target: { value: "x" } });
    await act(async () => { vi.advanceTimersByTime(250); });
    const newest = calls[calls.length - 1]!;
    const older = calls[calls.length - 2]!;
    await act(async () => { newest.resolve({ options: [opt("new")] }); });
    await act(async () => { older.resolve({ options: [opt("old")] }); });
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["NEW"]);
  });

  it("loads the next page at the end of the list when there is more", async () => {
    const { calls, input } = setup();
    fireEvent.focus(input());
    const first = calls[calls.length - 1]!;
    await act(async () => { first.resolve({ options: [opt("a"), opt("b")], hasMore: true }); });
    fireEvent.scroll(screen.getByRole("listbox"));
    const next = calls[calls.length - 1]!;
    expect(next.query).toMatchObject({ q: "", offset: 2 });
    await act(async () => { next.resolve({ options: [opt("c")], hasMore: false }); });
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["A", "B", "C"]);
    fireEvent.scroll(screen.getByRole("listbox"));
    expect(calls[calls.length - 1]).toBe(next); // hasMore is false: no further request
  });

  it("works from the keyboard: arrows move, Enter picks, Escape closes", async () => {
    const { calls, input, onChange } = setup();
    fireEvent.focus(input());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("a"), opt("b", { group: "G" })] }); });
    expect(input()).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByRole("listbox")).toBeInTheDocument();
    expect(screen.getByText("G")).toBeInTheDocument(); // group header
    fireEvent.keyDown(input(), { key: "ArrowDown" });
    expect(input().getAttribute("aria-activedescendant")).toBe(screen.getAllByRole("option")[1]!.id);
    fireEvent.keyDown(input(), { key: "Enter" });
    expect(onChange).toHaveBeenCalledWith({ voice: "b", "voice＠label": "B" });
    expect(screen.queryByRole("listbox")).toBeNull();

    fireEvent.focus(input());
    fireEvent.keyDown(input(), { key: "Escape" });
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("clears the value and its label", async () => {
    const { onChange } = setup({ voice: "a", "voice＠label": "A" });
    fireEvent.click(screen.getByLabelText("Clear selection"));
    expect(onChange).toHaveBeenCalledWith({ voice: "" });
  });

  it("shows the stored label while the source is offline", async () => {
    const { calls, input } = setup({ voice: "v1", "voice＠label": "Robot" });
    expect(input().value).toBe("Robot");
    await act(async () => { calls[0]!.reject(new Error("Voicemod is not running")); });
    expect(input().value).toBe("Robot");
    expect(screen.getByRole("alert")).toHaveTextContent("Voicemod is not running");
  });

  it("marks a stored item the source no longer has as unavailable", async () => {
    const { calls, input } = setup({ voice: "gone", "voice＠label": "Old voice" });
    await act(async () => { calls[0]!.resolve({ options: [opt("other")] }); });
    expect(input().value).toBe("Unavailable: Old voice");
    expect(calls[0]!.query).toMatchObject({ selected: "gone" });
  });

  it("shows the label the source returns for the selected value", async () => {
    const { calls, input } = setup({ voice: "v1", "voice＠label": "Stale name" });
    await act(async () => { calls[0]!.resolve({ options: [{ value: "v1", label: "New name" }] }); });
    expect(input().value).toBe("New name");
  });
});

describe("button picture", () => {
  const ref = "plugin-image:voicemod/abc";

  it("is on by default when the button has no icon and writes the reference", async () => {
    const apply = vi.fn();
    const { calls, input, onChange } = setup({}, { current: undefined, apply });
    fireEvent.focus(input());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("a", { icon: ref })] }); });
    fireEvent.click(screen.getByRole("option"));
    expect(onChange).toHaveBeenCalledWith({ voice: "a", "voice＠label": "A" });
    expect(apply).toHaveBeenCalledWith(ref);
  });

  it("does not replace an icon the person chose", async () => {
    const apply = vi.fn();
    const { calls, input } = setup({}, { current: "data:image/svg+xml;utf8,x", apply });
    fireEvent.focus(input());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("a", { icon: ref })] }); });
    fireEvent.click(screen.getByRole("option"));
    expect(apply).not.toHaveBeenCalled();
    const box = screen.getByLabelText("Use its picture as the button icon") as HTMLInputElement;
    expect(box.checked).toBe(false);
    fireEvent.click(box);
    expect(apply).toHaveBeenCalledWith(ref);
  });

  it("unticking removes the picture; a built-in icon name offers no checkbox", async () => {
    const apply = vi.fn();
    const { calls, input } = setup({ voice: "a" }, { current: ref, apply });
    await act(async () => { calls[0]!.resolve({ options: [opt("a", { icon: ref })] }); });
    fireEvent.click(screen.getByLabelText("Use its picture as the button icon"));
    expect(apply).toHaveBeenCalledWith(undefined);

    fireEvent.focus(input());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("b", { icon: "mic" })] }); });
    fireEvent.click(screen.getByRole("option"));
    expect(screen.queryByLabelText("Use its picture as the button icon")).toBeNull();
  });
});

describe("icon references", () => {
  it("maps a plugin image to the editor endpoint and leaves other icons alone", () => {
    expect(resolveOptionIcon("plugin-image:my plugin/img 1")).toBe("/api/plugins/my%20plugin/images/img%201");
    expect(resolveOptionIcon("mic")).toBeNull();
    expect(resolveOptionIcon(undefined)).toBeNull();
    expect(iconSrc("data:image/png;base64,AA")).toBe("data:image/png;base64,AA");
    expect(iconSrc("plugin-image:p/i")).toBe("/api/plugins/p/images/i");
  });
});

describe("refresh", () => {
  const refreshBtn = () => screen.getByLabelText("Refresh list");

  it("asks with refresh once, shows a spinner and re-asks every 1.5 s until the source is done", async () => {
    const { calls } = setup();
    await act(async () => { calls[0]!.resolve({ options: [] }); });
    fireEvent.click(refreshBtn());
    const click = calls[calls.length - 1]!;
    expect(click.query).toMatchObject({ refresh: true, offset: 0 });
    expect(refreshBtn().querySelector("svg")).toHaveClass("spin");

    await act(async () => { click.resolve({ options: [opt("old")], refreshing: true }); });
    expect(refreshBtn().querySelector("svg")).toHaveClass("spin");
    const before = calls.length;
    await act(async () => { vi.advanceTimersByTime(1499); });
    expect(calls).toHaveLength(before);
    await act(async () => { vi.advanceTimersByTime(1); });
    const poll = calls[calls.length - 1]!;
    expect(calls).toHaveLength(before + 1);
    expect(poll.query?.refresh).toBeFalsy();

    await act(async () => { poll.resolve({ options: [opt("fresh")], refreshing: false }); });
    expect(refreshBtn().querySelector("svg")).not.toHaveClass("spin");
    await act(async () => { vi.advanceTimersByTime(10000); });
    expect(calls).toHaveLength(before + 1); // done: no more asking
  });

  it("gives up after 8 retries and keeps the latest options", async () => {
    const { calls } = setup();
    await act(async () => { calls[0]!.resolve({ options: [] }); });
    fireEvent.click(refreshBtn());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("x")], refreshing: true }); });
    const after = calls.length;
    for (let i = 0; i < 8; i++) {
      await act(async () => { vi.advanceTimersByTime(1500); });
      await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("x" + i)], refreshing: true }); });
    }
    expect(calls).toHaveLength(after + 8);
    await act(async () => { vi.advanceTimersByTime(10000); });
    expect(calls).toHaveLength(after + 8);
    expect(refreshBtn().querySelector("svg")).not.toHaveClass("spin");
  });

  it("works with an empty list and shows the source's error", async () => {
    const { calls } = setup();
    await act(async () => { calls[0]!.resolve({ options: [], error: "Start the app" }); });
    fireEvent.click(refreshBtn());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [], error: "Start the app" }); });
    expect(screen.getByRole("alert")).toHaveTextContent("Start the app");
  });

  it("does not let a stale poll answer overwrite a newer one, and stops on unmount", async () => {
    const { calls, input, unmount } = setup();
    await act(async () => { calls[0]!.resolve({ options: [] }); });
    fireEvent.click(refreshBtn());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [opt("a")], refreshing: true }); });
    await act(async () => { vi.advanceTimersByTime(1500); });
    const poll = calls[calls.length - 1]!;
    fireEvent.focus(input()); // a newer request replaces the poll
    const newer = calls[calls.length - 1]!;
    expect(poll.query!.signal!.aborted).toBe(true);
    await act(async () => { newer.resolve({ options: [opt("new")] }); });
    await act(async () => { poll.resolve({ options: [opt("stale")], refreshing: true }); });
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual(["NEW"]);

    fireEvent.click(refreshBtn());
    await act(async () => { calls[calls.length - 1]!.resolve({ options: [], refreshing: true }); });
    const last = calls[calls.length - 1]!;
    unmount();
    expect(last.query!.signal!.aborted).toBe(true);
    const n = calls.length;
    await act(async () => { vi.advanceTimersByTime(5000); });
    expect(calls).toHaveLength(n);
  });
});

describe("layout", () => {
  it("draws plugin Selects stacked, never as a Label | Value row, and one per row", () => {
    const fields: SettingField[] = [
      { key: "source", label: "Source", kind: "Select", options: [{ value: "a", label: "A" }] },
      { key: "panel", label: "Panel", kind: "Select", optionsSource: "p", searchable: true },
    ];
    const { container } = render(
      <PreferencesProvider>
        <SchemaForm fields={fields} values={{}} onChange={() => {}} fetchOptions={() => new Promise(() => {})} />
      </PreferencesProvider>,
    );
    expect(container.querySelectorAll(".pf-field")).toHaveLength(2);
    expect(container.querySelector(".pf-field.single")).toBeNull();
    expect(container.querySelector(".pf-grid-2")).toBeNull();
  });
});
