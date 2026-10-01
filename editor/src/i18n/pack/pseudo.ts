import { en } from "../en";
import { PSEUDO_TAG } from "../language";
import { checkPack, type CheckedPack } from "./validate";

const ACCENTS: Record<string, string> = {
  a: "á", e: "é", i: "í", o: "ö", u: "ü", c: "ç", n: "ñ", y: "ý", s: "š", z: "ž",
  A: "Å", E: "É", I: "Î", O: "Ø", U: "Û", C: "Ç", N: "Ñ", Y: "Ý", S: "Š", Z: "Ž",
};

/** English with accented letters, about a third longer and inside brackets, with every `{name}` kept: shows what a longer translation does to a layout. */
export function pseudoize(text: string): string {
  const pieces = text.split(/(\{[A-Za-z][A-Za-z0-9]*\})/);
  const accented = pieces.map((piece) => (piece.startsWith("{") && piece.endsWith("}") ? piece : [...piece].map((ch) => ACCENTS[ch] ?? ch).join(""))).join("");
  const padding = "·".repeat(Math.ceil(text.length / 3));
  return `[${accented}${padding}]`;
}

/** The pseudo pack, built in the editor (development builds only offer it) and never sent to the server. */
export function buildPseudoPack(): CheckedPack {
  const strings: Record<string, string> = {};
  for (const [key, text] of Object.entries(en)) strings[key] = pseudoize(text);
  return checkPack({ meta: { tag: PSEUDO_TAG, name: "Pseudo", version: 1 }, strings }, PSEUDO_TAG)!;
}
