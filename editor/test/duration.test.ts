import { describe, expect, it } from "vitest";
import { clampDuration, joinDuration, splitDuration } from "../src/panels/actionForms/duration";

describe("duration", () => {
  it("picks the unit a value reads best in", () => {
    expect(splitDuration(0)).toEqual({ amount: 0, unit: "ms" });
    expect(splitDuration(500)).toEqual({ amount: 500, unit: "ms" });
    expect(splitDuration(1500)).toEqual({ amount: 1.5, unit: "s" });
    expect(splitDuration(2000)).toEqual({ amount: 2, unit: "s" });
    expect(splitDuration(60000)).toEqual({ amount: 1, unit: "min" });
    expect(splitDuration(90000)).toEqual({ amount: 90, unit: "s" });
    expect(splitDuration(61050)).toEqual({ amount: 61050, unit: "ms" });
  });

  it("round-trips and rounds decimals to whole milliseconds", () => {
    for (const ms of [0, 500, 1500, 2000, 60000, 90000, 61000]) {
      const { amount, unit } = splitDuration(ms);
      expect(joinDuration(amount, unit)).toBe(ms);
    }
    expect(joinDuration(1.2345, "s")).toBe(1235);
  });

  it("clamps into the range", () => {
    expect(clampDuration(6000, 0, 5000)).toBe(5000);
    expect(clampDuration(-5, 0, 5000)).toBe(0);
    expect(clampDuration(100, null, null)).toBe(100);
  });
});
