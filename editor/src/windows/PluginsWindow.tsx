import { useEffect, useState } from "react";
import { Download, FolderOpen, Link as LinkIcon, Plus, RefreshCw, RotateCw, Settings, Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { PluginCatalogEntryInfo, PluginInfo, PluginLinkInspectResult, PluginSourceInfo } from "../api/types";
import { DialogHost } from "../dialogs/DialogHost";
import { confirmAsync, promptAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "installed" | "discover";

const STATUS_COLOR: Record<PluginInfo["status"], string> = {
  Loaded: "var(--ms-success, #4ade80)",
  Incompatible: "var(--ms-warning, #facc15)",
  Error: "var(--ms-danger)",
  NeedsApproval: "var(--ms-warning, #facc15)",
};

/** The whole page of the "Eklentiler" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window. Lists what MacroGrid.Core.Plugins.PluginManager found under plugins/, and lets the user
 * install (from a folder), reload or remove a plugin. All of it takes effect immediately — no restart. */
export function PluginsWindow() {
  const { t } = useT();
  useDocumentTitle("plugins.title");
  const [category, setCategory] = useState<Category>("installed");
  const [plugins, setPlugins] = useState<PluginInfo[] | null>(null);
  const [installing, setInstalling] = useState(false);
  const [uninstallingId, setUninstallingId] = useState<string | null>(null);
  const [reloadingId, setReloadingId] = useState<string | null>(null);
  const [approvingId, setApprovingId] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [catalog, setCatalog] = useState<PluginCatalogEntryInfo[] | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [installingCatalogId, setInstallingCatalogId] = useState<string | null>(null);
  const [sources, setSources] = useState<PluginSourceInfo[]>([]);
  const [officialSource, setOfficialSource] = useState({ id: "official", owner: "Deccoyi", repo: "macro-grid-plugin" });
  const [selectedSource, setSelectedSource] = useState("official");
  const [catalogOfficial, setCatalogOfficial] = useState(true);

  const refresh = () => api.listPlugins().then(setPlugins).catch(() => {});

  useEffect(() => {
    refresh();
  }, []);

  const refreshCatalog = (source = selectedSource) => {
    setCatalogLoading(true);
    setCatalogError(null);
    setCatalog(null);
    api
      .fetchPluginCatalog(source)
      .then((response) => {
        setCatalogOfficial(response.official ?? source === "official");
        if (response.error) setCatalogError(response.error);
        else setCatalog(response.plugins);
      })
      .catch((err) => setCatalogError(err instanceof Error ? err.message : String(err)))
      .finally(() => setCatalogLoading(false));
  };

  useEffect(() => {
    // The HTTP client behind this is only ever used when Discover is actually open — never in the background.
    if (category === "discover" && catalog === null && !catalogLoading) refreshCatalog();
  }, [category]);

  const selectSource = (id: string) => {
    setSelectedSource(id);
    refreshCatalog(id);
  };

  const addSourceUrl = async (url: string) => {
    setError(null);
    try {
      const result = await api.addPluginSource(url);
      if (!result.id) {
        setError(t("plugins.discover.source.addFailed", result.error ?? "?"));
        return;
      }
      const list = await api.listPluginSources();
      setSources(list.added);
      selectSource(result.id);
    } catch (err) {
      setError(t("plugins.discover.source.addFailed", err instanceof Error ? err.message : String(err)));
    }
  };

  const addSource = async () => {
    const url = await promptAsync(t("plugins.discover.source.addPrompt"), "", { title: t("plugins.discover.source.addTitle") });
    if (!url) return;
    await addSourceUrl(url);
  };

  const installFromLink = async () => {
    const url = await promptAsync(t("plugins.discover.link.prompt"), "", { title: t("plugins.discover.link.title") });
    if (!url) return;
    setError(null);
    setNotice(null);

    let inspected: PluginLinkInspectResult;
    try {
      inspected = await api.inspectPluginLink(url);
    } catch (err) {
      setError(t("plugins.discover.link.failed", err instanceof Error ? err.message : String(err)));
      return;
    }
    if (inspected.error) {
      setError(t("plugins.discover.link.failed", inspected.error));
      return;
    }

    if (inspected.isMultiPlugin) {
      if (await confirmAsync(t("plugins.discover.link.isSource", `${inspected.owner}/${inspected.repo}`), { title: t("plugins.discover.link.title") })) {
        await addSourceUrl(url);
      }
      return;
    }
    if (!inspected.compatible) {
      setError(t("plugins.discover.link.failed", t("plugins.discover.incompatible")));
      return;
    }

    const risk = inspected.kind === "js" && (inspected.permissions?.length ?? 0) > 0
      ? t("plugins.discover.thirdParty.permissions", inspected.permissions!.map(permissionText).join(", "))
      : t("plugins.discover.thirdParty.fullAccess");
    const repoLabel = `${inspected.owner}/${inspected.repo}`;
    const proceed = await confirmAsync(`${t("plugins.discover.thirdParty.warning", repoLabel)} ${risk}`.trim(), {
      title: t("plugins.discover.thirdParty.title"),
      danger: true,
    });
    if (!proceed) return;

    try {
      const result = await api.installFromPluginLink(url);
      if (result.installed) {
        setNotice(t("plugins.discover.installSuccess", result.name ?? inspected.name ?? inspected.id ?? ""));
        refresh();
      } else {
        setError(t("plugins.discover.link.failed", result.error ?? "?"));
      }
    } catch (err) {
      setError(t("plugins.discover.link.failed", err instanceof Error ? err.message : String(err)));
    }
  };

  const removeSource = async (source: PluginSourceInfo) => {
    if (!(await confirmAsync(t("plugins.discover.source.removeConfirm", source.name), { title: t("plugins.discover.source.remove") }))) return;
    await api.removePluginSource(source.id);
    setSources((prev) => prev.filter((s) => s.id !== source.id));
    if (selectedSource === source.id) selectSource("official");
  };

  useEffect(() => {
    if (category !== "discover") return;
    api
      .listPluginSources()
      .then((r) => {
        setSources(r.added);
        setOfficialSource(r.official);
      })
      .catch(() => {});
  }, [category]);

  const permissionText = (permission: string) => {
    if (permission === "variables" || permission === "actions" || permission === "input") return t(`plugins.permission.${permission}`);
    if (permission.startsWith("http:")) return t("plugins.permission.http", permission.slice(5));
    return t("plugins.permission.unknown", permission);
  };

  const installFromCatalog = async (entry: PluginCatalogEntryInfo, official: boolean, sourceLabel: string) => {
    if (!entry.installableVersion) return;

    if (!official) {
      const risk = entry.kind === "js" && entry.permissions.length > 0
        ? t("plugins.discover.thirdParty.permissions", entry.permissions.map(permissionText).join(", "))
        : t("plugins.discover.thirdParty.fullAccess");
      const proceed = await confirmAsync(`${t("plugins.discover.thirdParty.warning", sourceLabel)} ${risk}`.trim(), {
        title: t("plugins.discover.thirdParty.title"),
        danger: true,
      });
      if (!proceed) return;
    }

    setInstallingCatalogId(entry.id);
    setError(null);
    setNotice(null);
    try {
      const result = await api.installFromPluginCatalog(selectedSource, entry.id, entry.installableVersion);
      if (result.installed) {
        setNotice(t("plugins.discover.installSuccess", result.name ?? entry.name));
        refresh();
        refreshCatalog();
      } else {
        setError(t("plugins.discover.installFailed", entry.name, result.error ?? "?"));
      }
    } catch (err) {
      setError(t("plugins.discover.installFailed", entry.name, err instanceof Error ? err.message : String(err)));
    } finally {
      setInstallingCatalogId(null);
    }
  };

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

  const reload = async (plugin: PluginInfo) => {
    setReloadingId(plugin.id);
    setError(null);
    setNotice(null);
    try {
      const info = await api.reloadPlugin(plugin.id);
      if (info.status === "Loaded") setNotice(t("plugins.reload.success", plugin.name));
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setReloadingId(null);
    }
  };

  const approve = async (plugin: PluginInfo) => {
    setApprovingId(plugin.id);
    setError(null);
    setNotice(null);
    try {
      const info = await api.approvePlugin(plugin.id);
      if (info.status === "Loaded") setNotice(t("plugins.approve.success", plugin.name));
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setApprovingId(null);
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
        const name = result.name ?? result.id ?? "";
        if (result.status && result.status !== "Loaded") setError(t("plugins.install.failed", name, result.detail ?? result.status));
        else setNotice(t("plugins.install.success", name));
        refresh();
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setInstalling(false);
    }
  };

  const currentSourceLabel = () => {
    if (selectedSource === officialSource.id) return `${officialSource.owner}/${officialSource.repo}`;
    const saved = sources.find((s) => s.id === selectedSource);
    return saved ? `${saved.owner}/${saved.repo}` : selectedSource;
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
                {p.hasIcon && (
                  <img
                    src={api.getPluginIconUrl(p.id)}
                    alt=""
                    style={{ width: 20, height: 20, borderRadius: 4, marginTop: 1, flexShrink: 0, objectFit: "contain" }}
                  />
                )}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13 }}>
                    {p.name} <span style={{ color: "var(--ms-text-disabled)" }}>v{p.version}</span>
                  </div>
                  {p.detail && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", marginTop: 2 }}>{p.detail}</div>}
                  {p.status === "NeedsApproval" && (
                    <div style={{ marginTop: 6 }}>
                      <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("plugins.approve.intro")}</div>
                      <ul style={{ margin: "4px 0 8px", paddingLeft: 18, fontSize: 12 }}>
                        {(p.pendingPermissions ?? []).map((permission) => (
                          <li key={permission}>{permissionText(permission)}</li>
                        ))}
                      </ul>
                      <button type="button" className="primary" disabled={approvingId === p.id} onClick={() => approve(p)}>
                        {t("plugins.approve")}
                      </button>
                    </div>
                  )}
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
                  title={t("plugins.reload")}
                  disabled={reloadingId === p.id}
                  onClick={() => reload(p)}
                  style={{ display: "flex", padding: 6, flexShrink: 0 }}
                >
                  <RotateCw size={14} />
                </button>
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
        <div style={{ maxWidth: 460 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
            <SectionLabel>{t("plugins.category.discover")}</SectionLabel>
            <div style={{ flex: 1 }} />
            <button type="button" className="ghost" title={t("plugins.refresh")} onClick={() => refreshCatalog()} style={{ display: "flex", padding: 6 }}>
              <RefreshCw size={14} />
            </button>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10 }}>
            <label style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("plugins.discover.source.label")}</label>
            <select value={selectedSource} onChange={(e) => selectSource(e.target.value)} style={{ flex: 1, minWidth: 0, fontSize: 12 }}>
              <option value={officialSource.id}>{t("plugins.discover.source.official")}</option>
              {sources.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
            {selectedSource !== officialSource.id && (
              <button
                type="button"
                className="ghost"
                title={t("plugins.discover.source.remove")}
                onClick={() => {
                  const source = sources.find((s) => s.id === selectedSource);
                  if (source) removeSource(source);
                }}
                style={{ display: "flex", padding: 6, flexShrink: 0 }}
              >
                <Trash2 size={14} />
              </button>
            )}
            <button type="button" className="ghost" title={t("plugins.discover.source.add")} onClick={addSource} style={{ display: "flex", padding: 6, flexShrink: 0 }}>
              <Plus size={14} />
            </button>
          </div>

          <button
            type="button"
            className="ghost"
            onClick={installFromLink}
            style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10, fontSize: 12 }}
          >
            <LinkIcon size={14} />
            {t("plugins.discover.link.install")}
          </button>

          {catalogLoading && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("plugins.discover.loading")}</div>}
          {!catalogLoading && catalogError && (
            <div style={{ fontSize: 12, color: "var(--ms-danger)", marginTop: 8 }}>{t("plugins.discover.offline")}</div>
          )}
          {!catalogLoading && !catalogError && catalog?.length === 0 && (
            <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("plugins.discover.empty")}</div>
          )}
          {!catalogLoading &&
            !catalogError &&
            catalog?.map((entry) => (
              <div key={entry.id} style={{ borderTop: "1px solid var(--ms-border)" }}>
                <div style={{ display: "flex", alignItems: "flex-start", gap: 8, padding: "8px 0" }}>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 13 }}>
                      {entry.name}{" "}
                      {entry.latestVersion && <span style={{ color: "var(--ms-text-disabled)" }}>v{entry.latestVersion}</span>}
                    </div>
                    {entry.author && (
                      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", marginTop: 2 }}>{t("plugins.discover.by", entry.author)}</div>
                    )}
                    {entry.description && (
                      <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", marginTop: 4 }}>{entry.description}</div>
                    )}
                    {!entry.compatible && (
                      <div style={{ fontSize: 11, color: "var(--ms-warning, #facc15)", marginTop: 4 }}>{t("plugins.discover.incompatible")}</div>
                    )}
                  </div>
                  {entry.installed && !entry.updateAvailable && (
                    <span style={{ fontSize: 11.5, color: "var(--ms-text-disabled)", flexShrink: 0, marginTop: 2 }}>
                      {t("plugins.discover.installed")}
                    </span>
                  )}
                  {(!entry.installed || entry.updateAvailable) && entry.compatible && (
                    <button
                      type="button"
                      className="ghost"
                      title={entry.updateAvailable ? t("plugins.discover.update") : t("plugins.discover.install")}
                      disabled={installingCatalogId === entry.id}
                      onClick={() => installFromCatalog(entry, catalogOfficial, currentSourceLabel())}
                      style={{ display: "flex", padding: 6, flexShrink: 0 }}
                    >
                      <Download size={14} />
                    </button>
                  )}
                </div>
              </div>
            ))}

          {(notice || error) && (
            <div style={{ marginTop: 14, paddingTop: 14, borderTop: "1px solid var(--ms-border)" }}>
              {notice && <p style={{ fontSize: 12, color: "var(--ms-success, #4ade80)", margin: 0 }}>{notice}</p>}
              {error && <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: 0 }}>{error}</p>}
            </div>
          )}
        </div>
      )}
      {/* This window is a separate native window; the confirm dialog of "Remove" is drawn by its own host. */}
      <DialogHost />
    </ToolWindowLayout>
  );
}
