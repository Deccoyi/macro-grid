import { useEffect, useState } from "react";
import { Trash2 } from "lucide-react";
import type { ProfileSummary } from "../api/types";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { SectionLabel, Seg } from "../panels/fields/controls";
import { usePreferences } from "../preferences/PreferencesContext";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "general" | "appearance" | "language" | "previewProfiles" | "profiles";

/** The whole page of the "Tercihler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window, not an in-page dialog. */
export function PreferencesWindow() {
  const { t, lang, setLang } = useT();
  useDocumentTitle("preferences.title");
  const {
    theme, setTheme, previewProfiles, addPreviewProfile, removePreviewProfile, defaultProfileId, setDefaultProfileId,
    launchMode, setLaunchMode, autostartMode, setAutostartMode,
    checkForUpdates, setCheckForUpdates, includePreReleases, setIncludePreReleases,
  } = usePreferences();
  const [category, setCategory] = useState<Category>("general");
  const [name, setName] = useState("");
  const [width, setWidth] = useState(390);
  const [height, setHeight] = useState(844);
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [autostart, setAutostart] = useState<boolean | null>(null);
  const [autostartError, setAutostartError] = useState<string | null>(null);

  useEffect(() => {
    api.listProfiles().then(setProfiles).catch(() => {});
    api.getAutostart().then((r) => setAutostart(r.enabled)).catch(() => {});
  }, []);

  const changeAutostart = async (enabled: boolean) => {
    setAutostartError(null);
    try {
      setAutostart((await api.setAutostart(enabled)).enabled);
    } catch (err) {
      setAutostartError(t("preferences.autostart.failed", err instanceof Error ? err.message : String(err)));
    }
  };

  const categories = [
    { id: "general", label: t("preferences.category.general") },
    { id: "appearance", label: t("preferences.category.appearance") },
    { id: "language", label: t("preferences.category.language") },
    { id: "previewProfiles", label: t("preferences.category.previewProfiles") },
    { id: "profiles", label: t("preferences.category.profiles") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      {category === "general" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 420 }}>
          <SectionLabel>{t("preferences.category.general")}</SectionLabel>
          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <input
              type="checkbox"
              checked={autostart === true}
              disabled={autostart === null}
              onChange={(e) => changeAutostart(e.target.checked)}
            />
            {t("preferences.autostart")}
          </label>
          <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("preferences.autostart.hint")}</p>
          {autostartError && <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: 0 }}>{autostartError}</p>}
          <label className="field">
            {t("preferences.autostartMode")}
            <select value={autostartMode} onChange={(e) => setAutostartMode(e.target.value as typeof autostartMode)}>
              <option value="tray">{t("preferences.autostartMode.tray")}</option>
              <option value="window">{t("preferences.autostartMode.window")}</option>
            </select>
          </label>
          <label className="field">
            {t("preferences.launchMode")}
            <select value={launchMode} onChange={(e) => setLaunchMode(e.target.value as typeof launchMode)}>
              <option value="window">{t("preferences.launchMode.window")}</option>
              <option value="tray">{t("preferences.launchMode.tray")}</option>
            </select>
          </label>
          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <input type="checkbox" checked={checkForUpdates} onChange={(e) => setCheckForUpdates(e.target.checked)} />
            {t("preferences.updates.auto")}
          </label>
          <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("preferences.updates.auto.hint")}</p>
          <label style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <input type="checkbox" checked={includePreReleases} onChange={(e) => setIncludePreReleases(e.target.checked)} />
            {t("preferences.updates.prerelease")}
          </label>
        </div>
      )}

      {category === "appearance" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
          <SectionLabel>{t("preferences.category.appearance")}</SectionLabel>
          <label className="field">
            {t("preferences.theme")}
            <Seg
              value={theme}
              onChange={setTheme}
              options={[
                { value: "dark", label: t("preferences.theme.dark") },
                { value: "light", label: t("preferences.theme.light") },
              ]}
            />
          </label>
        </div>
      )}

      {category === "language" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
          <SectionLabel>{t("preferences.category.language")}</SectionLabel>
          <label className="field">
            {t("preferences.language")}
            <Seg value={lang} onChange={setLang} options={[{ value: "tr", label: "Türkçe" }, { value: "en", label: "English" }]} />
          </label>
        </div>
      )}

      {category === "previewProfiles" && (
        <div style={{ maxWidth: 420 }}>
          <SectionLabel>{t("preferences.previewProfiles")}</SectionLabel>
          {previewProfiles.map((p) => (
            <div key={p.id} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", borderTop: "1px solid var(--ms-border)" }}>
              <div style={{ flex: 1, fontSize: 12.5 }}>{p.name}</div>
              <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{p.width}×{p.height}</div>
              <button type="button" className="ghost" title={t("preferences.previewProfiles.remove")} onClick={() => removePreviewProfile(p.id)} style={{ padding: 5, color: "var(--ms-danger)" }}>
                <Trash2 size={13} />
              </button>
            </div>
          ))}
          <div style={{ display: "flex", gap: 6, marginTop: 8 }}>
            <input type="text" placeholder={t("preferences.previewProfiles.name")} value={name} onChange={(e) => setName(e.target.value)} style={{ flex: 1 }} />
            <input type="number" min={100} max={4000} value={width} onChange={(e) => setWidth(Number(e.target.value))} style={{ width: 60 }} title={t("preferences.previewProfiles.width")} />
            <input type="number" min={100} max={4000} value={height} onChange={(e) => setHeight(Number(e.target.value))} style={{ width: 60 }} title={t("preferences.previewProfiles.height")} />
            <button
              type="button"
              className="ghost"
              disabled={!name.trim()}
              onClick={() => { addPreviewProfile({ name: name.trim(), width, height }); setName(""); }}
            >
              {t("preferences.previewProfiles.add")}
            </button>
          </div>
        </div>
      )}

      {category === "profiles" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
          <SectionLabel>{t("preferences.category.profiles")}</SectionLabel>
          <label className="field">
            {t("preferences.defaultProfile")}
            <select
              value={defaultProfileId ?? ""}
              onChange={(e) => setDefaultProfileId(e.target.value || null)}
            >
              <option value="">{t("preferences.defaultProfile.none")}</option>
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </label>
          <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>
            {t("preferences.defaultProfile.hint")}
          </p>
        </div>
      )}
    </ToolWindowLayout>
  );
}
