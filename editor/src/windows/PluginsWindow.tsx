import { useEffect, useState } from "react";
import { FolderOpen, RefreshCw, Settings } from "lucide-react";
import { api } from "../api/client";
import type { ObsPluginSettings, PluginInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "installed" | "discover";

const STATUS_COLOR: Record<PluginInfo["status"], string> = {
  Loaded: "var(--ms-success, #4ade80)",
  Incompatible: "var(--ms-warning, #facc15)",
  Error: "var(--ms-danger)",
};

/** The whole page of the "Eklentiler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window. Lists what MacroStation.Core.Plugins.PluginLoader found under plugins/ at last startup, and
 * lets the user browse to a plugin folder to install one (copied into place — still needs a restart to
 * actually load, since the loader only runs once at startup before the DI container is built). */
export function PluginsWindow() {
  const { t } = useT();
  const [category, setCategory] = useState<Category>("installed");
  const [plugins, setPlugins] = useState<PluginInfo[] | null>(null);
  const [installing, setInstalling] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const refresh = () => api.listPlugins().then(setPlugins).catch(() => {});

  useEffect(() => {
    refresh();
  }, []);

  const install = async () => {
    setInstalling(true);
    setError(null);
    setNotice(null);
    try {
      const result = await api.installPluginDialog();
      if (result.canceled) return;
      if (result.installed) {
        setNotice(t("plugins.install.success", result.name ?? result.id ?? ""));
        refresh();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setInstalling(false);
    }
  };

  const categories = [
    { id: "installed", label: t("plugins.category.installed") },
    { id: "discover", label: t("plugins.category.discover") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      {category === "installed" ? (
        <div style={{ maxWidth: 460 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
            <SectionLabel>{t("plugins.category.installed")}</SectionLabel>
            <div style={{ flex: 1 }} />
            <button type="button" className="ghost" title={t("plugins.refresh")} onClick={refresh} style={{ display: "flex", padding: 6 }}>
              <RefreshCw size={14} />
            </button>
          </div>

          {plugins === null && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("pairing.loading")}</div>}
          {plugins?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("plugins.none")}</div>}
          {plugins?.map((p) => (
            <div key={p.id} style={{ borderTop: "1px solid var(--ms-border)" }}>
              <div style={{ display: "flex", alignItems: "flex-start", gap: 8, padding: "8px 0" }}>
                <span style={{ width: 8, height: 8, borderRadius: "50%", marginTop: 5, flexShrink: 0, background: STATUS_COLOR[p.status] }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13 }}>
                    {p.name} <span style={{ color: "var(--ms-text-disabled)" }}>v{p.version}</span>
                  </div>
                  {p.detail && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", marginTop: 2 }}>{p.detail}</div>}
                </div>
                {/* Only OBS has a known settings shape right now (see api/client.ts's getObsSettings/
                    saveObsSettings) — the host has no generic per-plugin settings schema/UI yet. */}
                {p.id === "obs" && (
                  <button
                    type="button"
                    className="ghost"
                    title={t("plugins.settings")}
                    onClick={() => setExpandedId((id) => (id === p.id ? null : p.id))}
                    style={{ display: "flex", padding: 6, flexShrink: 0 }}
                  >
                    <Settings size={14} />
                  </button>
                )}
              </div>
              {expandedId === p.id && p.id === "obs" && <ObsSettingsInline />}
            </div>
          ))}

          <div style={{ marginTop: 14, paddingTop: 14, borderTop: "1px solid var(--ms-border)" }}>
            <button type="button" onClick={install} disabled={installing} style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <FolderOpen size={14} />
              {t("plugins.install.browse")}
            </button>
            <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: "8px 0 0", lineHeight: 1.5, maxWidth: 400 }}>
              {t("plugins.install.hint")}
            </p>
            {notice && <p style={{ fontSize: 12, color: "var(--ms-success, #4ade80)", margin: "8px 0 0" }}>{notice}</p>}
            {error && <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: "8px 0 0" }}>{error}</p>}
          </div>
        </div>
      ) : (
        <>
          <SectionLabel>{t("plugins.category.discover")}</SectionLabel>
          <p style={{ margin: "10px 0 0", fontSize: 12.5, color: "var(--ms-text-secondary)", lineHeight: 1.5, maxWidth: 420 }}>
            {t("plugins.comingSoon.body")}
          </p>
        </>
      )}
    </ToolWindowLayout>
  );
}

/** Inline settings for the OBS plugin — expands under its row in the "Yüklü Eklentiler" list, no window/
 * modal (docs/ui-guidelines.md: mevcut workspace/panel yerine bir yenisini açma). Reads/writes through
 * the host's generic per-plugin settings.json passthrough (api.getObsSettings/saveObsSettings). */
function ObsSettingsInline() {
  const { t } = useT();
  const [settings, setSettings] = useState<ObsPluginSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api
      .getObsSettings()
      .then((s) => setSettings(s ?? { enabled: false, host: "127.0.0.1", port: 4455, password: "" }))
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false));
  }, []);

  const update = (patch: Partial<ObsPluginSettings>) => {
    setSettings((s) => (s ? { ...s, ...patch } : s));
    setSaved(false);
  };

  const save = async () => {
    if (!settings) return;
    setSaving(true);
    setError(null);
    try {
      await api.saveObsSettings(settings);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", padding: "0 0 10px 16px" }}>{t("pairing.loading")}</div>;
  if (!settings) return null;

  return (
    <div style={{ padding: "0 0 10px 16px", display: "flex", flexDirection: "column", gap: 8 }}>
      <label className="field" style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
        <input type="checkbox" checked={settings.enabled} onChange={(e) => update({ enabled: e.target.checked })} />
        {t("plugins.obs.enabled")}
      </label>

      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 8 }}>
        <label className="field">
          {t("plugins.obs.host")}
          <input type="text" value={settings.host} onChange={(e) => update({ host: e.target.value })} />
        </label>
        <label className="field">
          {t("plugins.obs.port")}
          <input type="number" min={1} max={65535} value={settings.port} onChange={(e) => update({ port: Number(e.target.value) })} />
        </label>
      </div>

      <label className="field">
        {t("plugins.obs.password")}
        <input type="password" value={settings.password} onChange={(e) => update({ password: e.target.value })} />
      </label>

      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: "2px 0 0", lineHeight: 1.5, maxWidth: 380 }}>
        {t("plugins.obs.hint")}
      </p>

      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        <button type="button" onClick={save} disabled={saving} style={{ alignSelf: "flex-start" }}>
          {t("plugins.obs.save")}
        </button>
        {saved && <span style={{ fontSize: 11.5, color: "var(--ms-success, #4ade80)" }}>{t("plugins.obs.saved")}</span>}
        {error && <span style={{ fontSize: 11.5, color: "var(--ms-danger)" }}>{error}</span>}
      </div>
    </div>
  );
}
