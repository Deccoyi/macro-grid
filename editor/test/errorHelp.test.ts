import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { en } from "../src/i18n/en";
import { tr } from "../src/i18n/tr";

const DICTS = { en, tr };

/** Every code a person can see in the Error List has a help text in both languages. The codes are read from the sources that make them. */
function codes(): string[] {
  const found = new Set<string>();
  const server = readFileSync("../src/MacroGrid.Core/Diagnostics/ProblemList.cs", "utf8");
  for (const m of server.matchAll(/=\s*"(P\d{3})"/g)) found.add(m[1]!);
  for (const file of ["profileCheck.ts", "editorEvents.ts", "saveSummary.ts"]) {
    for (const m of readFileSync(`src/diagnostics/${file}`, "utf8").matchAll(/"([EWI]\d{3})"/g)) found.add(m[1]!);
  }
  return [...found];
}

describe("error help texts", () => {
  it("reads a believable number of codes", () => {
    expect(codes().length).toBeGreaterThan(20);
  });

  it.each(["en", "tr"] as const)("has a text for every code in %s", (lang) => {
    const dict = DICTS[lang] as unknown as Record<string, unknown>;
    expect(codes().filter((c) => typeof dict[`errorHelp.${c}`] !== "string")).toEqual([]);
  });
});
