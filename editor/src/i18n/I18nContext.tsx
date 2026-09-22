import { useCallback, useMemo } from "react";
import { usePreferences } from "../preferences/PreferencesContext";
import { en } from "./en";
import { tr, type DictKey } from "./tr";

export type Language = "tr" | "en";
type Dict = Record<DictKey, string | ((...args: string[]) => string)>;
const DICTS: Record<Language, Dict> = { tr, en };

/** The one hook every component uses for both reading translated strings (t) and, rarely, the raw
 * language code (lang) or switching it (setLang, only the Preferences panel does). The language itself
 * lives in PreferencesContext (server-persisted, see PreferencesContext.tsx) — this hook just reads it. */
export function useT() {
  const { language, setLanguage } = usePreferences();

  const t = useCallback(
    (key: DictKey, ...args: string[]): string => {
      const value = DICTS[language][key];
      return typeof value === "function" ? (value as (...a: string[]) => string)(...args) : value;
    },
    [language],
  );

  return useMemo(() => ({ lang: language, setLang: setLanguage, t }), [language, setLanguage, t]);
}
