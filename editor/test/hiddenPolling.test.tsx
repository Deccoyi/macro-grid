import { act, cleanup, render, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const fetchProblems = vi.fn(() => Promise.resolve({ version: 1, problems: [] }));
vi.mock("../src/api/client", () => ({ api: { fetchProblems: () => fetchProblems(), clearProblems: () => Promise.resolve() } }));

import { DiagnosticsProvider } from "../src/diagnostics/DiagnosticsContext";
import { resetLiveValues, setLiveVariables, useLiveNames } from "../src/state/liveValues";

let hidden = false;
const setHidden = (value: boolean) => {
  hidden = value;
  document.dispatchEvent(new Event("visibilitychange"));
};

beforeEach(() => {
  vi.useFakeTimers();
  hidden = false;
  Object.defineProperty(document, "hidden", { configurable: true, get: () => hidden });
  fetchProblems.mockClear();
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
  resetLiveValues();
});

describe("polls while the page is hidden", () => {
  it("the problems poll stops when hidden and refreshes once when shown again", async () => {
    render(<DiagnosticsProvider><div /></DiagnosticsProvider>);
    expect(fetchProblems).toHaveBeenCalledTimes(1);

    act(() => setHidden(true));
    await act(() => vi.advanceTimersByTimeAsync(10_000));
    expect(fetchProblems).toHaveBeenCalledTimes(1);

    act(() => setHidden(false));
    expect(fetchProblems).toHaveBeenCalledTimes(2);
  });
});

describe("live values", () => {
  it("the names text changes only when a name appears or goes, not when a value changes", () => {
    const { result } = renderHook(() => useLiveNames());
    act(() => setLiveVariables({ "system.time": "10:00", "system.cpu": 5, gone: null }));
    expect(result.current).toBe("system.cpu\nsystem.time");
    const before = result.current;
    act(() => setLiveVariables({ "system.time": "10:01", "system.cpu": 9, gone: null }));
    expect(result.current).toBe(before);
  });
});
