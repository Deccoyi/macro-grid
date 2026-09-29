import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type { AppPreferences, DockLayoutProfile, PreviewProfileInfo } from "../api/types";

export type Theme = "dark" | "light";
export type Language = "tr" | "en";
type PreviewProfile = PreviewProfileInfo;

const DEFAULTS: AppPreferences = {
  theme: "dark",
  language: navigator.language.toLowerCase().startsWith("tr") ? "tr" : "en",
  previewProfiles: [],
  collapsedInspectorSections: {},
  defaultProfileId: null,
  launchMode: "window",
  autostartMode: "tray",
  checkForUpdates: true,
  includePreReleases: true,
  dockLayoutJson: "",
  dockLayoutProfiles: [],
};

interface PreferencesContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  language: Language;
  /** The language the server has stored. Text the server translates (plugin names, action lists, ...) has to be refetched when this changes. */
  savedLanguage: Language;
  setLanguage: (language: Language) => void;
  previewProfiles: PreviewProfile[];
  addPreviewProfile: (p: Omit<PreviewProfile, "id">) => void;
  removePreviewProfile: (id: string) => void;
  collapsedInspectorSections: Record<string, boolean>;
  setInspectorSectionCollapsed: (id: string, collapsed: boolean) => void;
  defaultProfileId: string | null;
  setDefaultProfileId: (id: string | null) => void;
  launchMode: AppPreferences["launchMode"];
  setLaunchMode: (mode: AppPreferences["launchMode"]) => void;
  autostartMode: AppPreferences["autostartMode"];
  setAutostartMode: (mode: AppPreferences["autostartMode"]) => void;
  checkForUpdates: boolean;
  setCheckForUpdates: (enabled: boolean) => void;
  includePreReleases: boolean;
  setIncludePreReleases: (enabled: boolean) => void;
  /** True once the initial GET /api/preferences has resolved (or failed) — callers that must not act on
   * still-default values (like restoring the saved dock layout) wait for this. */
  loaded: boolean;
  dockLayoutJson: string;
  setDockLayoutJson: (json: string) => void;
  dockLayoutProfiles: DockLayoutProfile[];
  saveDockLayoutProfile: (name: string, layoutJson: string) => void;
  deleteDockLayoutProfile: (id: string) => void;
}

const PreferencesContext = createContext<PreferencesContextValue | null>(null);

/** Editor-wide preferences (theme, language, user-defined preview device sizes) — persisted server-side
 * as one JSON file (see PreferencesStore.cs / GET+PUT /api/preferences), not browser localStorage: a
 * cleared cache, a different browser, or opening the editor from another machine on the LAN must not
 * lose these, since they're the user's settings, not this browser's. */
// Each tool window (Tercihler, Eklentiler, ...) is its own WebView2 with its own `document` — changing
// the theme there only ever touched that window's <html data-theme>, never the main editor window's,
// since they don't share React state. BroadcastChannel gives same-origin windows an instant push where
// the platform supports it; the focus/visibility refetch below is the reliable fallback either way (e.g.
// switching back to the main window after changing something in Tercihler always picks up the change).
const CHANNEL_NAME = "macro-grid-preferences";

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [prefs, setPrefs] = useState<AppPreferences>(DEFAULTS);
  const loaded = useRef(false);
  const [loadedState, setLoadedState] = useState(false);
  const [savedLanguage, setSavedLanguage] = useState<Language>(DEFAULTS.language);
  const channel = useRef<BroadcastChannel | null>(null);
  // Counts local changes and the saves still in flight. A refetch (started by a window focus, and the first click into the
  // editor is what focuses it) that was answered before this change reached the server carries the old values; applying
  // them would put the change back (a section the user just collapsed opened again).
  const localChanges = useRef(0);
  const savesInFlight = useRef(0);

  const refetch = useCallback(() => {
    const changesAtStart = localChanges.current;
    api.getPreferences().then((p) => {
      loaded.current = true;
      setLoadedState(true);
      if (localChanges.current !== changesAtStart || savesInFlight.current > 0) return;
      setPrefs(p);
      setSavedLanguage(p.language);
    }).catch(() => {
      loaded.current = true;
      setLoadedState(true);
    });
  }, []);

  useEffect(() => {
    refetch();

    try {
      channel.current = new BroadcastChannel(CHANNEL_NAME);
      channel.current.onmessage = (e) => setPrefs(e.data as AppPreferences);
    } catch {
      channel.current = null;
    }

    const onFocus = () => refetch();
    const onVisibility = () => { if (document.visibilityState === "visible") refetch(); };
    window.addEventListener("focus", onFocus);
    document.addEventListener("visibilitychange", onVisibility);

    return () => {
      channel.current?.close();
      window.removeEventListener("focus", onFocus);
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, [refetch]);

  const persist = useCallback((next: AppPreferences) => {
    setPrefs(next);
    localChanges.current++;
    // Only after the initial GET resolves — otherwise a save could race the load and overwrite the
    // server's copy with these still-default values.
    if (loaded.current) {
      savesInFlight.current++;
      api.savePreferences(next).then(() => setSavedLanguage(next.language)).catch(() => {}).finally(() => { savesInFlight.current--; });
      channel.current?.postMessage(next);
    }
  }, []);

  const setTheme = useCallback((theme: Theme) => persist({ ...prefs, theme }), [prefs, persist]);
  const setLanguage = useCallback((language: Language) => persist({ ...prefs, language }), [prefs, persist]);

  const addPreviewProfile = useCallback(
    (p: Omit<PreviewProfile, "id">) => {
      const profile: PreviewProfile = { ...p, id: `preview-${Date.now().toString(36)}` };
      persist({ ...prefs, previewProfiles: [...prefs.previewProfiles, profile] });
    },
    [prefs, persist],
  );

  const removePreviewProfile = useCallback(
    (id: string) => persist({ ...prefs, previewProfiles: prefs.previewProfiles.filter((p) => p.id !== id) }),
    [prefs, persist],
  );

  const setInspectorSectionCollapsed = useCallback(
    (id: string, collapsed: boolean) =>
      persist({ ...prefs, collapsedInspectorSections: { ...prefs.collapsedInspectorSections, [id]: collapsed } }),
    [prefs, persist],
  );

  const setDefaultProfileId = useCallback(
    (defaultProfileId: string | null) => persist({ ...prefs, defaultProfileId }),
    [prefs, persist],
  );

  const setLaunchMode = useCallback(
    (launchMode: AppPreferences["launchMode"]) => persist({ ...prefs, launchMode }),
    [prefs, persist],
  );

  const setAutostartMode = useCallback(
    (autostartMode: AppPreferences["autostartMode"]) => persist({ ...prefs, autostartMode }),
    [prefs, persist],
  );

  const setCheckForUpdates = useCallback(
    (checkForUpdates: boolean) => persist({ ...prefs, checkForUpdates }),
    [prefs, persist],
  );

  const setIncludePreReleases = useCallback(
    (includePreReleases: boolean) => persist({ ...prefs, includePreReleases }),
    [prefs, persist],
  );

  const setDockLayoutJson = useCallback(
    (dockLayoutJson: string) => persist({ ...prefs, dockLayoutJson }),
    [prefs, persist],
  );

  const saveDockLayoutProfile = useCallback(
    (name: string, layoutJson: string) => {
      const profile: DockLayoutProfile = { id: `layout-${Date.now().toString(36)}`, name, layoutJson };
      persist({ ...prefs, dockLayoutProfiles: [...prefs.dockLayoutProfiles, profile] });
    },
    [prefs, persist],
  );

  const deleteDockLayoutProfile = useCallback(
    (id: string) => persist({ ...prefs, dockLayoutProfiles: prefs.dockLayoutProfiles.filter((p) => p.id !== id) }),
    [prefs, persist],
  );

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", prefs.theme);
  }, [prefs.theme]);

  // The page starts as lang="tr"; CSS upper-casing follows it, so an English label showed a Turkish dotted capital I ("VERSİON").
  useEffect(() => {
    document.documentElement.lang = prefs.language;
  }, [prefs.language]);

  const value = useMemo(
    () => ({
      theme: prefs.theme,
      setTheme,
      language: prefs.language,
      savedLanguage,
      setLanguage,
      previewProfiles: prefs.previewProfiles,
      addPreviewProfile,
      removePreviewProfile,
      collapsedInspectorSections: prefs.collapsedInspectorSections,
      setInspectorSectionCollapsed,
      defaultProfileId: prefs.defaultProfileId,
      setDefaultProfileId,
      launchMode: prefs.launchMode,
      setLaunchMode,
      autostartMode: prefs.autostartMode,
      setAutostartMode,
      checkForUpdates: prefs.checkForUpdates,
      setCheckForUpdates,
      includePreReleases: prefs.includePreReleases,
      setIncludePreReleases,
      loaded: loadedState,
      dockLayoutJson: prefs.dockLayoutJson,
      setDockLayoutJson,
      dockLayoutProfiles: prefs.dockLayoutProfiles,
      saveDockLayoutProfile,
      deleteDockLayoutProfile,
    }),
    [
      prefs, savedLanguage, loadedState, setTheme, setLanguage, addPreviewProfile, removePreviewProfile,
      setInspectorSectionCollapsed, setDefaultProfileId, setLaunchMode, setAutostartMode, setCheckForUpdates,
      setIncludePreReleases, setDockLayoutJson, saveDockLayoutProfile, deleteDockLayoutProfile,
    ],
  );

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences() {
  const ctx = useContext(PreferencesContext);
  if (!ctx) throw new Error("usePreferences() must be used within a PreferencesProvider");
  return ctx;
}
