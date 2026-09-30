import { describe, expect, it } from "vitest";
import { widgetOptionOf } from "../src/windows/widgetPermission";

describe("widget permission", () => {
  it("recognises the options a widget declares", () => {
    expect(widgetOptionOf("widget:gauge:storage")).toBe("storage");
    expect(widgetOptionOf("widget:clock:keepLoaded")).toBe("keepLoaded");
    expect(widgetOptionOf("widget:x:notifications")).toBeNull();
  });

  it("leaves everything else alone", () => {
    expect(widgetOptionOf("variables")).toBeNull();
    expect(widgetOptionOf("http:example.com")).toBeNull();
    expect(widgetOptionOf("widget:gauge:other")).toBeNull();
    expect(widgetOptionOf("widget:gauge")).toBeNull();
  });
});
