import { describe, expect, it } from "vitest";
import { isCurrentColorShape } from "../src/panels/IconPicker";

describe("isCurrentColorShape", () => {
  it("accepts a pack icon drawn only in the current colour", () => {
    expect(isCurrentColorShape('<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M1 1h10"/></svg>')).toBe(true);
    expect(isCurrentColorShape('<svg><path fill="currentColor" d="M1 1h10"/></svg>')).toBe(true);
  });

  it("rejects an icon with its own colours, a filled background or a gradient", () => {
    expect(isCurrentColorShape('<svg><rect fill="#ff0000" width="24" height="24"/><path stroke="currentColor" d="M1 1"/></svg>')).toBe(false);
    expect(isCurrentColorShape('<svg><path style="fill:rgb(1,2,3)" stroke="currentColor"/></svg>')).toBe(false);
    expect(isCurrentColorShape('<svg><defs><linearGradient id="g"/></defs><path stroke="currentColor"/></svg>')).toBe(false);
  });

  it("rejects an icon that never uses the current colour", () => {
    expect(isCurrentColorShape('<svg><path d="M1 1"/></svg>')).toBe(false);
  });
});
