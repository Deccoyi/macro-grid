import { useEffect, useState } from "react";
import type { DictKey } from "../i18n/tr";

/** A short line of feedback in the status bar ("3 widgets did not fit", "This change cannot be undone").
 * The key and its arguments are stored, not translated text, so it follows a language switch. */
export interface StatusNotice {
  key: DictKey;
  args: string[];
  /** Bumped on every notice, so the same message twice in a row restarts the timeout. */
  seq: number;
}

/** How long a notice stays in the status bar. */
const NOTICE_MS = 6000;

let current: StatusNotice | null = null;
let seq = 0;
let timer: ReturnType<typeof setTimeout> | null = null;
const listeners = new Set<(notice: StatusNotice | null) => void>();

function emit() {
  for (const fn of listeners) fn(current);
}

/** Module-level, like dialogStore: plain hooks (the profile tree, the widget actions) can report
 * something without being wired to the status bar component. */
export function showStatusNotice(key: DictKey, ...args: string[]): void {
  seq += 1;
  current = { key, args, seq };
  if (timer) clearTimeout(timer);
  timer = setTimeout(() => {
    current = null;
    timer = null;
    emit();
  }, NOTICE_MS);
  emit();
}

export function useStatusNotice(): StatusNotice | null {
  const [notice, setNotice] = useState<StatusNotice | null>(current);
  useEffect(() => {
    listeners.add(setNotice);
    return () => { listeners.delete(setNotice); };
  }, []);
  return notice;
}
