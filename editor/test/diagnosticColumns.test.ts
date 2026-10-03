import { beforeEach, describe, expect, it } from "vitest";
import { clampWidth, DEFAULT_COLUMN_WIDTHS, gridTemplate, loadColumnWidths, MIN_COLUMN_WIDTH, saveColumnWidths, tableMinWidth } from "../src/panels/diagnosticColumns";

beforeEach(() => localStorage.clear());

describe("diagnostic columns", () => {
  it("keeps a width inside the allowed range", () => {
    expect(clampWidth(5)).toBe(MIN_COLUMN_WIDTH);
    expect(clampWidth(99999)).toBe(1600);
    expect(clampWidth(123.4)).toBe(123);
  });

  it("starts from the defaults and remembers what was dragged", () => {
    expect(loadColumnWidths()).toEqual(DEFAULT_COLUMN_WIDTHS);
    saveColumnWidths({ ...DEFAULT_COLUMN_WIDTHS, description: 700 });
    expect(loadColumnWidths().description).toBe(700);
  });

  it("ignores damaged stored values", () => {
    localStorage.setItem("macro-grid.editor.diagnosticColumns", JSON.stringify({ code: "wide", source: 2 }));
    const widths = loadColumnWidths();
    expect(widths.code).toBe(DEFAULT_COLUMN_WIDTHS.code);
    expect(widths.source).toBe(MIN_COLUMN_WIDTH);
  });

  it("lets the description take the rest of a wide panel and makes a narrow one scroll", () => {
    expect(gridTemplate(DEFAULT_COLUMN_WIDTHS)).toBe("90px 70px 420px 150px 130px 100px 70px 1fr");
    expect(tableMinWidth(DEFAULT_COLUMN_WIDTHS)).toBe(1050);
  });
});

describe("hidden columns and auto-fit", () => {
  it("remembers the hidden columns and always keeps one", async () => {
    const { loadHiddenColumns, saveHiddenColumns, visibleColumns, DIAGNOSTIC_COLUMNS } = await import("../src/panels/diagnosticColumns");
    expect(loadHiddenColumns()).toEqual([]);
    saveHiddenColumns(["code", "details"]);
    expect(loadHiddenColumns()).toEqual(["code", "details"]);
    expect(visibleColumns(["code", "details"])).toEqual(["severity", "description", "source", "screen", "location"]);
    expect(visibleColumns([...DIAGNOSTIC_COLUMNS])).toEqual(["severity"]);
    localStorage.setItem("macro-grid.editor.diagnosticHiddenColumns", "{bad");
    expect(loadHiddenColumns()).toEqual([]);
  });

  it("fits a column to its widest text plus padding, inside the allowed range", async () => {
    const { fitWidth } = await import("../src/panels/diagnosticColumns");
    const measure = (text: string) => text.length * 7;
    expect(fitWidth(["ab", "abcdef"], measure, 20)).toBe(62);
    expect(fitWidth([], measure, 0)).toBe(MIN_COLUMN_WIDTH);
  });
});
