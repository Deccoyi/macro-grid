import { describe, expect, it } from "vitest";
import { setVariableModes } from "../src/panels/actionForms/forms";

describe("Set variable modes", () => {
  it("offers only what fits the type", () => {
    expect(setVariableModes("number")).toEqual(["set", "add", "reset"]);
    expect(setVariableModes("boolean")).toEqual(["set", "toggle", "reset"]);
    expect(setVariableModes("text")).toEqual(["set", "reset"]);
    expect(setVariableModes(undefined)).toEqual(["set", "reset"]);
  });
});
