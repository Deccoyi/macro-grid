import type { Page, PageTreeNode, Profile, Widget } from "@macro/renderer";
import type { ProfileTreeNode } from "../api/types";

/** A plain module-level singleton, not React state or the OS clipboard: Ctrl+C/Ctrl+V need to survive
 * switching pages and profiles (the editor only ever holds one profile in memory at a time), and nothing
 * in the UI needs to re-render just because the clipboard's contents changed. A folder entry carries the
 * folder's own tree node (ids only) alongside the actual Page/Profile objects for everything inside it —
 * pasting recreates the whole subtree with fresh ids, including into a different profile. */
export type ClipboardEntry =
  | { kind: "widgets"; widgets: Widget[] }
  | { kind: "page"; page: Page }
  | { kind: "profile"; profile: Profile }
  | { kind: "pageFolder"; folder: PageTreeNode; pages: Page[] }
  | { kind: "profileFolder"; folder: ProfileTreeNode; profiles: Profile[] };

let current: ClipboardEntry | null = null;

export function setClipboard(entry: ClipboardEntry): void {
  current = entry;
}

export function getClipboard(): ClipboardEntry | null {
  return current;
}
