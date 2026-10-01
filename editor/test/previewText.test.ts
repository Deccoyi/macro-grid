import { describe, expect, it } from "vitest";
import { renderPreviewText } from "../src/grid/previewText";

describe("preview text", () => {
  it("leaves text without tokens alone", () => {
    expect(renderPreviewText("Hello", {})).toBe("Hello");
    expect(renderPreviewText(undefined, {})).toBeUndefined();
  });

  it("fills a number with its format", () => {
    expect(renderPreviewText("CPU {cpu|0}%", { cpu: 42.3 })).toBe("CPU 42%");
  });

  it("renders a missing or null variable as empty text", () => {
    expect(renderPreviewText("a{x}b", {})).toBe("ab");
    expect(renderPreviewText("a{x}b", { x: null })).toBe("ab");
  });

  it("keeps escaped braces as literal braces", () => {
    expect(renderPreviewText("{{x}}", { x: 1 })).toBe("{x}");
  });
});
