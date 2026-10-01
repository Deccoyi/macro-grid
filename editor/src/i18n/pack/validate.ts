import { en } from "../en";
import { PARAMS } from "../params";
import type { DictKey } from "../tr";
import { sanitizeText } from "./sanitizeText";

export const PLURAL_CATEGORIES = ["zero", "one", "two", "few", "many", "other"] as const;
export const MAX_ROWS = 5000;
export const MAX_NAME_LENGTH = 40;

export type RefusalReason =
  | "notText"
  | "tooLong"
  | "brokenSurrogate"
  | "unknownKey"
  | "placeholders"
  | "pluralBase";

export type EntryResult =
  | { status: "ok"; text: string }
  | { status: "empty" }
  | { status: "refused"; reason: RefusalReason };

const TOKEN = /\{([A-Za-z][A-Za-z0-9]*)\}/g;

export function tokensOf(text: string): Set<string> {
  return new Set([...text.matchAll(TOKEN)].map((m) => m[1]!));
}

/** The plural categories a pack may add to a base beyond the `.one` and `.other` rows the editor ships. */
function splitPlural(key: string): { base: string; category: string } | null {
  const dot = key.lastIndexOf(".");
  if (dot < 0) return null;
  const category = key.slice(dot + 1);
  return (PLURAL_CATEGORIES as readonly string[]).includes(category) ? { base: key.slice(0, dot), category } : null;
}

/** True for a dictionary key, and for a plural row of a base that has an `.other` row. */
export function isKnownKey(key: string): boolean {
  if (Object.hasOwn(en, key)) return true;
  const plural = splitPlural(key);
  return plural !== null && Object.hasOwn(en, `${plural.base}.other`);
}

/** The English text a row is translated from (a plural row without its own English text is translated from the `.other` text). */
export function englishFor(key: string): string | undefined {
  if (Object.hasOwn(en, key)) return en[key as DictKey];
  const plural = splitPlural(key);
  return plural && Object.hasOwn(en, `${plural.base}.other`) ? en[`${plural.base}.other` as DictKey] : undefined;
}

/**
 * The one check for a row, used on a CSV cell at import and on every string of a pack at load. The key must exist; the text is cleaned
 * (see sanitizeText); the `{name}` placeholders must be the same set as in the English text, so a dropped or mistyped one refuses the row.
 * A plural row for another category than `.other` (a language may need `.few`) may leave a placeholder out, since "one" can be worded without the number.
 */
export function validateEntry(key: string, value: unknown): EntryResult {
  if (!isKnownKey(key)) return { status: "refused", reason: "unknownKey" };
  const english = englishFor(key)!;
  const cleaned = sanitizeText(value, english);
  if (cleaned.status !== "ok") return cleaned;

  const wanted = tokensOf(english);
  const found = tokensOf(cleaned.text);
  const plural = splitPlural(key);
  if (plural && plural.category !== "other") {
    const declared = new Set(PARAMS[`${plural.base}.other` as DictKey] ?? []);
    for (const name of found) if (!declared.has(name)) return { status: "refused", reason: "placeholders" };
    return cleaned;
  }
  if (wanted.size !== found.size || [...wanted].some((name) => !found.has(name))) return { status: "refused", reason: "placeholders" };
  return cleaned;
}

export interface CheckedPack {
  tag: string;
  name: string;
  version: number;
  /** Only rows that passed the check. Created without a prototype, so a key such as `__proto__` is just a name. */
  strings: Record<string, string>;
  rejected: number;
}

/** Checks a pack read from the server: the container shape, then every string with validateEntry. Rows that fail are left out (English shows instead). */
export function checkPack(raw: unknown, tag: string): CheckedPack | null {
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return null;
  const { meta, strings } = raw as { meta?: unknown; strings?: unknown };
  if (typeof meta !== "object" || meta === null || typeof strings !== "object" || strings === null || Array.isArray(strings)) return null;

  const m = meta as { tag?: unknown; name?: unknown; version?: unknown };
  if (typeof m.tag !== "string" || m.tag.toLowerCase() !== tag.toLowerCase()) return null;

  const name = sanitizeText(m.name);
  const result: Record<string, string> = Object.create(null);
  let rejected = 0;
  let rows = 0;
  for (const key of Object.keys(strings)) {
    if (++rows > MAX_ROWS) { rejected++; continue; }
    const entry = validateEntry(key, (strings as Record<string, unknown>)[key]);
    if (entry.status === "ok") result[key] = entry.text;
    else if (entry.status === "refused") rejected++;
  }

  return {
    tag,
    name: name.status === "ok" ? name.text.slice(0, MAX_NAME_LENGTH) : tag,
    version: typeof m.version === "number" && Number.isFinite(m.version) ? m.version : 1,
    strings: result,
    rejected,
  };
}
