export const MAX_TEXT_LENGTH = 2000;

export type SanitizeResult =
  | { status: "ok"; text: string }
  /** Nothing is left after cleaning: the row counts as not translated. */
  | { status: "empty" }
  | { status: "refused"; reason: "notText" | "tooLong" | "brokenSurrogate" };

const LONE_SURROGATE = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/;
// C0 and C1 controls except the line breaks and the tab that are handled first.
const CONTROLS = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]/g;
// Zero width space, byte order mark, the embedding and override characters (and the line and paragraph separators, handled as breaks).
const HIDDEN = /[\u200B\uFEFF\u202A-\u202E]/g;

/**
 * The one cleaner for every text that comes from outside: a CSV cell or a string of a pack read back from the server (the file can
 * also be edited by hand). It refuses what it cannot make safe (never cuts) and removes what only hides or confuses. A line break is kept
 * only when the English text of the key has one; U+200C to U+200F stay because several scripts need them. It is on purpose wider than
 * the server's own text cleaner.
 */
export function sanitizeText(value: unknown, english?: string): SanitizeResult {
  if (typeof value !== "string") return { status: "refused", reason: "notText" };
  if (value.length > MAX_TEXT_LENGTH * 2) return { status: "refused", reason: "tooLong" };

  let text = value.normalize("NFC");
  if (LONE_SURROGATE.test(text)) return { status: "refused", reason: "brokenSurrogate" };

  const allowBreaks = english?.includes("\n") ?? false;
  text = text.replace(/\r\n|\r|\u2028|\u2029|\n/g, allowBreaks ? "\n" : " ").replace(/\t/g, " ");
  text = text.replace(CONTROLS, "").replace(HIDDEN, "");
  text = balancedIsolates(text);
  text = text.trim();

  if (text.length > MAX_TEXT_LENGTH) return { status: "refused", reason: "tooLong" };
  return text.length === 0 ? { status: "empty" } : { status: "ok", text };
}

/** The direction isolates U+2066 to U+2069 are kept only when every opener has its closer in order; otherwise all of them go. */
function balancedIsolates(text: string): string {
  let depth = 0;
  for (const ch of text) {
    if (ch === "\u2066" || ch === "\u2067" || ch === "\u2068") depth++;
    else if (ch === "\u2069" && --depth < 0) return text.replace(/[\u2066-\u2069]/g, "");
  }
  return depth === 0 ? text : text.replace(/[\u2066-\u2069]/g, "");
}
