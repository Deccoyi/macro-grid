import { describe, expect, it } from "vitest";
import { effectiveOptions } from "../src/widgets/pluginWidget/PluginWidgetContent";

const info = { options: ["keepLoaded", "storage"], optionsOff: ["keepLoaded"] };

describe("effective widget options", () => {
  it("starts from the manifest defaults", () => {
    expect(effectiveOptions(info, undefined)).toEqual(["storage"]);
  });

  it("lets the person switch each option on or off for one widget", () => {
    expect(effectiveOptions(info, { options: { keepLoaded: true, storage: false } })).toEqual(["keepLoaded"]);
  });

  it("never adds an option the plugin did not declare", () => {
    expect(effectiveOptions(info, { options: { notifications: true } })).toEqual(["storage"]);
    expect(effectiveOptions(undefined, { options: { storage: true } })).toEqual([]);
  });

  it("ignores values that are not true or false", () => {
    expect(effectiveOptions(info, { options: { keepLoaded: "yes", storage: 0 } })).toEqual(["storage"]);
  });
});
