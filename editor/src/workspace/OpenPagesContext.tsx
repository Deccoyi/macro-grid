import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useEditorStateContext } from "../state/EditorStateContext";

export interface OpenPagesApi {
  /** Every page id currently open as a Document tab. Never empty while a profile is loaded — closeTab
   * refuses to drop the last one, so "no open page" only ever happens transiently while a profile loads. */
  openPageIds: string[];
  closeTab: (pageId: string) => void;
}

const OpenPagesContext = createContext<OpenPagesApi | null>(null);

/** Lifted out of DocumentArea so the Toolbox (a sibling tool window, not a descendant of DocumentArea) can
 * also see "is any page tab actually open" and disable its add-widget buttons when it isn't — the bug
 * being fixed here is that closing the last tab (previously possible via the middle-click shortcut, which
 * had no floor unlike the X button) left state.currentPageId still pointing at a page with no visible tab,
 * so Toolbox kept adding widgets to a page nothing on screen represented as open. */
export function OpenPagesProvider({ children }: { children: ReactNode }) {
  const state = useEditorStateContext();
  const { currentPageId, profile } = state;
  const [openPageIds, setOpenPageIds] = useState<string[]>(() => (currentPageId ? [currentPageId] : []));

  useEffect(() => {
    if (currentPageId) setOpenPageIds((ids) => (ids.includes(currentPageId) ? ids : [...ids, currentPageId]));
  }, [currentPageId]);

  useEffect(() => {
    if (!profile) return;
    const validIds = new Set(profile.pages.map((p) => p.id));
    setOpenPageIds((ids) => {
      const next = ids.filter((id) => validIds.has(id));
      return next.length === ids.length ? ids : next;
    });
  }, [profile]);

  const closeTab = (pageId: string) => {
    setOpenPageIds((ids) => {
      if (ids.length <= 1) return ids; // always keep at least one tab open
      const idx = ids.indexOf(pageId);
      const next = ids.filter((id) => id !== pageId);
      if (pageId === currentPageId && next.length > 0) {
        // Activate the right neighbour, else the left one — same rule the plan gives for tabs.
        const neighbour = next[Math.min(idx, next.length - 1)] ?? next[0]!;
        state.setCurrentPageId(neighbour);
        state.setSelectedIds([]);
      }
      return next;
    });
  };

  return <OpenPagesContext.Provider value={{ openPageIds, closeTab }}>{children}</OpenPagesContext.Provider>;
}

export function useOpenPages(): OpenPagesApi {
  const value = useContext(OpenPagesContext);
  if (!value) throw new Error("useOpenPages() used outside <OpenPagesProvider>");
  return value;
}
