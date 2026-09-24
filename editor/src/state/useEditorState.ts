import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ActionBinding, AppMatch, Page, Profile, Widget, WidgetType } from "@macro/renderer";
import { api } from "../api/client";
import type { ActionInfo, ProfileSummary, StatusEntry, VariableInfo, VariableSnapshot } from "../api/types";
import { choiceAsync, confirmAsync } from "../dialogs/dialogStore";
import { findFreeCell } from "../grid/collision";
import { useT } from "../i18n/I18nContext";
import { usePreferences } from "../preferences/PreferencesContext";
import { invalidateIconPacks } from "../panels/IconPicker";

let nextTempId = 1;
/** Client-generated ids only ever need to be unique within this editing session; the server assigns real ones on first save of a brand new widget/page... */
function tempId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${nextTempId++}`;
}

export function useEditorState() {
  const { t } = useT();
  const { savedLanguage } = usePreferences();
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [currentPageId, setCurrentPageId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [variables, setVariables] = useState<VariableSnapshot>({});
  const [actions, setActions] = useState<ActionInfo[]>([]);
  const [variableCatalog, setVariableCatalog] = useState<VariableInfo[]>([]);
  const [status, setStatus] = useState<StatusEntry[]>([]);

  const refreshVariables = useCallback(() => {
    api.variablesSnapshot().then(setVariables).catch(() => {});
    api.getStatus().then(setStatus).catch(() => {});
  }, []);

  const refreshCatalogs = useCallback(() => {
    invalidateIconPacks();
    api.listActions().then(setActions).catch(() => {});
    api.variableCatalog().then(setVariableCatalog).catch(() => {});
  }, []);

  const loadProfileList = useCallback(async (selectId?: string) => {
    const list = await api.listProfiles();
    setProfiles(list);
    const id = selectId ?? list[0]?.id;
    if (id) {
      const full = await api.getProfile(id);
      setProfile(full);
      setCurrentPageId(full.pages[0]?.id ?? null);
      setSelectedIds([]);
      setDirty(false);
    }
  }, []);

  useEffect(() => {
    loadProfileList().catch((e) => setError(String(e)));
    refreshCatalogs();
    refreshVariables();
  }, [loadProfileList, refreshVariables, refreshCatalogs]);

  // Plugin texts are translated by the server, so a saved language change needs a fresh copy of them.
  useEffect(() => {
    refreshCatalogs();
    refreshVariables();
  }, [savedLanguage, refreshCatalogs, refreshVariables]);

  // Plugins are installed / reloaded / removed live from the separate "Eklentiler" window, which changes the
  // action list, variable picker and icon packs. Coming back to this window is the cheap moment to refetch.
  useEffect(() => {
    window.addEventListener("focus", refreshCatalogs);
    return () => window.removeEventListener("focus", refreshCatalogs);
  }, [refreshCatalogs]);

  // Auto-poll so the canvas's dynamic-style preview (evaluateWidgetDynamicStyle) actually reacts to
  // live values like system.cpu instead of only updating when the user clicks "Refresh variables" —
  // this is a one-shot REST snapshot, not a push, since the editor isn't a real device session.
  useEffect(() => {
    const timer = setInterval(refreshVariables, 2000);
    return () => clearInterval(timer);
  }, [refreshVariables]);

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

  // ---- profile-level ----

  const selectProfile = useCallback(
    async (id: string) => {
      if (!(await confirmDiscardIfDirty())) return;
      const full = await api.getProfile(id);
      setProfile(full);
      setCurrentPageId(full.pages[0]?.id ?? null);
      setSelectedIds([]);
      setDirty(false);
    },
    [confirmDiscardIfDirty],
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

  /** Foreground-window auto-switch rules for this profile — see docs/auto-profile-switch.md. */
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

  // ---- pages ----

  const addPage = useCallback(() => {
    const page: Page = { id: tempId("page"), name: t("state.defaultPageName", String((profile?.pages.length ?? 0) + 1)), cols: 4, rows: 3, gap: 10, widgets: [] };
    mutate((draft) => ({ ...draft, pages: [...draft.pages, page] }));
    setCurrentPageId(page.id);
  }, [mutate, profile, t]);

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
    [mutate, profile, currentPageId],
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
    [mutate, profile, t],
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
    [mutate, profile],
  );

  // ---- widgets ----

  const addWidget = useCallback(
    (type: WidgetType) => {
      if (!currentPage) return;
      const cell = findFreeCell(1, 1, currentPage.widgets, currentPage.cols, currentPage.rows);
      if (!cell) {
        setError(t("state.noFreeCell"));
        return;
      }
      const widget: Widget = {
        id: tempId("widget"),
        type,
        x: cell.x,
        y: cell.y,
        w: 1,
        h: 1,
        text: defaultTextFor(type, t),
        style: {},
        actions: {},
      };
      mutatePage(currentPage.id, (p) => p.widgets.push(widget));
      setSelectedIds([widget.id]);
    },
    [currentPage, mutatePage, t],
  );

  const updateWidget = useCallback(
    (widgetId: string, fn: (widget: Widget) => void) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => {
        const widget = p.widgets.find((w) => w.id === widgetId);
        if (widget) fn(widget);
      });
    },
    [currentPage, mutatePage],
  );

  const setWidgetRect = useCallback(
    (widgetId: string, rect: { x: number; y: number; w: number; h: number }) =>
      updateWidget(widgetId, (w) => Object.assign(w, rect)),
    [updateWidget],
  );

  const setWidgetActions = useCallback(
    (widgetId: string, event: string, bindings: ActionBinding[]) =>
      updateWidget(widgetId, (w) => {
        if (bindings.length === 0) delete (w.actions as Record<string, unknown>)[event];
        else (w.actions as Record<string, ActionBinding[]>)[event] = bindings;
      }),
    [updateWidget],
  );

  const deleteWidget = useCallback(
    (widgetId: string) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => w.id !== widgetId); });
      setSelectedIds((sel) => sel.filter((id) => id !== widgetId));
    },
    [currentPage, mutatePage],
  );

  const deleteSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !selectedIds.includes(w.id)); });
    setSelectedIds([]);
  }, [currentPage, mutatePage, selectedIds]);

  const duplicateSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    const clones = selectedWidgets.map((w) => {
      const cell = findFreeCell(w.w, w.h, currentPage.widgets, currentPage.cols, currentPage.rows);
      return { ...structuredClone(w), id: tempId("widget"), x: cell?.x ?? w.x, y: cell?.y ?? w.y };
    });
    mutatePage(currentPage.id, (p) => p.widgets.push(...clones));
    setSelectedIds(clones.map((c) => c.id));
  }, [currentPage, mutatePage, selectedIds, selectedWidgets]);

  /** Copies (or moves) the given widgets onto another page, in this profile or a different one. A
   * cross-profile target is fetched and saved directly (see duplicatePageToProfile above — the editor
   * never holds two profiles in memory at once). */
  const moveOrCopyWidgets = useCallback(
    async (widgetIds: string[], targetProfileId: string, targetPageId: string, mode: "move" | "copy") => {
      if (!currentPage || !profile) return;
      const widgets = currentPage.widgets.filter((w) => widgetIds.includes(w.id));
      if (widgets.length === 0) return;
      const clones = widgets.map((w) => ({ ...structuredClone(w), id: tempId("widget") }));

      if (targetProfileId === profile.id) {
        mutate((draft) => {
          const targetPage = draft.pages.find((p) => p.id === targetPageId);
          if (targetPage) targetPage.widgets.push(...clones);
          if (mode === "move") {
            const sourcePage = draft.pages.find((p) => p.id === currentPage.id);
            if (sourcePage) sourcePage.widgets = sourcePage.widgets.filter((w) => !widgetIds.includes(w.id));
          }
          return draft;
        });
      } else {
        const target = await api.getProfile(targetProfileId);
        const targetPage = target.pages.find((p) => p.id === targetPageId);
        if (targetPage) {
          targetPage.widgets.push(...clones);
          await api.saveProfile(target);
        }
        if (mode === "move") {
          mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !widgetIds.includes(w.id)); });
        }
      }
      setSelectedIds([]);
    },
    [currentPage, profile, mutate, mutatePage],
  );

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
    clearError: () => setError(null),
    variables,
    actions,
    variableCatalog,
    status,
    refreshVariables,
    setCurrentPageId,
    setSelectedIds,
    toggleSelected,
    selectProfile,
    createProfile,
    importProfileFromJson,
    deleteProfile,
    renameProfile,
    setPreviewDevice,
    setAppMatches,
    save,
    addPage,
    renamePage,
    deletePage,
    setPageGrid,
    setPageGap,
    setPagePadding,
    setPageAlignment,
    duplicatePage,
    duplicatePageToProfile,
    addWidget,
    updateWidget,
    setWidgetRect,
    setWidgetActions,
    deleteWidget,
    deleteSelectedWidgets,
    duplicateSelectedWidgets,
    moveOrCopyWidgets,
  };
}

function defaultTextFor(type: WidgetType, t: ReturnType<typeof useT>["t"]): string {
  switch (type) {
    case "button": return t("widget.type.button");
    case "toggle": return t("widget.type.toggle");
    case "label": return t("widget.type.label");
    case "slider": return t("widget.type.slider");
    case "knob": return t("widget.type.knob");
    case "image": return "";
    case "web": return t("widget.type.web");
    case "plugin-html": return t("widget.type.plugin-html");
    default: return "";
  }
}
