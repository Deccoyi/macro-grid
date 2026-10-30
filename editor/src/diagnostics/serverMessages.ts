import type { DictKey } from "../i18n/tr";

/** The server writes its problem texts in English. These are the ones the editor knows and shows translated (the others stay as written). */
const KNOWN: { pattern: RegExp; key: DictKey; groups?: number }[] = [
  { pattern: /^The variable '(.+)' does not exist\.$/, key: "diag.server.variableMissing" },
  { pattern: /^This rule uses the variable '(.+)', which does not exist\.$/, key: "diag.server.ruleVariableMissing" },
  { pattern: /^A rule cannot press keys or type text by itself\. Remove this step from the rule\.$/, key: "diag.server.ruleKeys" },
  { pattern: /^This rule was started too often, so some starts were dropped\.$/, key: "diag.server.ruleDropped" },
  { pattern: /^This rule kept starting too often and was switched off\. Check what starts it, then switch it on again\.$/, key: "diag.server.ruleSwitchedOff" },
  { pattern: /^The server was stopping\.$/, key: "diag.server.stopping" },
];

/** The dictionary key and values for a server message the editor can translate, or null. */
export function translateServerMessage(message: string): { key: DictKey; args: string[] } | null {
  for (const { pattern, key } of KNOWN) {
    const m = pattern.exec(message);
    if (m) return { key, args: m.slice(1) };
  }
  return null;
}
