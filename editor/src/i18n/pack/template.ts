import type { LanguagePackFile } from "../../api/types";
import { en } from "../en";
import { isCritical } from "../critical";
import { MAX_ROWS, MAX_NAME_LENGTH, englishFor, validateEntry, type RefusalReason } from "./validate";
import { sanitizeText } from "./sanitizeText";
import { CSV_COLUMNS, protectCell, readCsv, unprotectCell, writeCsv } from "./csv";

/** The pack format this editor writes. */
export const PACK_FORMAT = 1;

/** A short stable hash (FNV-1a) of an English text, stored per row so a later change of the English text marks the row "needs review". */
export function sourceHash(english: string): string {
  let hash = 0x811c9dc5;
  for (let i = 0; i < english.length; i++) {
    hash ^= english.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash.toString(16);
}

export const ENGLISH_KEYS: readonly string[] = Object.keys(en);

/** How many of the English texts a pack translates, and how many of those were written for an older English text. */
export function coverage(strings: Record<string, string>, sources: Record<string, string> | undefined): { done: number; total: number; needsReview: number } {
  let done = 0;
  let needsReview = 0;
  for (const key of ENGLISH_KEYS) {
    if (!Object.hasOwn(strings, key)) continue;
    done++;
    const stamp = sources && Object.hasOwn(sources, key) ? sources[key] : undefined;
    if (stamp !== undefined && stamp !== sourceHash(englishFor(key)!)) needsReview++;
  }
  return { done, total: ENGLISH_KEYS.length, needsReview };
}

function noteFor(key: string): string {
  return isCritical(key) ? "Shown with the English text next to it" : "";
}

function maxLengthFor(english: string): number {
  return Math.min(2000, Math.max(40, english.length * 3));
}

/** The table a translator fills in: one row per English text, with the pack's current translation when there is one. */
export function buildTemplate(existing?: Record<string, string>): string {
  const rows: string[][] = [[...CSV_COLUMNS]];
  for (const key of ENGLISH_KEYS) {
    const english = englishFor(key)!;
    const translation = existing && Object.hasOwn(existing, key) ? existing[key]! : "";
    rows.push([protectCell(key), protectCell(english), protectCell(translation), noteFor(key), String(maxLengthFor(english))]);
  }
  return writeCsv(rows);
}

export interface RefusedRow {
  line: number;
  key: string;
  reason: RefusalReason | "shape";
}

export interface ImportReport {
  /** Rows with a translation that passed the check. */
  accepted: number;
  /** Rows left empty (not translated). */
  empty: number;
  refused: RefusedRow[];
}

export type ImportOutcome =
  | { ok: true; pack: LanguagePackFile; report: ImportReport }
  | { ok: false; reason: "notATable" | "noHeader" | "tooManyRows" };

/**
 * Reads a filled-in table into a pack. Only the `key` and `translation` columns are read (the English column is ignored, so a table edited
 * from an older template still maps by key). A row that fails the check is listed in the report and left out, never repaired.
 */
export function importTable(text: string, tag: string, name: string, version: number): ImportOutcome {
  const rows = readCsv(text);
  if (!rows || rows.length === 0) return { ok: false, reason: "notATable" };
  if (rows.length > MAX_ROWS + 1) return { ok: false, reason: "tooManyRows" };

  const header = rows[0]!.map((cell) => cell.trim().toLowerCase());
  const keyColumn = header.indexOf("key");
  const translationColumn = header.indexOf("translation");
  if (keyColumn < 0 || translationColumn < 0) return { ok: false, reason: "noHeader" };

  const strings: Record<string, string> = {};
  const sources: Record<string, string> = {};
  const report: ImportReport = { accepted: 0, empty: 0, refused: [] };
  const seen = new Set<string>();

  rows.slice(1).forEach((row, index) => {
    const line = index + 2;
    const key = unprotectCell((row[keyColumn] ?? "").trim());
    if (key === "") return;
    if (row.length <= translationColumn || seen.has(key)) {
      report.refused.push({ line, key, reason: "shape" });
      return;
    }
    seen.add(key);
    const entry = validateEntry(key, unprotectCell(row[translationColumn] ?? ""));
    if (entry.status === "ok") {
      strings[key] = entry.text;
      sources[key] = sourceHash(englishFor(key)!);
      report.accepted++;
    } else if (entry.status === "empty") report.empty++;
    else report.refused.push({ line, key, reason: entry.reason });
  });

  const cleanName = sanitizeText(name);
  const packName = (cleanName.status === "ok" ? cleanName.text : tag).slice(0, MAX_NAME_LENGTH);
  return { ok: true, pack: { meta: { format: PACK_FORMAT, tag, name: packName, version }, strings, sources }, report };
}
