import { useCallback, useMemo } from "react";
import { usePreferences } from "../preferences/PreferencesContext";
import { en } from "./en";
import { format } from "./format";
import { isBuiltIn, localeOf, type Language } from "./language";
import { usePack } from "./packStore";
import { PARAMS } from "./params";
import { tr, type DictKey } from "./tr";

export type { Language } from "./language";
const BUILT_IN_DICTS: Record<"tr" | "en", Record<DictKey, string>> = { tr, en };

/** The base of every plural string: `status.pluginUpdates` for the keys `status.pluginUpdates.one` and `status.pluginUpdates.other`. */
export type PluralBase = DictKey extends infer K ? (K extends `${infer B}.other` ? B : never) : never;

const pluralRules = new Map<string, Intl.PluralRules>();
function pluralCategory(language: Language, count: number): string {
  const locale = localeOf(language);
  let rules = pluralRules.get(locale);
  if (!rules) pluralRules.set(locale, (rules = new Intl.PluralRules(locale)));
  return rules.select(count);
}

/** A replaced value inside a pack's text is wrapped in direction isolates, so a value in another script cannot reorder the words around it. */
const isolate = (value: string) => `\u2068${value}\u2069`;

/** The one hook every component uses for both reading translated strings (t, tn for a text that depends on a number) and, rarely, the raw
 * language code (lang) or switching it (setLang, only the Preferences panel does). The language itself
 * lives in PreferencesContext (server-persisted, see PreferencesContext.tsx) — this hook just reads it.
 * With a language pack active a key the pack lacks (or failed the check) shows its English text. */
export function useT() {
  const { language, setLanguage } = usePreferences();
  const pack = usePack();
  const activePack = !isBuiltIn(language) && pack?.tag === language ? pack : null;

  const raw = useCallback(
    (key: string): string | undefined => {
      if (isBuiltIn(language)) return BUILT_IN_DICTS[language][key as DictKey];
      return activePack && Object.hasOwn(activePack.strings, key) ? activePack.strings[key] : undefined;
    },
    [language, activePack],
  );

  const t = useCallback(
    (key: DictKey, ...args: string[]): string => {
      const value = raw(key) ?? en[key];
      const names = PARAMS[key];
      return names ? format(value, names, args, isBuiltIn(language) ? undefined : isolate) : value;
    },
    [language, raw],
  );

  /** A text that depends on a number: picks `<base>.<category>` by the language's plural rules, else `<base>.other`. The count is the first value. */
  const tn = useCallback(
    (base: PluralBase, count: number, ...args: string[]): string => {
      const category = pluralCategory(language, count);
      const specific = `${base}.${category}`;
      const other = `${base}.other`;
      const key = raw(specific) !== undefined ? specific : raw(other) !== undefined ? other : Object.hasOwn(en, specific) ? specific : other;
      return t(key as DictKey, String(count), ...args);
    },
    [language, raw, t],
  );

  return useMemo(() => ({ lang: language, setLang: setLanguage, t, tn }), [language, setLanguage, t, tn]);
}
