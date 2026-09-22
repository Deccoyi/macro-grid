import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ActionBinding, Page, Profile, Widget, WidgetType } from "@macro/renderer";
import { api } from "../api/client";
import type { ActionInfo, ProfileSummary, VariableInfo, VariableSnapshot } from "../api/types";
import { findFreeCell } from "../grid/collision";

let nextTempId = 1;
/** Client-generated ids only ever need to be unique within this editing session; the server assigns real ones on first save of a brand new widget/page... */
function tempId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${nextTempId++}`;
}

export function useEditorState() {
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [currentPageId, setCurrentPageId] = useState<string | null>(null);
  const [selectedWidgetId, setSelectedWidgetId] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [variables, setVariables] = useState<VariableSnapshot>({});
  const [actions, setActions] = useState<ActionInfo[]>([]);
  const [variableCatalog, setVariableCatalog] = useState<VariableInfo[]>([]);

  const refreshVariables = useCallback(() => {
    api.variablesSnapshot().then(setVariables).catch(() => {});
  }, []);

  const loadProfileList = useCallback(async (selectId?: string) => {
    const list = await api.listProfiles();
    setProfiles(list);
    const id = selectId ?? list[0]?.id;
    if (id) {
      const full = await api.getProfile(id);
      setProfile(full);
      setCurrentPageId(full.pages[0]?.id ?? null);
      setSelectedWidgetId(null);
      setDirty(false);
    }
  }, []);

  useEffect(() => {
    loadProfileList().catch((e) => setError(String(e)));
    api.listActions().then(setActions).catch(() => {});
    api.variableCatalog().then(setVariableCatalog).catch(() => {});
    refreshVariables();
  }, [loadProfileList, refreshVariables]);

  // Auto-poll so the canvas's dynamic-style preview (evaluateWidgetDynamicStyle) actually reacts to
  // live values like system.cpu instead of only updating when the user clicks "Değişkenleri yenile" —
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
    () => !dirty || confirm("Kaydedilmemiş değişiklikler kaybolacak. Devam edilsin mi?"),
    [dirty],
  );

  const currentPage = useMemo<Page | null>(
    () => profile?.pages.find((p) => p.id === currentPageId) ?? null,
    [profile, currentPageId],
  );

  const selectedWidget = useMemo<Widget | null>(
    () => currentPage?.widgets.find((w) => w.id === selectedWidgetId) ?? null,
    [currentPage, selectedWidgetId],
  );

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
      if (!confirmDiscardIfDirty()) return;
      const full = await api.getProfile(id);
      setProfile(full);
      setCurrentPageId(full.pages[0]?.id ?? null);
      setSelectedWidgetId(null);
      setDirty(false);
    },
    [confirmDiscardIfDirty],
  );

  const createProfile = useCallback(async () => {
    if (!confirmDiscardIfDirty()) return;
    const created = await api.createProfile();
    await loadProfileList(created.id);
  }, [confirmDiscardIfDirty, loadProfileList]);

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
    const page: Page = { id: tempId("page"), name: `Sayfa ${((profile?.pages.length ?? 0) + 1)}`, cols: 4, rows: 3, widgets: [] };
    mutate((draft) => ({ ...draft, pages: [...draft.pages, page] }));
    setCurrentPageId(page.id);
  }, [mutate, profile]);

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

  // ---- widgets ----

  const addWidget = useCallback(
    (type: WidgetType) => {
      if (!currentPage) return;
      const cell = findFreeCell(1, 1, currentPage.widgets, currentPage.cols, currentPage.rows);
      if (!cell) {
        setError("Sayfada boş hücre kalmadı.");
        return;
      }
      const widget: Widget = {
        id: tempId("widget"),
        type,
        x: cell.x,
        y: cell.y,
        w: 1,
        h: 1,
        text: defaultTextFor(type),
        style: {},
        actions: {},
      };
      mutatePage(currentPage.id, (p) => p.widgets.push(widget));
      setSelectedWidgetId(widget.id);
    },
    [currentPage, mutatePage],
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
      setSelectedWidgetId((sel) => (sel === widgetId ? null : sel));
    },
    [currentPage, mutatePage],
  );

  return {
    profiles,
    profile,
    currentPage,
    currentPageId,
    selectedWidget,
    selectedWidgetId,
    dirty,
    saving,
    error,
    clearError: () => setError(null),
    variables,
    actions,
    variableCatalog,
    refreshVariables,
    setCurrentPageId,
    setSelectedWidgetId,
    selectProfile,
    createProfile,
    deleteProfile,
    renameProfile,
    save,
    addPage,
    renamePage,
    deletePage,
    setPageGrid,
    addWidget,
    updateWidget,
    setWidgetRect,
    setWidgetActions,
    deleteWidget,
  };
}

function defaultTextFor(type: WidgetType): string {
  switch (type) {
    case "button": return "Buton";
    case "toggle": return "Toggle";
    case "label": return "Etiket";
    case "slider": return "Slider";
    case "knob": return "Knob";
    case "image": return "";
    case "web": return "Web";
    case "plugin-html": return "Plugin";
    default: return "";
  }
}
