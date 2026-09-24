import { useCallback } from "react";
import type { Page } from "@macro/renderer";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { tempId } from "./tempId";
import type { ProfileDocument } from "./useProfileDocument";

/** Page-level edits of the open profile: add, rename, delete, grid settings and duplication. */
export function usePageActions({ profile, currentPageId, mutate, mutatePage, setCurrentPageId, setSelectedIds }: ProfileDocument) {
  const { t } = useT();

  const addPage = useCallback(() => {
    const page: Page = { id: tempId("page"), name: t("state.defaultPageName", String((profile?.pages.length ?? 0) + 1)), cols: 4, rows: 3, gap: 10, widgets: [] };
    mutate((draft) => ({ ...draft, pages: [...draft.pages, page] }));
    setCurrentPageId(page.id);
  }, [mutate, profile, setCurrentPageId, t]);

  const renamePage = useCallback((pageId: string, name: string) => mutatePage(pageId, (p) => { p.name = name; }), [mutatePage]);

  const deletePage = useCallback(
    (pageId: string) => {
      if (!profile || profile.pages.length <= 1) return;
      mutate((draft) => ({ ...draft, pages: draft.pages.filter((p) => p.id !== pageId) }));
      if (currentPageId === pageId) {
        const remaining = profile.pages.filter((p) => p.id !== pageId);
        setCurrentPageId(remaining[0]?.id ?? null);
      }
    },
    [mutate, profile, currentPageId, setCurrentPageId],
  );

  const setPageGrid = useCallback(
    (pageId: string, cols: number, rows: number) => mutatePage(pageId, (p) => { p.cols = cols; p.rows = rows; }),
    [mutatePage],
  );

  const setPageGap = useCallback(
    (pageId: string, gap: number) => mutatePage(pageId, (p) => { p.gap = gap; }),
    [mutatePage],
  );

  const setPagePadding = useCallback(
    (pageId: string, padding: number) => mutatePage(pageId, (p) => { p.padding = padding; }),
    [mutatePage],
  );

  const setPageAlignment = useCallback(
    (pageId: string, alignment: "start" | "center" | "end") => mutatePage(pageId, (p) => { p.alignment = alignment; }),
    [mutatePage],
  );

  const duplicatePage = useCallback(
    (pageId: string) => {
      const source = profile?.pages.find((p) => p.id === pageId);
      if (!source) return;
      const clone: Page = {
        ...structuredClone(source),
        id: tempId("page"),
        name: `${source.name} ${t("state.duplicateSuffix")}`,
        widgets: structuredClone(source.widgets).map((w) => ({ ...w, id: tempId("widget") })),
      };
      mutate((draft) => ({ ...draft, pages: [...draft.pages, clone] }));
      setCurrentPageId(clone.id);
      setSelectedIds([]);
    },
    [mutate, profile, setCurrentPageId, setSelectedIds, t],
  );

  /** Deep-clones the page (and a fresh id for every one of its widgets) into another profile, fetched
   * and saved directly via the API since the editor only ever holds one profile in memory at a time. */
  const duplicatePageToProfile = useCallback(
    async (pageId: string, targetProfileId: string) => {
      const source = profile?.pages.find((p) => p.id === pageId);
      if (!source) return;
      const target = targetProfileId === profile?.id ? profile : await api.getProfile(targetProfileId);
      if (!target) return;
      const clone: Page = {
        ...structuredClone(source),
        id: tempId("page"),
        widgets: structuredClone(source.widgets).map((w) => ({ ...w, id: tempId("widget") })),
      };
      if (targetProfileId === profile?.id) {
        mutate((draft) => ({ ...draft, pages: [...draft.pages, clone] }));
        setCurrentPageId(clone.id);
        setSelectedIds([]);
      } else {
        await api.saveProfile({ ...target, pages: [...target.pages, clone] });
      }
    },
    [mutate, profile, setCurrentPageId, setSelectedIds],
  );

  return { addPage, renamePage, deletePage, setPageGrid, setPageGap, setPagePadding, setPageAlignment, duplicatePage, duplicatePageToProfile };
}
