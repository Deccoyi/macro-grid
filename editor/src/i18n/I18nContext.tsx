import { useCallback, useMemo } from "react";
import { usePreferences } from "../preferences/PreferencesContext";
import { en } from "./en";
import { format } from "./format";
import { PARAMS } from "./params";
import { tr, type DictKey } from "./tr";

export type Language = "tr" | "en";
const DICTS: Record<Language, Record<DictKey, string>> = { tr, en };

/** The base of every plural string: `status.pluginUpdates` for the keys `status.pluginUpdates.one` and `status.pluginUpdates.other`. */
export type PluralBase = DictKey extends infer K ? (K extends `${infer B}.other` ? B : never) : never;

const pluralRules = new Map<string, Intl.PluralRules>();
function pluralCategory(language: string, count: number): string {
  let rules = pluralRules.get(language);
  if (!rules) pluralRules.set(language, (rules = new Intl.PluralRules(language)));
  return rules.select(count);
}

/** The one hook every component uses for both reading translated strings (t, tn for a text that depends on a number) and, rarely, the raw
 * language code (lang) or switching it (setLang, only the Preferences panel does). The language itself
 * lives in PreferencesContext (server-persisted, see PreferencesContext.tsx) — this hook just reads it. */
export function useT() {
  const { language, setLanguage } = usePreferences();

  const t = useCallback(
    (key: DictKey, ...args: string[]): string => {
      const names = PARAMS[key];
      const value = DICTS[language][key];
      return names ? format(value, names, args) : value;
    },
    [language],
  );

  /** A text that depends on a number: picks `<base>.<category>` by the language's plural rules, else `<base>.other`. The count is the first value. */
  const tn = useCallback(
    (base: PluralBase, count: number, ...args: string[]): string => {
      const specific = `${base}.${pluralCategory(language, count)}` as DictKey;
      return t(specific in DICTS[language] ? specific : (`${base}.other` as DictKey), String(count), ...args);
    },
    [language, t],
  );

  return useMemo(() => ({ lang: language, setLang: setLanguage, t, tn }), [language, setLanguage, t, tn]);
}
