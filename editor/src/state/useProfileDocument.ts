import { useCallback, useEffect, useMemo, useRef, useState, type SetStateAction } from "react";
import type { AppMatch, Page, Profile, Widget } from "@macro/renderer";
import { api } from "../api/client";
import type { ProfileSummary } from "../api/types";
import { choiceAsync, confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import {
  createHistory, isDirty, markSaved, recordChange, redo as redoHistory, redoLabel as historyRedoLabel,
  undo as undoHistory, undoLabel as historyUndoLabel, type History,
} from "./history";
import { normalizePageTree, reorderPagesByTree } from "./pageTree";
import { showStatusNotice } from "./statusNotice";

/** What one undo step restores: the profile, the page that was showing and the widget selection. */
export interface DocumentSnapshot {
  profile: Profile;
  currentPageId: string | null;
  selectedIds: string[];
}

/** Every change to the open profile names its undo step; changes that fire repeatedly (typing, dragging a
 * slider, moving a widget) also pass a `coalesceKey`, so a burst of them becomes one step. */
export interface MutateOptions {
  label?: DictKey;
  coalesceKey?: string;
}

const DEFAULT_LABEL: DictKey = "undo.edit";

/**
 * The profile being edited: the profile list, the in-memory copy of the open profile, the current page and
 * selection, the undo history, the dirty flag, and the profile-level operations (open, create, import,
 * delete, rename, save). Page and widget edits are built on top of `mutate` / `mutatePage` (see
 * usePageActions / useWidgetActions) — the one place the open profile changes, so the one place undo
 * snapshots are taken (docs/design/editor-edit-commands.md, "Undo / redo").
 */
export function useProfileDocument() {
  const { t } = useT();
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfileState] = useState<Profile | null>(null);
  const [currentPageId, setCurrentPageIdState] = useState<string | null>(null);
  const [selectedIds, setSelectedIdsState] = useState<string[]>([]);
  const [history, setHistoryState] = useState<History<DocumentSnapshot>>(createHistory);
  // The profile as last opened or saved: what the Hierarchy tree's "*" markers compare against.
  const [savedProfile, setSavedProfile] = useState<Profile | null>(null);
  /** How many times the profile was saved in this session; the Diagnostic Messages show a summary after each one. */
  const [saveCount, setSaveCount] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Refs mirror the document state synchronously: two mutate() calls in one event (a drag that swaps two
  // widgets) must each see the other's result, and each must snapshot exactly what was showing before it.
  const profileRef = useRef<Profile | null>(null);
  const currentPageIdRef = useRef<string | null>(null);
  const selectedIdsRef = useRef<string[]>([]);
  const historyRef = useRef<History<DocumentSnapshot>>(history);

  const setProfile = useCallback((next: Profile | null) => {
    profileRef.current = next;
    setProfileState(next);
  }, []);

  const setCurrentPageId = useCallback((value: SetStateAction<string | null>) => {
    const next = typeof value === "function" ? value(currentPageIdRef.current) : value;
    currentPageIdRef.current = next;
    setCurrentPageIdState(next);
  }, []);

  const setSelectedIds = useCallback((value: SetStateAction<string[]>) => {
    const next = typeof value === "function" ? value(selectedIdsRef.current) : value;
    selectedIdsRef.current = next;
    setSelectedIdsState(next);
  }, []);

  const setHistory = useCallback((next: History<DocumentSnapshot>) => {
    historyRef.current = next;
    setHistoryState(next);
  }, []);

  const dirty = isDirty(history);

  /** Makes `full` the open profile: first page, no selection, nothing unsaved, no undo history (another
   * profile opened, a reload from the server and an import all come through here). Normalizes the page
   * tree against the actual pages on the way in — an old profile saved before folders existed, or one
   * whose tree drifted somehow, opens with a repaired flat/folder arrangement rather than carrying the
   * drift forward. */
  const openProfile = useCallback((full: Profile) => {
    const pageTree = normalizePageTree(full.pageTree, full.pages);
    const normalized: Profile = { ...full, pageTree, pages: reorderPagesByTree(full.pages, pageTree) };
    setProfile(normalized);
    setSavedProfile(normalized);
    setCurrentPageId(normalized.pages[0]?.id ?? null);
    setSelectedIds([]);
    setHistory(createHistory());
  }, [setProfile, setCurrentPageId, setSelectedIds, setHistory]);

  const loadProfileList = useCallback(async (selectId?: string) => {
    const list = await api.listProfiles();
    setProfiles(list);
    const id = selectId ?? list[0]?.id;
    if (id) {
      const full = await api.getProfile(id);
      openProfile(full);
    }
  }, [openProfile]);

  useEffect(() => {
    loadProfileList().catch((e) => setError(String(e)));
  }, [loadProfileList]);

  // Kept in a ref (not just the `dirty` value) so the beforeunload listener below always reads the
  // current value without having to re-subscribe the listener on every dirty change.
  const dirtyRef = useRef(dirty);
  dirtyRef.current = dirty;

  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (!dirtyRef.current) return;
      e.preventDefault();
      e.returnValue = "";
    };
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, []);

  /** True if it's OK to discard the current in-memory profile (asks first when there are unsaved edits). */
  const confirmDiscardIfDirty = useCallback(
    () => !dirty || confirmAsync(t("dialog.discardChanges"), { danger: true }),
    [dirty, t],
  );

  const currentPage = useMemo<Page | null>(
    () => profile?.pages.find((p) => p.id === currentPageId) ?? null,
    [profile, currentPageId],
  );

  const selectedWidgets = useMemo<Widget[]>(
    () => (currentPage ? currentPage.widgets.filter((w) => selectedIds.includes(w.id)) : []),
    [currentPage, selectedIds],
  );

  const selectedWidget = useMemo<Widget | null>(
    () => (selectedWidgets.length === 1 ? selectedWidgets[0]! : null),
    [selectedWidgets],
  );

  // Finer-grained than `dirty` (which just says "the document differs from what was saved"): which pages,
  // and whether the profile's own settings (name, auto-switch rules, preview device), differ from the last
  // opened or saved copy — purely for the Hierarchy tree's "*" markers. Derived by comparison rather than
  // tracked per change, so undoing an edit clears its marker too.
  const dirtyPageIds = useMemo<Set<string>>(() => {
    const ids = new Set<string>();
    if (!profile || !dirty) return ids;
    const savedPages = new Map((savedProfile?.pages ?? []).map((p) => [p.id, p]));
    for (const page of profile.pages) {
      const saved = savedPages.get(page.id);
      if (!saved || (saved !== page && JSON.stringify(saved) !== JSON.stringify(page))) ids.add(page.id);
    }
    return ids;
  }, [profile, savedProfile, dirty]);

  const profileDirty = useMemo(() => {
    if (!profile || !savedProfile || !dirty) return false;
    const settings = (p: Profile) => JSON.stringify({ name: p.name, appMatches: p.appMatches, previewDeviceId: p.previewDeviceId });
    return settings(profile) !== settings(savedProfile);
  }, [profile, savedProfile, dirty]);

  const toggleSelected = useCallback((id: string) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  }, [setSelectedIds]);

  const snapshot = useCallback((p: Profile): DocumentSnapshot => ({
    profile: p,
    currentPageId: currentPageIdRef.current,
    selectedIds: selectedIdsRef.current,
  }), []);

  /** Applies `fn` to the in-memory profile, records the previous state as an undo step and marks the
   * document dirty; the server is never touched until Save. A change that leaves the profile exactly as it
   * was (dropping an item onto its own place) records nothing. Re-normalizes the page tree against
   * whatever `fn` left `pages` as (a page added, removed or pasted elsewhere in the state layer never has
   * to touch `pageTree` itself — it's kept in sync here, once) and reorders `pages` to match. */
  const mutate = useCallback((fn: (draft: Profile) => Profile, options: MutateOptions = {}) => {
    const prev = profileRef.current;
    if (!prev) return;
    const draft = fn(structuredClone(prev));
    const pageTree = normalizePageTree(draft.pageTree, draft.pages);
    const next: Profile = { ...draft, pageTree, pages: reorderPagesByTree(draft.pages, pageTree) };
    if (JSON.stringify(next) === JSON.stringify(prev)) return;
    setHistory(recordChange(historyRef.current, snapshot(prev), options.label ?? DEFAULT_LABEL, {
      coalesceKey: options.coalesceKey,
      now: Date.now(),
    }));
    setProfile(next);
  }, [setHistory, setProfile, snapshot]);

  const mutatePage = useCallback(
    (pageId: string, fn: (page: Page) => void, options?: MutateOptions) => {
      mutate((draft) => {
        const page = draft.pages.find((p) => p.id === pageId);
        if (page) fn(page);
        return draft;
      }, options);
    },
    [mutate],
  );

  /** Shows a history snapshot: its profile, its page (the first page if that one no longer exists) and
   * its selection, minus any widget that is not on that page any more. */
  const restore = useCallback((s: DocumentSnapshot) => {
    setProfile(s.profile);
    const page = s.profile.pages.find((p) => p.id === s.currentPageId) ?? s.profile.pages[0] ?? null;
    setCurrentPageId(page?.id ?? null);
    const widgetIds = new Set(page?.widgets.map((w) => w.id) ?? []);
    setSelectedIds(s.selectedIds.filter((id) => widgetIds.has(id)));
  }, [setProfile, setCurrentPageId, setSelectedIds]);

  const undo = useCallback(() => {
    const current = profileRef.current;
    if (!current) return;
    const result = undoHistory(historyRef.current, snapshot(current));
    if (!result) return;
    setHistory(result.history);
    restore(result.state);
  }, [restore, setHistory, snapshot]);

  const redo = useCallback(() => {
    const current = profileRef.current;
    if (!current) return;
    const result = redoHistory(historyRef.current, snapshot(current));
    if (!result) return;
    setHistory(result.history);
    restore(result.state);
  }, [restore, setHistory, snapshot]);

  const canUndo = history.past.length > 0;
  const canRedo = history.future.length > 0;
  const undoLabel = historyUndoLabel(history);
  const redoLabel = historyRedoLabel(history);

  const selectProfile = useCallback(
    async (id: string) => {
      if (!(await confirmDiscardIfDirty())) return;
      const full = await api.getProfile(id);
      openProfile(full);
    },
    [confirmDiscardIfDirty, openProfile],
  );

  const createProfile = useCallback(async () => {
    if (!(await confirmDiscardIfDirty())) return;
    const created = await api.createProfile();
    await loadProfileList(created.id);
    showStatusNotice("undo.notUndoable");
  }, [confirmDiscardIfDirty, loadProfileList]);

  /** "File > Import Profile": normally creates a brand-new profile then overwrites it with the
   * imported content under the new id. If a profile with the same name already exists the user is asked
   * (never decided silently): "Overwrite" replaces that profile's content in place (keeping its id),
   * "Rename" imports as a new profile named name_1, name_2, ... and Cancel aborts the import. */
  const importProfileFromJson = useCallback(
    async (data: Profile) => {
      if (!(await confirmDiscardIfDirty())) return;
      const originalName = data.name || t("profile.defaultName");
      const existing = profiles.find((p) => p.name === originalName);
      let name = originalName;

      if (existing) {
        const choice = await choiceAsync(
          t("profile.importConflict", originalName),
          [
            { value: "overwrite", label: t("profile.importOverwrite"), danger: true },
            { value: "rename", label: t("profile.importRename"), primary: true },
          ],
          { title: t("profile.importConflictTitle") },
        );
        if (choice === null) return;

        if (choice === "overwrite") {
          await api.saveProfile({ ...data, id: existing.id, name: originalName });
          await loadProfileList(existing.id);
          return;
        }

        const existingNames = new Set(profiles.map((p) => p.name));
        let suffix = 1;
        while (existingNames.has(name)) {
          name = `${originalName}_${suffix}`;
          suffix += 1;
        }
      }

      const created = await api.createProfile();
      await api.saveProfile({ ...data, id: created.id, name });
      await loadProfileList(created.id);
    },
    [confirmDiscardIfDirty, loadProfileList, profiles, t],
  );

  const deleteProfile = useCallback(
    async (id: string) => {
      await api.deleteProfile(id);
      await loadProfileList();
      showStatusNotice("undo.notUndoable");
    },
    [loadProfileList],
  );

  /** Refreshes just the profile summary list (the Hierarchy tree's profile names/ids) without touching
   * the open profile — unlike loadProfileList(), which opens one. Used after a profile is created or
   * changed by something other than "open it" (pasting a profile or a profile folder). */
  const refreshProfileList = useCallback(async () => {
    setProfiles(await api.listProfiles());
  }, []);

  const renameProfile = useCallback(
    (name: string) => mutate((draft) => ({ ...draft, name }), { label: "undo.renameProfile" }),
    [mutate],
  );

  /** Remembers which "Preview" preset this profile should open with — see Profile.PreviewDeviceId. */
  const setPreviewDevice = useCallback(
    (id: string) => mutate((draft) => ({ ...draft, previewDeviceId: id === "free" ? undefined : id }), { label: "undo.previewDevice" }),
    [mutate],
  );

  /** Foreground-window auto-switch rules for this profile — see docs/design/auto-profile-switch.md. */
  const setAppMatches = useCallback(
    (appMatches: AppMatch[]) => mutate((draft) => ({ ...draft, appMatches }), { label: "undo.appMatches", coalesceKey: "profile:appMatches" }),
    [mutate],
  );

  const save = useCallback(async () => {
    const toSave = profileRef.current;
    if (!toSave) return;
    const savedId = historyRef.current.currentId;
    setSaving(true);
    setError(null);
    try {
      await api.saveProfile(toSave);
      setHistory(markSaved(historyRef.current, savedId));
      setSavedProfile(toSave);
      setSaveCount((n) => n + 1);
      setProfiles((prev) => prev.map((p) => (p.id === toSave.id ? { ...p, name: toSave.name } : p)));
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  }, [setHistory]);

  return {
    profiles,
    profile,
    currentPage,
    currentPageId,
    selectedWidget,
    selectedWidgets,
    selectedIds,
    dirty,
    dirtyPageIds,
    profileDirty,
    saving,
    error,
    setError,
    saveCount,
    setCurrentPageId,
    setSelectedIds,
    toggleSelected,
    mutate,
    mutatePage,
    undo,
    redo,
    canUndo,
    canRedo,
    undoLabel,
    redoLabel,
    selectProfile,
    createProfile,
    importProfileFromJson,
    deleteProfile,
    refreshProfileList,
    renameProfile,
    setPreviewDevice,
    setAppMatches,
    save,
  };
}

/** What the page and widget hooks need from the profile document. */
export type ProfileDocument = ReturnType<typeof useProfileDocument>;
