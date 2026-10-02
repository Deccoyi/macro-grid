import { useEffect, useState } from "react";
import { Braces, Globe, SlidersHorizontal, Smartphone, Sun, Trash2, User, Zap } from "lucide-react";
import type { ProfileSummary } from "../api/types";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { NumberInput, Seg, SelectInput, Switch, TextInput } from "../panels/fields/controls";
import { usePreferences } from "../preferences/PreferencesContext";
import { AutomationPage } from "./AutomationPage";
import { GlobalVariablesPage } from "./GlobalVariablesPage";
import { LanguageSection } from "./LanguageSection";
import { SettingsShell, type SettingsGroup } from "./SettingsShell";
import { PageHeader, SettingGroup, SettingRow } from "./settingsPrimitives";

type Category = "general" | "appearance" | "language" | "previewProfiles" | "profiles" | "globalVariables" | "automation";

/** The whole page of the "Tercihler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window, not an in-page dialog. Layout and measurements: docs/ui/settings-window-design.md. */
export function PreferencesWindow() {
  const { t } = useT();
  useDocumentTitle("preferences.title");
  const {
    theme, setTheme, previewProfiles, addPreviewProfile, removePreviewProfile, defaultProfileId, setDefaultProfileId,
    launchMode, setLaunchMode, autostartMode, setAutostartMode,
    checkForUpdates, setCheckForUpdates, includePreReleases, setIncludePreReleases, allowUnencrypted, setAllowUnencrypted, dismissedNotices, dismissNotice,
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

  const groups: SettingsGroup[] = [
    {
      label: t("preferences.group.app"),
      categories: [
        { id: "general", label: t("preferences.category.general"), icon: SlidersHorizontal },
        { id: "appearance", label: t("preferences.category.appearance"), icon: Sun },
        { id: "language", label: t("preferences.category.language"), icon: Globe },
      ],
    },
    {
      label: t("preferences.group.devices"),
      categories: [
        { id: "previewProfiles", label: t("preferences.category.previewProfiles"), icon: Smartphone },
        { id: "profiles", label: t("preferences.category.profiles"), icon: User },
      ],
    },
    {
      label: t("preferences.group.data"),
      categories: [
        { id: "globalVariables", label: t("preferences.category.globalVariables"), icon: Braces },
        { id: "automation", label: t("preferences.category.automation"), icon: Zap },
      ],
    },
  ];

  return (
    <SettingsShell
      groups={groups} navLabel={t("preferences.nav")} activeId={category} onSelect={(id) => setCategory(id as Category)}
    >
      {category === "general" && (
        <>
          <PageHeader title={t("preferences.category.general")} lead={t("preferences.lead.general")} alert={autostartError} />
          <SettingGroup title={t("preferences.general.startup")}>
            <SettingRow
              name={t("preferences.autostart")} hint={t("preferences.autostart.hint")} switchControl
              control={<Switch checked={autostart === true} disabled={autostart === null} onChange={(v) => void changeAutostart(v)} label={t("preferences.autostart")} />}
            />
            <SettingRow
              name={t("preferences.autostartMode")}
              control={
                <SelectInput value={autostartMode} label={t("preferences.autostartMode")} onChange={(v) => setAutostartMode(v as typeof autostartMode)}>
                  <option value="tray">{t("preferences.autostartMode.tray")}</option>
                  <option value="window">{t("preferences.autostartMode.window")}</option>
                </SelectInput>
              }
            />
            <SettingRow
              name={t("preferences.launchMode")}
              control={
                <SelectInput value={launchMode} label={t("preferences.launchMode")} onChange={(v) => setLaunchMode(v as typeof launchMode)}>
                  <option value="window">{t("preferences.launchMode.window")}</option>
                  <option value="tray">{t("preferences.launchMode.tray")}</option>
                </SelectInput>
              }
            />
          </SettingGroup>

          <SettingGroup title={t("preferences.general.updates")}>
            <SettingRow
              name={t("preferences.updates.auto")} hint={t("preferences.updates.auto.hint")} switchControl
              control={<Switch checked={checkForUpdates} onChange={setCheckForUpdates} label={t("preferences.updates.auto")} />}
            />
            <SettingRow
              name={t("preferences.updates.prerelease")} switchControl
              control={<Switch checked={includePreReleases} onChange={setIncludePreReleases} label={t("preferences.updates.prerelease")} />}
            />
          </SettingGroup>

          <SettingGroup title={t("preferences.general.security")}>
            <SettingRow
              name={t("preferences.unencrypted")} hint={t("preferences.unencrypted.hint")} switchControl
              control={<Switch checked={allowUnencrypted} onChange={setAllowUnencrypted} label={t("preferences.unencrypted")} />}
            />
          </SettingGroup>

          {Object.keys(dismissedNotices).length > 0 && (
            <div className="st-footer">
              <button type="button" className="st-btn" onClick={() => dismissNotice()}>{t("preferences.notices.reset")}</button>
            </div>
          )}
        </>
      )}

      {category === "appearance" && (
        <>
          <PageHeader title={t("preferences.category.appearance")} lead={t("preferences.lead.appearance")} />
          <SettingGroup>
            <SettingRow
              name={t("preferences.theme")}
              control={
                <Seg
                  value={theme}
                  onChange={setTheme}
                  options={[
                    { value: "dark", label: t("preferences.theme.dark") },
                    { value: "light", label: t("preferences.theme.light") },
                  ]}
                />
              }
            />
          </SettingGroup>
        </>
      )}

      {category === "language" && <LanguageSection />}

      {category === "previewProfiles" && (
        <>
          <PageHeader title={t("preferences.category.previewProfiles")} lead={t("preferences.lead.previewProfiles")} />
          <SettingGroup>
            {previewProfiles.map((p) => (
              <div key={p.id} className="st-row" style={{ gridTemplateColumns: "minmax(0, 1fr) auto 28px", columnGap: 8 }}>
                <div className="st-row-name">{p.name}</div>
                <div className="st-count">{p.width}×{p.height}</div>
                <button type="button" className="st-ib danger" aria-label={t("preferences.previewProfiles.remove")} title={t("preferences.previewProfiles.remove")} onClick={() => removePreviewProfile(p.id)}>
                  <Trash2 size={14} />
                </button>
              </div>
            ))}
          </SettingGroup>
          <div className="st-add" style={{ gridTemplateColumns: "minmax(0, 1fr) 80px 80px auto" }}>
            <TextInput value={name} onChange={setName} placeholder={t("preferences.previewProfiles.name")} label={t("preferences.previewProfiles.name")} />
            <NumberInput value={width} onChange={setWidth} min={100} max={4000} label={t("preferences.previewProfiles.width")} />
            <NumberInput value={height} onChange={setHeight} min={100} max={4000} label={t("preferences.previewProfiles.height")} />
            <button type="button" className="st-btn" disabled={!name.trim()} onClick={() => { addPreviewProfile({ name: name.trim(), width, height }); setName(""); }}>
              {t("preferences.previewProfiles.add")}
            </button>
          </div>
        </>
      )}

      {category === "profiles" && (
        <>
          <PageHeader title={t("preferences.category.profiles")} lead={t("preferences.lead.profiles")} />
          <SettingGroup>
            <SettingRow
              name={t("preferences.defaultProfile")} hint={t("preferences.defaultProfile.hint")}
              control={
                <SelectInput value={defaultProfileId ?? ""} label={t("preferences.defaultProfile")} onChange={(v) => setDefaultProfileId(v || null)}>
                  <option value="">{t("preferences.defaultProfile.none")}</option>
                  {profiles.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </SelectInput>
              }
            />
          </SettingGroup>
        </>
      )}

      {category === "globalVariables" && <GlobalVariablesPage />}
      {category === "automation" && <AutomationPage />}
    </SettingsShell>
  );
}
