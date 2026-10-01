import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import type { AddPanelOptions, DockviewApi, IDockviewPanel } from "dockview-react";
import { useT } from "../i18n/I18nContext";
import { usePreferences } from "../preferences/PreferencesContext";
import { DEFAULT_LAYOUT_JSON } from "./defaultLayout";
import { TOOL_WINDOWS } from "./toolWindows";

/** The editor's own opaque envelope stored in a DockLayoutProfile's `layoutJson` (or in preferences'
 * dockLayoutJson for the "last used" layout): dockview's own toJSON() only knows about the docked tree, so
 * which tool windows are auto-hidden — off the dockview tree entirely, tracked only by WorkspaceContext —
 * has to be captured alongside it or a saved layout would silently drop its auto-hidden panels. */
interface StoredLayout {
  dockview: unknown;
  openIds: string[];
  autoHiddenIds: string[];
}

export interface WorkspaceApi {
  /** Ids currently open somewhere (docked or auto-hidden) — what PanelHost mounts content for. */
  openIds: string[];
  autoHiddenIds: string[];
  openFlyoutId: string | null;
  setOpenFlyoutId: (id: string | null) => void;
  isOpen: (id: string) => boolean;
  isAutoHidden: (id: string) => boolean;
  /** Registered by DockWorkspace once the library is ready; everything below is a no-op before that. */
  registerApi: (api: DockviewApi) => void;
  /** The one-time default layout build, called from DockWorkspace's onReady. */
  buildDefaultLayout: (api: DockviewApi) => void;
  /** Docked panel → its edge's auto-hide rail. */
  unpin: (id: string) => void;
  /** Auto-hidden panel → back to its default placement in the layout. */
  pin: (id: string) => void;
  /** View menu: reopen if closed, open the flyout if auto-hidden, focus if already docked. */
  toggleFromMenu: (id: string) => void;
  /** Makes sure a tool window is visible and in front: reopens it if closed, opens its flyout if auto-hidden, activates its tab if docked. Never closes. */
  bringForward: (id: string) => void;
  /** The current arrangement as a string, for saving into a DockLayoutProfile or as the "last used"
   * layout. Null before the dockview instance is ready. */
  serializeLayout: () => string | null;
  /** Replaces the whole workspace with a previously-serialized arrangement. */
  applyLayout: (json: string) => void;
  /** The one layout that isn't a saved profile: rebuilt from code, always available. */
  resetToDefaultLayout: () => void;
}

const WorkspaceContext = createContext<WorkspaceApi | null>(null);

/** Owns the dockview instance's open/closed/auto-hidden bookkeeping above both MenuBar (the View menu)
 * and DockWorkspace (the actual dockview tree) — they're siblings under App.tsx, so this can't live
 * inside DockWorkspace itself the way earlier phases had it; the View menu needs to read and drive the
 * same state a sibling component owns. See docs/design/docking-workspace.md, the useWorkspace() this
 * was always meant to be. */
export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const { t } = useT();
  const prefs = usePreferences();
  const apiRef = useRef<DockviewApi | null>(null);
  const autoHidingRef = useRef<Set<string>>(new Set());
  // Set around a programmatic api.clear()/fromJSON() so the onDidRemovePanel listener doesn't mistake the
  // old layout's panels being torn down for the user closing them one by one.
  const resettingRef = useRef(false);
  // "pending" until the saved dockLayoutJson (or its absence) is known, so the very first render's default
  // layout can't autosave over a not-yet-fetched preference and erase it.
  const startupRef = useRef<"pending" | "done">("pending");
  const autosaveTimerRef = useRef<number | null>(null);

  const [openIds, setOpenIds] = useState(() => TOOL_WINDOWS.filter((tw) => tw.defaultOpen).map((tw) => tw.id));
  const [autoHiddenIds, setAutoHiddenIds] = useState<string[]>([]);
  const [openFlyoutId, setOpenFlyoutId] = useState<string | null>(null);

  const addToolWindow = useCallback(
    (id: string, extra?: Partial<AddPanelOptions>): IDockviewPanel | undefined => {
      const def = TOOL_WINDOWS.find((tw) => tw.id === id)!;
      return apiRef.current?.addPanel({
        id,
        component: "toolWindow",
        title: t(def.titleKey),
        params: { toolWindowId: id },
        minimumWidth: def.minSize.width,
        minimumHeight: def.minSize.height,
        ...extra,
      } as AddPanelOptions);
    },
    [t],
  );

  const ensureDocument = useCallback((api: DockviewApi) => {
    if (api.getPanel("document")) return;
    const anchor = api.getPanel("hierarchy") ?? api.getPanel("toolbox") ?? api.getPanel("properties");
    const documentGroup = anchor
      ? api.addGroup({ referenceGroup: anchor.group, direction: "right", hideHeader: true })
      : api.addGroup({ direction: "right", hideHeader: true });
    api.addPanel({ id: "document", component: "document", title: "Document", position: { referenceGroup: documentGroup, direction: "within" } });
  }, []);

  const registerApi = useCallback(
    (api: DockviewApi) => {
      apiRef.current = api;
      api.onDidRemovePanel((removed) => {
        if (resettingRef.current) return; // a programmatic clear()/fromJSON(), not the user closing panels
        if (removed.id === "document") { ensureDocument(api); return; }
        if (!TOOL_WINDOWS.some((tw) => tw.id === removed.id)) return;
        if (autoHidingRef.current.delete(removed.id)) return; // an unpin, not a close: still open, just off-tree
        setOpenIds((ids) => ids.filter((id) => id !== removed.id));
      });
      api.onDidLayoutChange(() => scheduleAutosaveRef.current());
    },
    [ensureDocument],
  );

  // Applies the built-in Default layout (see defaultLayout.ts) via the same fromJSON path a saved profile
  // uses, rather than reconstructing it panel-by-panel: an earlier version built it with addPanel/addGroup
  // calls and got hierarchy's width wrong, because dockview's initialWidth is a no-op on a panel that's
  // still alone in an empty grid — which is exactly what building "from scratch" requires. Replaying a
  // previously-serialized tree doesn't have that problem, and it's what a saved profile does anyway.
  const buildDefaultLayout = useCallback((api: DockviewApi) => {
    let stored: StoredLayout;
    try {
      stored = JSON.parse(DEFAULT_LAYOUT_JSON) as StoredLayout;
    } catch {
      return;
    }
    resettingRef.current = true;
    try {
      api.fromJSON(stored.dockview as Parameters<DockviewApi["fromJSON"]>[0]);
    } finally {
      resettingRef.current = false;
    }
    setOpenIds(stored.openIds ?? []);
    setAutoHiddenIds(stored.autoHiddenIds ?? []);
  }, []);

  const unpin = useCallback((id: string) => {
    const panel = apiRef.current?.getPanel(id);
    if (!panel) return;
    autoHidingRef.current.add(id);
    panel.api.close();
    setAutoHiddenIds((ids) => (ids.includes(id) ? ids : [...ids, id]));
  }, []);

  // Re-docks a panel at its registry defaultPlacement, not just "wherever dockview feels like" (its
  // fallback is the active group) — mirrors buildDefaultLayout's own construction logic, relative to
  // whatever's still on the tree.
  const dockAtDefaultPlacement = useCallback(
    (id: string) => {
      const api = apiRef.current;
      if (!api) return;
      const sizeOf = (twId: string) => TOOL_WINDOWS.find((tw) => tw.id === twId)!.defaultPlacement.size;

      if (id === "errorList") {
        const errorGroup = api.addGroup({ direction: "below" });
        addToolWindow(id, { position: { referenceGroup: errorGroup, direction: "within" }, initialHeight: sizeOf(id) });
      } else if (id === "properties") {
        const doc = api.getPanel("document");
        addToolWindow(id, doc ? { position: { referenceGroup: doc.group, direction: "right" }, initialWidth: sizeOf(id) } : undefined);
      } else if (id === "toolbox") {
        const hierarchy = api.getPanel("hierarchy");
        addToolWindow(id, hierarchy ? { position: { referenceGroup: hierarchy.group, direction: "below" }, initialHeight: sizeOf(id) } : undefined);
      } else if (id === "hierarchy") {
        const toolbox = api.getPanel("toolbox");
        const doc = api.getPanel("document");
        addToolWindow(
          id,
          toolbox ? { position: { referenceGroup: toolbox.group, direction: "above" } }
            : doc ? { position: { referenceGroup: doc.group, direction: "left" }, initialWidth: sizeOf(id) }
            : undefined,
        );
      } else {
        addToolWindow(id);
      }
    },
    [addToolWindow],
  );

  const pin = useCallback(
    (id: string) => {
      setAutoHiddenIds((ids) => ids.filter((x) => x !== id));
      setOpenFlyoutId((cur) => (cur === id ? null : cur));
      dockAtDefaultPlacement(id);
    },
    [dockAtDefaultPlacement],
  );

  // A real open/close toggle for the View menu's checkable items: already open (docked OR auto-hidden) →
  // close it outright, closed → reopen at its default placement. Previously an already-docked panel just
  // got focused instead of closing, so clicking a checked item in the menu appeared to do nothing.
  const toggleFromMenu = useCallback(
    (id: string) => {
      if (autoHiddenIds.includes(id)) {
        setAutoHiddenIds((ids) => ids.filter((x) => x !== id));
        setOpenIds((ids) => ids.filter((x) => x !== id));
        setOpenFlyoutId((cur) => (cur === id ? null : cur));
        return;
      }
      const panel = apiRef.current?.getPanel(id);
      if (panel) { panel.api.close(); return; } // triggers onDidRemovePanel, which drops it from openIds
      setOpenIds((ids) => (ids.includes(id) ? ids : [...ids, id]));
      dockAtDefaultPlacement(id);
    },
    [autoHiddenIds, dockAtDefaultPlacement],
  );

  const bringForward = useCallback(
    (id: string) => {
      if (autoHiddenIds.includes(id)) { setOpenFlyoutId(id); return; }
      const panel = apiRef.current?.getPanel(id);
      if (panel) { panel.api.setActive(); return; }
      setOpenIds((ids) => (ids.includes(id) ? ids : [...ids, id]));
      dockAtDefaultPlacement(id);
    },
    [autoHiddenIds, dockAtDefaultPlacement],
  );

  const serializeLayout = useCallback((): string | null => {
    const api = apiRef.current;
    if (!api) return null;
    const stored: StoredLayout = { dockview: api.toJSON(), openIds, autoHiddenIds };
    return JSON.stringify(stored);
  }, [openIds, autoHiddenIds]);

  const applyLayout = useCallback((json: string) => {
    const api = apiRef.current;
    if (!api) return;
    let stored: StoredLayout;
    try {
      stored = JSON.parse(json) as StoredLayout;
    } catch {
      return;
    }
    resettingRef.current = true;
    try {
      api.fromJSON(stored.dockview as Parameters<DockviewApi["fromJSON"]>[0]);
    } finally {
      resettingRef.current = false;
    }
    setOpenFlyoutId(null);
    setAutoHiddenIds(stored.autoHiddenIds ?? []);
    setOpenIds(stored.openIds ?? []);
  }, []);

  const resetToDefaultLayout = useCallback(() => {
    const api = apiRef.current;
    if (!api) return;
    setOpenFlyoutId(null);
    buildDefaultLayout(api); // fromJSON replaces the whole tree and sets openIds/autoHiddenIds itself
  }, [buildDefaultLayout]);

  // A ref indirection so registerApi's one-time api.onDidLayoutChange subscription (registered once,
  // when dockview first becomes ready) always calls the CURRENT scheduleAutosave — which closes over
  // openIds/autoHiddenIds and prefs and would otherwise go stale the moment either changes.
  const scheduleAutosaveRef = useRef<() => void>(() => {});
  const scheduleAutosave = useCallback(() => {
    if (startupRef.current !== "done") return; // don't overwrite a not-yet-restored saved layout
    if (autosaveTimerRef.current !== null) window.clearTimeout(autosaveTimerRef.current);
    autosaveTimerRef.current = window.setTimeout(() => {
      const json = serializeLayout();
      if (json) prefs.setDockLayoutJson(json);
    }, 600);
  }, [serializeLayout, prefs]);
  scheduleAutosaveRef.current = scheduleAutosave;

  // Once preferences have loaded, swap the default layout DockWorkspace's onReady already built for the
  // user's last saved arrangement, if there is one — and only then start autosaving, so a slow fetch can
  // never race a fresh default layout into overwriting what was actually saved.
  useEffect(() => {
    if (startupRef.current === "done" || !prefs.loaded) return;
    if (apiRef.current && prefs.dockLayoutJson) applyLayout(prefs.dockLayoutJson);
    startupRef.current = "done";
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [prefs.loaded, prefs.dockLayoutJson]);

  // Auto-hide/pin/close/reopen change openIds/autoHiddenIds without touching the dockview tree itself
  // (so onDidLayoutChange won't fire for them) — autosave those too, on the same debounce.
  useEffect(() => {
    scheduleAutosave();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openIds, autoHiddenIds]);

  const value = useMemo<WorkspaceApi>(
    () => ({
      openIds,
      autoHiddenIds,
      openFlyoutId,
      setOpenFlyoutId,
      isOpen: (id) => openIds.includes(id),
      isAutoHidden: (id) => autoHiddenIds.includes(id),
      registerApi,
      buildDefaultLayout,
      unpin,
      pin,
      toggleFromMenu,
      bringForward,
      serializeLayout,
      applyLayout,
      resetToDefaultLayout,
    }),
    [
      openIds, autoHiddenIds, openFlyoutId, registerApi, buildDefaultLayout, unpin, pin, toggleFromMenu,
      serializeLayout, applyLayout, resetToDefaultLayout, bringForward,
    ],
  );

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspace(): WorkspaceApi {
  const value = useContext(WorkspaceContext);
  if (!value) throw new Error("useWorkspace() used outside <WorkspaceProvider>");
  return value;
}
