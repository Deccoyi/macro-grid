import { describe, expect, it } from "vitest";
import { translateServerMessage } from "../src/diagnostics/serverMessages";
import { en } from "../src/i18n/en";
import { tr } from "../src/i18n/tr";

describe("translateServerMessage", () => {
  it("finds the key and the variable of a known server message", () => {
    expect(translateServerMessage("This rule uses the variable 'user.b', which does not exist.")).toEqual({ key: "diag.server.ruleVariableMissing", args: ["user.b"] });
    expect(translateServerMessage("The variable 'user.x' does not exist.")).toEqual({ key: "diag.server.variableMissing", args: ["user.x"] });
    expect(translateServerMessage("A rule cannot press keys or type text by itself. Remove this step from the rule.")?.key).toBe("diag.server.ruleKeys");
  });

  it("leaves other messages alone", () => {
    expect(translateServerMessage("Something else failed.")).toBeNull();
  });

  it("has a Turkish text for every known key", () => {
    for (const key of ["diag.server.variableMissing", "diag.server.ruleVariableMissing", "diag.server.ruleKeys", "diag.server.ruleDropped", "diag.server.ruleSwitchedOff", "diag.server.stopping"] as const) {
      expect(tr[key]).toBeTruthy();
      expect(en[key]).toBeTruthy();
    }
  });
});
