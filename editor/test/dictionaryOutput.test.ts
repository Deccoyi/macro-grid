import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { en } from "../src/i18n/en";
import { tr } from "../src/i18n/tr";

/**
 * Records what every dictionary entry that takes arguments says for fixed sample arguments, in both languages, and compares it with a
 * stored file made from the code as it was before the entries became plain strings. Run with UPDATE_DICTIONARY_OUTPUT=1 to rewrite the
 * file after a change to a sentence that is meant.
 */
const file = join(dirname(fileURLToPath(import.meta.url)), "dictionaryOutput.json");

const SAMPLES: Record<string, (index: number) => string> = {
  zero: () => "0",
  one: () => "1",
  two: () => "2",
  five: () => "5",
  words: (i) => `Arg${i + 1}`,
};

function record(dict: Record<string, unknown>): Record<string, Record<string, string>> {
  const result: Record<string, Record<string, string>> = {};
  for (const [key, value] of Object.entries(dict)) {
    if (typeof value !== "function") continue;
    const fn = value as (...args: string[]) => string;
    const outputs: Record<string, string> = {};
    for (const [name, make] of Object.entries(SAMPLES)) outputs[name] = fn(...Array.from({ length: fn.length }, (_, i) => make(i)));
    result[key] = outputs;
  }
  return result;
}

describe("dictionary entries with arguments", () => {
  const current = { en: record(en), tr: record(tr) };

  it("covers every function entry", () => {
    expect(Object.keys(current.en).length).toBeGreaterThan(70);
    expect(Object.keys(current.tr)).toEqual(Object.keys(current.en));
  });

  it("says what the recorded file says", () => {
    if (process.env.UPDATE_DICTIONARY_OUTPUT || !existsSync(file)) writeFileSync(file, JSON.stringify(current, null, 2) + "\n");
    expect(current).toEqual(JSON.parse(readFileSync(file, "utf8")));
  });
});
