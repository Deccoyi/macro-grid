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

  it("shows the placeholder while a variable is unavailable and not when it has a value", () => {
    expect(renderPreviewText("CPU {cpu|0|--}", {})).toBe("CPU --");
    expect(renderPreviewText("{a||n/a}", { a: null })).toBe("n/a");
    expect(renderPreviewText("{a||x|y}", {})).toBe("x|y");
    expect(renderPreviewText("{cpu|0|--}", { cpu: 42.3 })).toBe("42");
    expect(renderPreviewText("[{b||--}]", { b: "" })).toBe("[]");
  });

  it("cuts a long placeholder", () => {
    expect(renderPreviewText("{a||" + "x".repeat(200) + "}", {})).toHaveLength(64);
  });

  it("keeps escaped braces as literal braces", () => {
    expect(renderPreviewText("{{x}}", { x: 1 })).toBe("{x}");
  });
});
