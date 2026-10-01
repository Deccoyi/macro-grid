import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { en } from "../src/i18n/en";
import { format } from "../src/i18n/format";
import { PARAMS } from "../src/i18n/params";
import { tr, type DictKey } from "../src/i18n/tr";

/**
 * Records what every dictionary entry that takes values says for fixed sample values, in both languages, and compares it with a stored
 * file first made from the code as it was when the entries were still functions. Run with UPDATE_DICTIONARY_OUTPUT=1 to rewrite the
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

function record(dict: Record<DictKey, string>): Record<string, Record<string, string>> {
  const result: Record<string, Record<string, string>> = {};
  for (const [key, names] of Object.entries(PARAMS)) {
    const outputs: Record<string, string> = {};
    for (const [name, make] of Object.entries(SAMPLES)) outputs[name] = format(dict[key as DictKey], names, names.map((_, i) => make(i)));
    result[key] = outputs;
  }
  return result;
}

describe("dictionary entries with values", () => {
  const current = { en: record(en), tr: record(tr) };

  it("covers every entry that takes values", () => {
    expect(Object.keys(current.en).length).toBeGreaterThan(70);
    expect(Object.keys(current.tr)).toEqual(Object.keys(current.en));
  });

  it("says what the recorded file says", () => {
    if (process.env.UPDATE_DICTIONARY_OUTPUT || !existsSync(file)) writeFileSync(file, JSON.stringify(current, null, 2) + "\n");
    expect(current).toEqual(JSON.parse(readFileSync(file, "utf8")));
  });
});
