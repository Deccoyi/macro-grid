import { beforeEach, describe, expect, it, vi } from "vitest";
import { offPlugins, reportRunning, subscribe, takeCrash, turnOn } from "../src/grid/widgetCrashGuard";

beforeEach(() => localStorage.clear());

describe("editor widget crash guard", () => {
  it("blames nobody for a normal start", () => {
    reportRunning(["gauges"]);

    expect(takeCrash("?window=x", vi.fn())).toEqual([]);
    expect(offPlugins().size).toBe(0);
  });

  it("switches off the plugins that were running when the window reloaded the editor after a crash, and cleans the address", () => {
    reportRunning(["gauges", "clock"]);
    const replace = vi.fn();

    expect(takeCrash("?window=x&widgetCrash=1", replace)).toEqual(["gauges", "clock"]);

    expect([...offPlugins()].sort()).toEqual(["clock", "gauges"]);
    expect(replace).toHaveBeenCalledWith(expect.stringContaining("window=x"));
    expect(replace).not.toHaveBeenCalledWith(expect.stringContaining("widgetCrash"));
    // The list of running plugins belonged to the dead renderer: a second reload does not blame them again.
    expect(takeCrash("?widgetCrash=1", vi.fn())).toEqual([]);
  });

  it("keeps a plugin off until the person turns it on, and says so to whoever listens", () => {
    reportRunning(["gauges"]);
    takeCrash("?widgetCrash=1", vi.fn());
    const changed = vi.fn();
    const stop = subscribe(changed);

    turnOn("gauges");

    expect(offPlugins().has("gauges")).toBe(false);
    expect(changed).toHaveBeenCalledTimes(1);
    stop();
  });
});
