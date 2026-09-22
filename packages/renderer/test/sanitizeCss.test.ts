import { describe, expect, it } from "vitest";
import { sanitizeWidgetCss } from "../src/style/sanitizeCss";

describe("sanitizeWidgetCss", () => {
  it("returns empty for empty input", () => {
    expect(sanitizeWidgetCss(undefined)).toEqual({ css: "", removed: [] });
    expect(sanitizeWidgetCss("   ")).toEqual({ css: "", removed: [] });
  });

  it("strips size and position properties", () => {
    const { css, removed } = sanitizeWidgetCss(":host { width: 500px; height: 10px; top: 0; position: absolute; }");

    expect(css).not.toMatch(/width|height|position|top/);
    expect(removed.length).toBeGreaterThanOrEqual(4);
  });

  it("strips grid-* and margin/transform/zoom/display", () => {
    const { css } = sanitizeWidgetCss(
      ":host { grid-column: 2; margin: 4px; transform: scale(2); zoom: 2; display: none; }",
    );

    expect(css).not.toMatch(/grid-column|margin|transform|zoom|display/);
  });

  it("strips min-*/max-* properties", () => {
    const { css } = sanitizeWidgetCss(":host { min-width: 10px; max-height: 5px; }");

    expect(css).toBe("");
  });

  it("keeps purely visual declarations", () => {
    const input = ":host { background: linear-gradient(45deg, red, blue); border: 2px solid gold; box-shadow: 0 0 4px #000; }";

    const { css, removed } = sanitizeWidgetCss(input);

    expect(css).toContain("linear-gradient(45deg, red, blue)");
    expect(css).toContain("border: 2px solid gold");
    expect(css).toContain("box-shadow: 0 0 4px #000");
    expect(removed).toEqual([]);
  });

  it("removes declarations with an external url() but keeps data: URIs", () => {
    const { css, removed } = sanitizeWidgetCss(
      ".icon { background-image: url(https://evil.example/x.png); } .ok { background-image: url(data:image/png;base64,AAAA); }",
    );

    expect(css).not.toContain("evil.example");
    expect(css).toContain("data:image/png;base64,AAAA");
    expect(removed.some((r) => r.includes("dış url"))).toBe(true);
  });

  it("strips @import (would fetch an external stylesheet)", () => {
    const { css, removed } = sanitizeWidgetCss("@import url('https://evil.example/x.css'); .a { color: red; }");

    expect(css).not.toContain("@import");
    expect(css).toContain("color: red");
    expect(removed.some((r) => r.startsWith("@import"))).toBe(true);
  });

  it("drops rules left empty after stripping", () => {
    const { css } = sanitizeWidgetCss(".a { width: 10px; }");

    expect(css.trim()).toBe("");
  });

  it("never throws on malformed CSS", () => {
    expect(() => sanitizeWidgetCss("{{{ not: css; ]][")).not.toThrow();
  });

  it("property matching is case-insensitive", () => {
    const { css } = sanitizeWidgetCss(":host { WIDTH: 10px; Transform: scale(2); }");

    expect(css).not.toMatch(/width|transform/i);
  });
});
