import { useEffect, useState } from "react";
import { FolderOpen, RefreshCw, Settings, Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { PluginInfo } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
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
  const [uninstallingId, setUninstallingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refresh = () => api.listPlugins().then(setPlugins).catch(() => {});

  useEffect(() => {
    refresh();
  }, []);

  const uninstall = async (plugin: PluginInfo) => {
    if (!(await confirmAsync(t("plugins.uninstall.confirm", plugin.name), { title: t("plugins.uninstall"), danger: true }))) return;
    setUninstallingId(plugin.id);
    setError(null);
    setNotice(null);
    try {
      const result = await api.uninstallPlugin(plugin.id);
      setNotice(result.pending ? t("plugins.uninstall.pending", plugin.name) : t("plugins.uninstall.success", plugin.name));
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setUninstallingId(null);
    }
  };

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
                {p.hasSettings && (
                  <button
                    type="button"
                    className="ghost"
                    title={t("plugins.settings")}
                    onClick={() => api.openPluginSettingsWindow(p.id)}
                    style={{ display: "flex", padding: 6, flexShrink: 0 }}
                  >
                    <Settings size={14} />
                  </button>
                )}
                <button
                  type="button"
                  className="ghost"
                  title={t("plugins.uninstall")}
                  disabled={uninstallingId === p.id}
                  onClick={() => uninstall(p)}
                  style={{ display: "flex", padding: 6, flexShrink: 0 }}
                >
                  <Trash2 size={14} />
                </button>
              </div>
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
