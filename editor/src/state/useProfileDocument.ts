import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { AppMatch, Page, Profile, Widget } from "@macro/renderer";
import { api } from "../api/client";
import type { ProfileSummary } from "../api/types";
import { choiceAsync, confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";

/**
 * The profile being edited: the profile list, the in-memory copy of the open profile, the current page and
 * selection, the dirty flag, and the profile-level operations (open, create, import, delete, rename, save).
 * Page and widget edits are built on top of `mutate` / `mutatePage` (see usePageActions / useWidgetActions).
 */
export function useProfileDocument() {
  const { t } = useT();
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [currentPageId, setCurrentPageId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /** Makes `full` the open profile: first page, no selection, nothing unsaved. */
  const openProfile = useCallback((full: Profile) => {
    setProfile(full);
    setCurrentPageId(full.pages[0]?.id ?? null);
    setSelectedIds([]);
    setDirty(false);
  }, []);

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

  // Kept in a ref (not just the `dirty` state) so the beforeunload listener below always reads the
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

  const toggleSelected = useCallback((id: string) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  }, []);

  /** Applies `fn` to the in-memory profile and marks it dirty; the server is never touched until Save. */
  const mutate = useCallback((fn: (draft: Profile) => Profile) => {
    setProfile((prev) => (prev ? fn(structuredClone(prev)) : prev));
    setDirty(true);
  }, []);

  const mutatePage = useCallback(
    (pageId: string, fn: (page: Page) => void) => {
      mutate((draft) => {
        const page = draft.pages.find((p) => p.id === pageId);
        if (page) fn(page);
        return draft;
      });
    },
    [mutate],
  );

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
    },
    [loadProfileList],
  );

  const renameProfile = useCallback(
    (name: string) => mutate((draft) => ({ ...draft, name })),
    [mutate],
  );

  /** Remembers which "Preview" preset this profile should open with — see Profile.PreviewDeviceId. */
  const setPreviewDevice = useCallback(
    (id: string) => mutate((draft) => ({ ...draft, previewDeviceId: id === "free" ? undefined : id })),
    [mutate],
  );

  /** Foreground-window auto-switch rules for this profile — see docs/design/auto-profile-switch.md. */
  const setAppMatches = useCallback(
    (appMatches: AppMatch[]) => mutate((draft) => ({ ...draft, appMatches })),
    [mutate],
  );

  const save = useCallback(async () => {
    if (!profile) return;
    setSaving(true);
    setError(null);
    try {
      await api.saveProfile(profile);
      setDirty(false);
      setProfiles((prev) => prev.map((p) => (p.id === profile.id ? { ...p, name: profile.name } : p)));
    } catch (e) {
      setError(String(e));
    } finally {
      setSaving(false);
    }
  }, [profile]);

  return {
    profiles,
    profile,
    currentPage,
    currentPageId,
    selectedWidget,
    selectedWidgets,
    selectedIds,
    dirty,
    saving,
    error,
    setError,
    setCurrentPageId,
    setSelectedIds,
    toggleSelected,
    mutate,
    mutatePage,
    selectProfile,
    createProfile,
    importProfileFromJson,
    deleteProfile,
    renameProfile,
    setPreviewDevice,
    setAppMatches,
    save,
  };
}

/** What the page and widget hooks need from the profile document. */
export type ProfileDocument = ReturnType<typeof useProfileDocument>;
