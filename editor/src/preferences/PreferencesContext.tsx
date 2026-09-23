import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api } from "../api/client";
import type { AppPreferences, PreviewProfileInfo } from "../api/types";

export type Theme = "dark" | "light";
export type Language = "tr" | "en";
export type PreviewProfile = PreviewProfileInfo;

const DEFAULTS: AppPreferences = { theme: "dark", language: "tr", previewProfiles: [], collapsedInspectorSections: {}, defaultProfileId: null };

interface PreferencesContextValue {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  language: Language;
  setLanguage: (language: Language) => void;
  previewProfiles: PreviewProfile[];
  addPreviewProfile: (p: Omit<PreviewProfile, "id">) => void;
  removePreviewProfile: (id: string) => void;
  collapsedInspectorSections: Record<string, boolean>;
  setInspectorSectionCollapsed: (id: string, collapsed: boolean) => void;
  defaultProfileId: string | null;
  setDefaultProfileId: (id: string | null) => void;
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
const CHANNEL_NAME = "macro-station-preferences";

export function PreferencesProvider({ children }: { children: ReactNode }) {
  const [prefs, setPrefs] = useState<AppPreferences>(DEFAULTS);
  const loaded = useRef(false);
  const channel = useRef<BroadcastChannel | null>(null);

  const refetch = useCallback(() => {
    api.getPreferences().then((p) => {
      loaded.current = true;
      setPrefs(p);
    }).catch(() => {
      loaded.current = true;
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
    // Only after the initial GET resolves — otherwise a save could race the load and overwrite the
    // server's copy with these still-default values.
    if (loaded.current) {
      api.savePreferences(next).catch(() => {});
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

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", prefs.theme);
  }, [prefs.theme]);

  const value = useMemo(
    () => ({
      theme: prefs.theme,
      setTheme,
      language: prefs.language,
      setLanguage,
      previewProfiles: prefs.previewProfiles,
      addPreviewProfile,
      removePreviewProfile,
      collapsedInspectorSections: prefs.collapsedInspectorSections,
      setInspectorSectionCollapsed,
      defaultProfileId: prefs.defaultProfileId,
      setDefaultProfileId,
    }),
    [prefs, setTheme, setLanguage, addPreviewProfile, removePreviewProfile, setInspectorSectionCollapsed, setDefaultProfileId],
  );

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>;
}

export function usePreferences() {
  const ctx = useContext(PreferencesContext);
  if (!ctx) throw new Error("usePreferences() must be used within a PreferencesProvider");
  return ctx;
}
