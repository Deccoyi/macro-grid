import { describe, expect, it } from "vitest";
import { en } from "../src/i18n/en";
import { format } from "../src/i18n/format";
import { PARAMS } from "../src/i18n/params";
import { tr } from "../src/i18n/tr";

const tokens = (text: string) => [...text.matchAll(/\{([A-Za-z][A-Za-z0-9]*)\}/g)].map((m) => m[1]!);

describe("format", () => {
  it("fills declared names in one pass", () => {
    expect(format("{a} and {b}", ["a", "b"], ["1", "2"])).toBe("1 and 2");
    expect(format("{a}", ["a"], ["{b}"])).toBe("{b}");
    expect(format("{a} {a}", ["a"], ["x"])).toBe("x x");
  });

  it("leaves unknown tokens alone and gives an empty string for a missing value", () => {
    expect(format("{user.name} {x}", ["a"], ["1"])).toBe("{user.name} {x}");
    expect(format("[{a}][{b}]", ["a", "b"], ["1"])).toBe("[1][]");
  });
});

describe("dictionaries", () => {
  it("hold only plain strings and the same keys", () => {
    for (const dict of [en, tr]) for (const value of Object.values(dict)) expect(typeof value).toBe("string");
    expect(Object.keys(en).sort()).toEqual(Object.keys(tr).sort());
  });

  it("list parameters only for keys that exist, and each declared name occurs in both languages", () => {
    for (const [key, names] of Object.entries(PARAMS)) {
      expect(key in en, key).toBe(true);
      for (const dict of [en, tr]) {
        const used = tokens((dict as Record<string, string>)[key]!);
        for (const name of used) expect(names, `${key} uses {${name}}`).toContain(name);
      }
      expect(new Set(names).size, key).toBe(names.length);
    }
  });

  it("use the same placeholders in both languages", () => {
    for (const key of Object.keys(en)) {
      const a = [...new Set(tokens((en as Record<string, string>)[key]!))].filter((n) => PARAMS[key as keyof typeof en]?.includes(n)).sort();
      const b = [...new Set(tokens((tr as Record<string, string>)[key]!))].filter((n) => PARAMS[key as keyof typeof en]?.includes(n)).sort();
      expect(b, key).toEqual(a);
    }
  });

});
