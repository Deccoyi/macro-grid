import { en, type MessageKey } from "./en";
import { tr } from "./tr";

/** The browser client speaks Turkish when the browser language is Turkish and English otherwise. */
const turkish = typeof navigator !== "undefined" && navigator.language.toLowerCase().startsWith("tr");

export function t(key: MessageKey): string {
  return turkish ? tr[key] : en[key];
}
