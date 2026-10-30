import { describe, expect, it } from "vitest";
import { matchesSearch } from "../src/panels/paletteSearch";

describe("toolbox search", () => {
  it("matches everything for an empty query", () => {
    expect(matchesSearch("", "Gauge")).toBe(true);
    expect(matchesSearch("   ", "Gauge")).toBe(true);
  });

  it("ignores case and looks in every text", () => {
    expect(matchesSearch("gau", "Gauge", "An animated dial")).toBe(true);
    expect(matchesSearch("DIAL", "Gauge", "An animated dial")).toBe(true);
  });

  it("needs every word to be found", () => {
    expect(matchesSearch("animated gauge", "Gauge", "An animated dial")).toBe(true);
    expect(matchesSearch("animated clock", "Gauge", "An animated dial")).toBe(false);
  });
});
