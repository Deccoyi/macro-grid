import { useCallback } from "react";
import type { Page, PageTreeNode } from "@macro/renderer";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { tempId } from "./tempId";
import { findNode, flattenLeafIds, insertNode, isSelfOrDescendant, removeNode } from "./tree";
import type { ProfileDocument } from "./useProfileDocument";

/** Page-level edits of the open profile: add, rename, delete, grid settings and duplication. */
export function usePageActions({ profile, currentPageId, mutate, mutatePage, markPageDirty, setCurrentPageId, setSelectedIds }: ProfileDocument) {
  const { t } = useT();

  const addPage = useCallback(() => {
    const page: Page = { id: tempId("page"), name: t("state.defaultPageName", String((profile?.pages.length ?? 0) + 1)), cols: 4, rows: 3, gap: 10, widgets: [] };
    mutate((draft) => ({ ...draft, pages: [...draft.pages, page] }));
    markPageDirty(page.id);
    setCurrentPageId(page.id);
  }, [mutate, markPageDirty, profile, setCurrentPageId, t]);

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
      markPageDirty(clone.id);
      setCurrentPageId(clone.id);
      setSelectedIds([]);
    },
    [mutate, markPageDirty, profile, setCurrentPageId, setSelectedIds, t],
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
        markPageDirty(clone.id);
        setCurrentPageId(clone.id);
        setSelectedIds([]);
      } else {
        await api.saveProfile({ ...target, pages: [...target.pages, clone] });
      }
    },
    [mutate, markPageDirty, profile, setCurrentPageId, setSelectedIds],
  );

  /** Ctrl+V of a page copied with Ctrl+C (see clipboard.ts) — into whichever profile is currently open,
   * which is what makes "paste into the same profile" and "paste into a different profile" the same
   * action: copy, switch profiles (or don't), paste. Always a fresh copy (id, every widget's id), same as
   * duplicatePage. `parentFolderId` lands it inside the folder the Hierarchy selection points at (see
   * WorkspaceUiContext's treeSelection), or at the root when nothing was selected. */
  const pastePage = useCallback(
    (page: Page, parentFolderId: string | null = null) => {
      if (!profile) return;
      const clone: Page = {
        ...structuredClone(page),
        id: tempId("page"),
        name: `${page.name} ${t("state.duplicateSuffix")}`,
        widgets: structuredClone(page.widgets).map((w) => ({ ...w, id: tempId("widget") })),
      };
      mutate((draft) => ({
        ...draft,
        pages: [...draft.pages, clone],
        pageTree: insertNode(draft.pageTree ?? [], { type: "page", id: clone.id }, parentFolderId, Number.MAX_SAFE_INTEGER),
      }));
      markPageDirty(clone.id);
      setCurrentPageId(clone.id);
      setSelectedIds([]);
    },
    [mutate, markPageDirty, profile, setCurrentPageId, setSelectedIds, t],
  );

  /** New page folder in the open profile's tree — `mutate` re-normalizes on every change, so this only
   * ever needs to touch `pageTree` itself. */
  const createPageFolder = useCallback(
    (parentFolderId: string | null, name: string) => {
      const folder: PageTreeNode = { type: "folder", id: tempId("pgfolder"), name, children: [] };
      mutate((draft) => ({ ...draft, pageTree: insertNode(draft.pageTree ?? [], folder, parentFolderId, Number.MAX_SAFE_INTEGER) }));
    },
    [mutate],
  );

  const renamePageFolder = useCallback(
    (id: string, name: string) => {
      mutate((draft) => {
        const rename = (nodes: PageTreeNode[]): PageTreeNode[] =>
          nodes.map((n) => (n.id === id ? { ...n, name } : n.children ? { ...n, children: rename(n.children) } : n));
        return { ...draft, pageTree: rename(draft.pageTree ?? []) };
      });
    },
    [mutate],
  );

  /** `keepContents` re-parents the folder's children to its own parent; otherwise its pages are deleted
   * along with it (refused if that would leave the profile with no pages — the same invariant deletePage
   * enforces). The caller is expected to have already confirmed this with the user. */
  const deletePageFolder = useCallback(
    (id: string, keepContents: boolean) => {
      if (!profile) return;
      const tree = profile.pageTree ?? [];
      const target = findNode(tree, id);
      if (!target) return;
      if (!keepContents) {
        const removedPageIds = new Set(flattenLeafIds(target.children ?? [], "page"));
        if (removedPageIds.size >= profile.pages.length) return;
      }
      mutate((draft) => {
        const draftTree = draft.pageTree ?? [];
        const draftTarget = findNode(draftTree, id);
        const { tree: without } = removeNode(draftTree, id);
        if (keepContents && draftTarget?.children) {
          return { ...draft, pageTree: [...without, ...draftTarget.children] };
        }
        const removedPageIds = new Set(flattenLeafIds(draftTarget?.children ?? [], "page"));
        return { ...draft, pageTree: without, pages: draft.pages.filter((p) => !removedPageIds.has(p.id)) };
      });
      if (!keepContents) {
        const removedPageIds = new Set(flattenLeafIds(target.children ?? [], "page"));
        if (currentPageId && removedPageIds.has(currentPageId)) {
          const remaining = profile.pages.filter((p) => !removedPageIds.has(p.id));
          setCurrentPageId(remaining[0]?.id ?? null);
        }
      }
    },
    [profile, mutate, currentPageId, setCurrentPageId],
  );

  /** Moves a page or page folder to `targetFolderId` (null: root) at `index` — the same operation for a
   * right-click "Move to folder..." and for dropping it there in the tree. Refuses moving a folder into
   * itself or one of its own descendants. */
  const movePageTreeNode = useCallback(
    (id: string, targetFolderId: string | null, index: number) => {
      mutate((draft) => {
        const tree = draft.pageTree ?? [];
        if (targetFolderId && isSelfOrDescendant(tree, id, targetFolderId)) return draft;
        const { tree: without, removed } = removeNode(tree, id);
        if (!removed) return draft;
        return { ...draft, pageTree: insertNode(without, removed, targetFolderId, index) };
      });
    },
    [mutate],
  );

  /** Ctrl+V of a whole page folder copied with Ctrl+C — recreates it (and every page and widget inside it,
   * recursively) with fresh ids, the same "always a fresh copy" rule as pastePage/duplicatePage. `pages`
   * is the snapshot of every page that was inside the folder when it was copied (clipboard.ts): the source
   * profile might not even be open any more by the time this runs. */
  const pastePageFolder = useCallback(
    (folder: PageTreeNode, pages: Page[], parentFolderId: string | null = null) => {
      if (!profile) return;
      const pagesById = new Map(pages.map((p) => [p.id, p]));
      const clone = (node: PageTreeNode): { node: PageTreeNode; newPages: Page[] } => {
        if (node.type === "page") {
          const source = pagesById.get(node.id);
          if (!source) return { node: { type: "page", id: node.id }, newPages: [] };
          const page: Page = { ...structuredClone(source), id: tempId("page"), widgets: structuredClone(source.widgets).map((w) => ({ ...w, id: tempId("widget") })) };
          return { node: { type: "page", id: page.id }, newPages: [page] };
        }
        const children: PageTreeNode[] = [];
        const newPages: Page[] = [];
        for (const child of node.children ?? []) {
          const result = clone(child);
          children.push(result.node);
          newPages.push(...result.newPages);
        }
        return { node: { type: "folder", id: tempId("pgfolder"), name: node.name, children }, newPages };
      };
      const { node, newPages } = clone(folder);
      mutate((draft) => ({
        ...draft,
        pages: [...draft.pages, ...newPages],
        pageTree: insertNode(draft.pageTree ?? [], node, parentFolderId, Number.MAX_SAFE_INTEGER),
      }));
    },
    [profile, mutate],
  );

  return {
    addPage, renamePage, deletePage, setPageGrid, setPageGap, setPagePadding, setPageAlignment, duplicatePage,
    duplicatePageToProfile, pastePage, pastePageFolder, createPageFolder, renamePageFolder, deletePageFolder, movePageTreeNode,
  };
}
