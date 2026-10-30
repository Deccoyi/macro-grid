/** The language preference: "tr" or "en" (built in), or the tag of an installed language pack ("de", "pt-BR"). */
export type Language = string;

export const BUILT_IN = ["tr", "en"] as const;
export type BuiltInLanguage = (typeof BUILT_IN)[number];

/** The development-only pseudo language (see pack/pseudo.ts). It is built in the editor and never stored on the server. */
export const PSEUDO_TAG = "qps-ploc";

export const MAX_TAG_LENGTH = 35;
const TAG_PATTERN = /^[a-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$/;

export function isBuiltIn(language: Language): language is BuiltInLanguage {
  return language === "tr" || language === "en";
}

/** The canonical form of a tag a pack may have (the same rule the server applies to the file name), or null when it is not allowed. */
export function canonicalTag(input: string): string | null {
  const tag = input.trim();
  if (tag.length === 0 || tag.length > MAX_TAG_LENGTH || !TAG_PATTERN.test(tag)) return null;
  let canonical: string | undefined;
  try {
    canonical = Intl.getCanonicalLocales(tag)[0];
  } catch {
    return null;
  }
  if (!canonical || canonical.length > MAX_TAG_LENGTH || !TAG_PATTERN.test(canonical) || isBuiltIn(canonical)) return null;
  return canonical;
}

/** The locale for dates, numbers and plural rules. A tag the engine does not know falls back to English formatting. */
export function localeOf(language: Language): string {
  if (language === "tr") return "tr-TR";
  if (language === "en") return "en-US";
  try {
    return Intl.getCanonicalLocales(language)[0] ?? "en-US";
  } catch {
    return "en-US";
  }
}

/** Languages written right to left (the language subtag). Used only to warn before an import; the layout is not adapted. */
const RIGHT_TO_LEFT = new Set(["ar", "he", "fa", "ur", "ps", "sd", "ug", "yi", "dv", "ckb"]);
export function isRightToLeft(tag: string): boolean {
  return RIGHT_TO_LEFT.has(tag.split("-")[0]!.toLowerCase());
}
