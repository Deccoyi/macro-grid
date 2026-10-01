import { describe, expect, it } from "vitest";
import { toExportLines } from "../src/diagnostics/exportLines";
import type { Diagnostic } from "../src/diagnostics/types";

describe("toExportLines", () => {
  it("sends the editor's own lines as text and leaves the server's out", () => {
    const list: Diagnostic[] = [
      { id: "a", source: "p", sourceName: "Lights", severity: "error", code: "P110", message: "x", origin: "server" },
      { id: "b", source: "editor", severity: "warning", code: "W300", message: "No cell", count: 3, target: { profileId: "p", pageId: "pg", widgetId: "w" } },
    ];
    expect(toExportLines(list, (d) => d.message ?? "", "Editor")).toEqual([
      { severity: "warning", code: "W300", message: "No cell", source: "Editor", count: 3, page: "pg", widget: "w" },
    ]);
  });
});
