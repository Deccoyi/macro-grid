import { describe, expect, it } from "vitest";
import { problemWidgets } from "../src/diagnostics/problemWidgets";
import type { Diagnostic } from "../src/diagnostics/types";

const line = (id: string, severity: Diagnostic["severity"], widgetId: string): Diagnostic =>
  ({ id, source: "s", severity, code: id, message: id, origin: "server", target: { profileId: "p", widgetId } }) as Diagnostic;

describe("problemWidgets", () => {
  it("keeps every line per widget, errors first, and lets an error win over a warning", () => {
    const marked = problemWidgets([line("W1", "warning", "a"), line("E1", "error", "a"), line("W2", "warning", "b"), line("I1", "info", "c"), line("E2", "error", "z")],
      "p", (d) => d.code);
    expect(marked.get("a")).toEqual({ level: "error", texts: ["E1", "W1"] });
    expect(marked.get("b")).toEqual({ level: "warning", texts: ["W2"] });
    expect(marked.has("c")).toBe(false);
  });
});
