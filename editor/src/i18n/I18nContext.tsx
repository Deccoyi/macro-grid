import { useCallback, useMemo } from "react";
import { usePreferences } from "../preferences/PreferencesContext";
import { en } from "./en";
import { format } from "./format";
import { PARAMS } from "./params";
import { tr, type DictKey } from "./tr";

export type Language = "tr" | "en";
const DICTS: Record<Language, Record<DictKey, string>> = { tr, en };

/** The one hook every component uses for both reading translated strings (t) and, rarely, the raw
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

  return useMemo(() => ({ lang: language, setLang: setLanguage, t }), [language, setLanguage, t]);
}
