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

describe("plural keys", () => {
  const bases = Object.keys(en).filter((k) => k.endsWith(".other")).map((k) => k.slice(0, -".other".length));

  it("have .one and .other in both languages", () => {
    expect(bases.length).toBeGreaterThanOrEqual(8);
    for (const base of bases) for (const dict of [en, tr]) {
      expect(`${base}.one` in dict, base).toBe(true);
      expect(`${base}.other` in dict, base).toBe(true);
    }
  });

  it("are picked by the language's plural rules", () => {
    const category = (lang: string, n: number) => new Intl.PluralRules(lang).select(n);
    expect(category("en", 1)).toBe("one");
    expect(category("en", 0)).toBe("other");
    expect(category("en", 2)).toBe("other");
    expect(category("tr", 1)).toBe("one");
  });

  it("say the singular wording for one", () => {
    expect(format(en["status.pluginUpdates.one"], PARAMS["status.pluginUpdates.one"]!, ["1"])).toBe("1 plugin update needed");
    expect(format(en["status.pluginUpdates.other"], PARAMS["status.pluginUpdates.other"]!, ["3"])).toBe("3 plugin updates needed");
  });
});
