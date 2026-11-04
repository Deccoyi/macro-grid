import { describe, expect, it } from "vitest";
import { variableToken } from "../src/panels/dynamic/variableToken";

describe("variableToken", () => {
  it("keeps a token example of the variable", () => {
    expect(variableToken({ name: "system.cpu", example: "{system.cpu|0}" })).toBe("{system.cpu|0}");
    expect(variableToken({ name: "p.bitrate", example: "{p.bitrate|0} kbps" })).toBe("{p.bitrate|0} kbps");
    expect(variableToken({ name: "p.on", example: "{p.on}" })).toBe("{p.on}");
  });

  it("never inserts a sample value", () => {
    expect(variableToken({ name: "voicechanger.enabled", example: "true" })).toBe("{voicechanger.enabled}");
    expect(variableToken({ name: "voicechanger.voice", example: "Robot" })).toBe("{voicechanger.voice}");
    expect(variableToken({ name: "voicechanger.voice", example: "" })).toBe("{voicechanger.voice}");
  });

  it("ignores a token of another variable", () => {
    expect(variableToken({ name: "a.b", example: "{a.bc}" })).toBe("{a.b}");
  });
});
