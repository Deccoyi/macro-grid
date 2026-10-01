import { useEffect, useState } from "react";
import { ArrowLeft, ExternalLink, FolderOpen, Link as LinkIcon, Plus, RefreshCw, RotateCw, Search, Settings, Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { PluginCatalogEntryInfo, PluginInfo, PluginLinkInspectResult, PluginSourceInfo } from "../api/types";
import { DialogHost } from "../dialogs/DialogHost";
import { confirmAsync, confirmRichAsync, promptAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";
import { permissionLabel } from "./permissionLabel";
import { InstallConsent } from "./InstallConsent";

type Category = "installed" | "discover";

const STATUS_COLOR: Record<PluginInfo["status"], string> = {
  Loaded: "var(--ms-success, #4ade80)",
  Incompatible: "var(--ms-warning, #facc15)",
  Error: "var(--ms-danger)",
  NeedsApproval: "var(--ms-warning, #facc15)",
  NotAllowed: "var(--ms-danger)",
};

// The catalog carries no icon of its own yet (docs/roadmap.md, "A richer Discover tab" — deciding what's
// worth adding to macrogrid-index.json is separate from this), so a card's avatar is its own initial on a
// color picked deterministically from its id, not a random one that would shift between renders.
const AVATAR_HUES = [4, 28, 48, 88, 152, 184, 208, 262, 300, 330];
function avatarHue(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return AVATAR_HUES[hash % AVATAR_HUES.length] ?? 0;
}

type DiscoverFilter = "all" | "notInstalled" | "installed" | "updates" | "js" | "native";
const DISCOVER_FILTERS: DiscoverFilter[] = ["all", "notInstalled", "installed", "updates", "js", "native"];

// The catalog has no category field yet, so "categories" are derived from what every entry does carry:
// install state and plugin kind. Real categories/tags need a catalog field first (docs/roadmap.md).
function matchesFilter(entry: PluginCatalogEntryInfo, filter: DiscoverFilter): boolean {
  switch (filter) {
    case "notInstalled": return !entry.installed;
    case "installed": return entry.installed;
    case "updates": return entry.updateAvailable;
    case "js": return entry.kind === "js";
    case "native": return entry.kind !== "js";
    default: return true;
  }
}

function GetButton({ entry, busy, onGet, large = false }: { entry: PluginCatalogEntryInfo; busy: boolean; onGet: () => void; large?: boolean }) {
  const { t } = useT();
  const pad = large ? "6px 26px" : "3px 14px";
  const font = large ? 13 : 11.5;
  if (entry.withdrawn) return <span style={{ fontSize: 11, color: "var(--ms-warning, #facc15)", flexShrink: 0 }}>{t("plugins.discover.withdrawn")}</span>;
  if (!entry.compatible) return <span style={{ fontSize: 11, color: "var(--ms-warning, #facc15)", flexShrink: 0 }}>{t("plugins.discover.incompatibleShort")}</span>;
  if (entry.installed && !entry.updateAvailable) {
    return <span style={{ fontSize: font, color: "var(--ms-text-disabled)", flexShrink: 0, padding: pad }}>{t("plugins.discover.installed")}</span>;
  }
  return (
    <button
      type="button"
      disabled={busy}
      onClick={(e) => { e.stopPropagation(); onGet(); }}
      style={{
        flexShrink: 0, borderRadius: 999, padding: pad, fontSize: font, fontWeight: 600, cursor: "pointer",
        border: "none", background: "var(--ms-accent)", color: "var(--ms-accent-on, #fff)",
      }}
    >
      {entry.updateAvailable ? t("plugins.discover.update") : t("plugins.discover.get")}
    </button>
  );
}

function InfoCell({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ flex: 1, textAlign: "center", borderRight: "1px solid var(--ms-border)" }}>
      <div style={{ fontSize: 10, letterSpacing: 0.4, textTransform: "uppercase", color: "var(--ms-text-disabled)" }}>{label}</div>
      <div style={{ fontSize: 13, marginTop: 4 }}>{value}</div>
    </div>
  );
}

function PluginAvatar({ id, name, size = 36, iconUrl }: { id: string; name: string; size?: number; iconUrl?: string }) {
  const hue = avatarHue(id);
  const [broken, setBroken] = useState(false);
  if (iconUrl && !broken) {
    return <img src={iconUrl} alt="" onError={() => setBroken(true)} style={{ width: size, height: size, borderRadius: 8, flexShrink: 0, objectFit: "contain" }} />;
  }
  return (
    <div
      style={{
        width: size, height: size, borderRadius: 8, flexShrink: 0,
        display: "flex", alignItems: "center", justifyContent: "center",
        background: `hsl(${hue}, 45%, 24%)`, color: `hsl(${hue}, 70%, 82%)`,
        fontSize: size * 0.44, fontWeight: 600,
      }}
    >
      {(name[0] ?? "?").toUpperCase()}
    </div>
  );
}

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
  const [selectedEntryId, setSelectedEntryId] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<DiscoverFilter>("all");

  const refresh = () => api.listPlugins().then(setPlugins).catch(() => {});

  useEffect(() => {
    refresh();
  }, []);

  const refreshCatalog = (source = selectedSource, force = false) => {
    setCatalogLoading(true);
    setCatalogError(null);
    setCatalog(null);
    setSelectedEntryId(null);
    api
      .fetchPluginCatalog(source, force)
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
      setError(t("plugins.discover.link.failed", inspected.incompatibleReason ?? t("plugins.discover.incompatible")));
      return;
    }

    const proceed = await confirmRichAsync({
      title: t("plugins.discover.thirdParty.title"),
      content: <InstallConsent name={inspected.name ?? inspected.id ?? ""} kind={inspected.kind} permissions={inspected.permissions} sourceLabel={`${inspected.owner}/${inspected.repo}`} />,
      confirmLabel: inspected.kind === "js" ? t("consent.allowInstall") : t("consent.installAnyway"),
      danger: inspected.kind !== "js",
    });
    if (!proceed) return;

    try {
      const result = await api.installFromPluginLink(url);
      if (result.installed) {
        setNotice(t("plugins.discover.installSuccess", result.name ?? inspected.name ?? inspected.id ?? ""));
        approveIfNeeded(result.id ?? inspected.id ?? "", result.status);
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

  const permissionText = (permission: string) => permissionLabel(permission, t).text;

  const installFromCatalog = async (entry: PluginCatalogEntryInfo, official: boolean, sourceLabel: string) => {
    if (!entry.installableVersion) return;

    const hasPermissions = entry.kind === "js" && entry.permissions.length > 0;
    if (!official || hasPermissions || entry.kind !== "js") {
      // Official plugins skip the warning for a native plugin (they are ours) but a JS plugin's permissions
      // are still confirmed before install, not after.
      if (!official || hasPermissions) {
        const proceed = await confirmRichAsync({
          title: official ? t("plugins.install.jsPermissions.title") : t("plugins.discover.thirdParty.title"),
          content: <InstallConsent name={entry.name} kind={entry.kind} permissions={entry.permissions} sourceLabel={official ? undefined : sourceLabel} />,
          confirmLabel: entry.kind === "js" ? t("consent.allowInstall") : t("consent.installAnyway"),
          danger: entry.kind !== "js",
        });
        if (!proceed) return;
      }
    }

    setInstallingCatalogId(entry.id);
    setError(null);
    setNotice(null);
    try {
      const result = await api.installFromPluginCatalog(selectedSource, entry.id, entry.installableVersion);
      if (result.installed) {
        setNotice(t("plugins.discover.installSuccess", result.name ?? entry.name));
        approveIfNeeded(entry.id, result.status);
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

  // The person already saw and confirmed the declared permissions before the install call ran (see
  // install(), installFromCatalog(), installFromLink()), so a fresh js plugin sitting in NeedsApproval
  // right after that is approved right away — no separate second "Enable" click.
  const approveIfNeeded = (id: string, status: string | undefined) => {
    if (status === "NeedsApproval") api.approvePlugin(id).then(refresh).catch(() => {});
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

  const switchPermission = async (plugin: PluginInfo, permission: string, enabled: boolean) => {
    setError(null);
    setNotice(null);
    try {
      await api.setPluginPermission(plugin.id, permission, enabled);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  const install = async () => {
    setInstalling(true);
    setError(null);
    setNotice(null);
    try {
      const browsed = await api.browsePluginInstall();
      if (browsed.canceled || !browsed.path) return;

      // The server has already refused a C# plugin that is not officially signed (browsePluginInstall throws), so
      // only a JavaScript plugin or a signed copy of an official plugin gets here.
      const name = browsed.name ?? browsed.id ?? "";
      if (browsed.kind === "js" && (browsed.permissions?.length ?? 0) > 0) {
        const proceed = await confirmRichAsync({
          title: t("plugins.install.jsPermissions.title"),
          content: <InstallConsent name={name} kind="js" permissions={browsed.permissions} />,
          confirmLabel: t("consent.allowInstall"),
        });
        if (!proceed) return;
      }

      const result = await api.confirmPluginInstall(browsed.path);
      if (result.installed) {
        const name = result.name ?? result.id ?? "";
        if (result.status && result.status !== "Loaded" && result.status !== "NeedsApproval") setError(t("plugins.install.failed", name, result.detail ?? result.status));
        else setNotice(t("plugins.install.success", name));
        approveIfNeeded(result.id ?? "", result.status);
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
  const selectedEntry = catalog?.find((e) => e.id === selectedEntryId) ?? null;
  const needle = query.trim().toLowerCase();
  const visibleEntries = (catalog ?? []).filter((e) =>
    matchesFilter(e, filter) && (!needle || `${e.name} ${e.author ?? ""} ${e.description ?? ""} ${e.category ?? ""} ${(e.tags ?? []).join(" ")}`.toLowerCase().includes(needle)));
  const filterCount = (f: DiscoverFilter) => (catalog ?? []).filter((e) => matchesFilter(e, f)).length;

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
                  <div style={{ fontSize: 13, display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
                    <span>
                      {p.name} <span style={{ color: "var(--ms-text-disabled)" }}>v{p.version}</span>
                    </span>
                    <span
                      style={{
                        fontSize: 10,
                        padding: "1px 6px",
                        borderRadius: 10,
                        border: "1px solid var(--ms-border)",
                        color: "var(--ms-text-secondary)",
                      }}
                    >
                      {t(`plugins.badge.${p.trust === "ThirdParty" ? "thirdParty" : p.trust === "Official" ? "official" : "local"}`)}
                    </span>
                    {catalog?.find((e) => e.id === p.id)?.updateAvailable && (
                      <span style={{ fontSize: 10, color: "var(--ms-warning, #facc15)" }}>{t("plugins.updateAvailable")}</span>
                    )}
                  </div>
                  {p.detail && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", marginTop: 2 }}>{p.detail}</div>}
                  {p.withdrawn && <div style={{ fontSize: 11, color: "var(--ms-warning, #facc15)", marginTop: 2 }}>{t("plugins.withdrawn.installedNote")}</div>}
                  {(p.keyboardUsesToday ?? 0) > 0 && (
                    <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", marginTop: 2 }}>{t("plugins.keyboardUses", String(p.keyboardUsesToday ?? 0))}</div>
                  )}
                  {p.status === "Loaded" && (p.permissions?.length ?? 0) > 0 && (
                    <details style={{ marginTop: 4 }}>
                      <summary style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", cursor: "pointer" }}>{t("plugins.permissions.title", String(p.permissions?.length ?? 0))}</summary>
                      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: "4px 0" }}>{t("plugins.permissions.hint")}</div>
                      {(p.permissions ?? []).map((permission) => {
                        const off = (p.switchedOffPermissions ?? []).includes(permission);
                        return (
                          <label key={permission} style={{ display: "flex", alignItems: "flex-start", gap: 6, fontSize: 12, padding: "2px 0" }}>
                            <input type="checkbox" checked={!off} onChange={(e) => switchPermission(p, permission, e.target.checked)} style={{ marginTop: 2 }} />
                            <span>{permissionText(permission)}</span>
                          </label>
                        );
                      })}
                    </details>
                  )}
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
      ) : selectedEntry ? (
        <div style={{ maxWidth: 680 }}>
          <button
            type="button"
            className="ghost"
            onClick={() => setSelectedEntryId(null)}
            style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, marginBottom: 14, padding: "4px 6px" }}
          >
            <ArrowLeft size={14} />
            {t("plugins.discover.back")}
          </button>

          <div style={{ display: "flex", gap: 16, alignItems: "center" }}>
            <PluginAvatar id={selectedEntry.id} name={selectedEntry.name} size={84} iconUrl={selectedEntry.hasIcon ? api.pluginCatalogIconUrl(selectedSource, selectedEntry.id) : undefined} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 22, fontWeight: 600 }}>{selectedEntry.name}</div>
              {selectedEntry.author && (
                <div style={{ fontSize: 13, color: "var(--ms-accent)", marginTop: 2 }}>{selectedEntry.author}</div>
              )}
              <div style={{ marginTop: 10 }}>
                <GetButton entry={selectedEntry} busy={installingCatalogId === selectedEntry.id} onGet={() => installFromCatalog(selectedEntry, catalogOfficial, currentSourceLabel())} large />
              </div>
            </div>
          </div>

          <div style={{ display: "flex", margin: "18px 0", padding: "10px 0", borderTop: "1px solid var(--ms-border)", borderBottom: "1px solid var(--ms-border)" }}>
            <InfoCell label={t("plugins.discover.info.version")} value={selectedEntry.latestVersion ? `v${selectedEntry.latestVersion}` : "-"} />
            <InfoCell label={t("plugins.discover.info.type")} value={selectedEntry.kind === "js" ? t("plugins.discover.kind.js") : t("plugins.discover.kind.native")} />
            <InfoCell label={t("plugins.discover.info.source")} value={t(`plugins.badge.${catalogOfficial ? "official" : "thirdParty"}`)} />
            {selectedEntry.category && <InfoCell label={t("plugins.discover.info.category")} value={selectedEntry.category} />}
            <InfoCell label={t("plugins.discover.info.access")} value={selectedEntry.kind === "js" ? t("plugins.discover.info.permissionsCount", String(selectedEntry.permissions.length)) : t("plugins.discover.info.fullAccess")} />
          </div>

          {(selectedEntry.tags?.length ?? 0) > 0 && (
            <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginBottom: 12 }}>
              {selectedEntry.tags?.map((tag) => (
                <span key={tag} style={{ fontSize: 11, padding: "1px 8px", borderRadius: 10, border: "1px solid var(--ms-border)", color: "var(--ms-text-secondary)" }}>{tag}</span>
              ))}
            </div>
          )}

          {selectedEntry.description && (
            <>
              <SectionLabel>{t("plugins.discover.about")}</SectionLabel>
              <p style={{ fontSize: 13, lineHeight: 1.65, color: "var(--ms-text-secondary)", margin: "6px 0 16px" }}>{selectedEntry.description}</p>
            </>
          )}

          {selectedEntry.withdrawn ? (
            <div style={{ fontSize: 12, color: "var(--ms-warning, #facc15)", marginBottom: 14 }}>{t("plugins.withdrawn.note")}</div>
          ) : !selectedEntry.compatible && (
            <div style={{ fontSize: 12, color: "var(--ms-warning, #facc15)", marginBottom: 14 }}>
              {selectedEntry.incompatibleReason ?? t("plugins.discover.incompatible")}
            </div>
          )}
          {selectedEntry.installedWithdrawn && !selectedEntry.withdrawn && (
            <div style={{ fontSize: 12, color: "var(--ms-warning, #facc15)", marginBottom: 14 }}>{t("plugins.withdrawn.installedNote")}</div>
          )}

          <SectionLabel>{t("plugins.discover.permissionsLabel")}</SectionLabel>
          {selectedEntry.kind === "js" ? (
            selectedEntry.permissions.length > 0 ? (
              <ul style={{ margin: "6px 0 16px", paddingLeft: 18, fontSize: 12.5, color: "var(--ms-text-secondary)", lineHeight: 1.7 }}>
                {selectedEntry.permissions.map((p) => <li key={p}>{permissionText(p)}</li>)}
              </ul>
            ) : (
              <p style={{ fontSize: 12.5, color: "var(--ms-text-secondary)", margin: "6px 0 16px" }}>{t("plugins.discover.noPermissions")}</p>
            )
          ) : (
            <p style={{ fontSize: 12.5, color: "var(--ms-text-secondary)", lineHeight: 1.6, margin: "6px 0 16px" }}>{t("plugins.discover.nativeNotice")}</p>
          )}

          {selectedEntry.homepage && (
            <a href={selectedEntry.homepage} target="_blank" rel="noreferrer" style={{ fontSize: 12.5, display: "inline-flex", alignItems: "center", gap: 4, color: "var(--ms-accent)" }}>
              <ExternalLink size={12} />
              {t("plugins.discover.homepage")}
            </a>
          )}

          {(notice || error) && (
            <div style={{ marginTop: 14 }}>
              {notice && <p style={{ fontSize: 12, color: "var(--ms-success, #4ade80)", margin: 0 }}>{notice}</p>}
              {error && <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: 0 }}>{error}</p>}
            </div>
          )}
        </div>
      ) : (
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
            <SectionLabel>{t("plugins.category.discover")}</SectionLabel>
            <div style={{ flex: 1 }} />
            <button type="button" className="ghost" title={t("plugins.refresh")} onClick={() => refreshCatalog(selectedSource, true)} style={{ display: "flex", padding: 6 }}>
              <RefreshCw size={14} />
            </button>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10, maxWidth: 460 }}>
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
          {!catalogLoading && !catalogError && catalog && catalog.length > 0 && (
            <>
              <div style={{ position: "relative", marginBottom: 10 }}>
                <Search size={14} style={{ position: "absolute", left: 10, top: 9, color: "var(--ms-text-disabled)" }} />
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder={t("plugins.discover.search")}
                  style={{ width: "100%", paddingLeft: 30, boxSizing: "border-box", borderRadius: 999 }}
                />
              </div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginBottom: 14 }}>
                {DISCOVER_FILTERS.map((f) => (
                  <button
                    key={f}
                    type="button"
                    onClick={() => setFilter(f)}
                    style={{
                      borderRadius: 999, padding: "3px 12px", fontSize: 11.5, cursor: "pointer",
                      border: "1px solid var(--ms-border)",
                      background: filter === f ? "var(--ms-accent)" : "transparent",
                      color: filter === f ? "var(--ms-accent-on, #fff)" : "var(--ms-text-secondary)",
                    }}
                  >
                    {t(`plugins.discover.filter.${f}`)} <span style={{ opacity: 0.7 }}>{filterCount(f)}</span>
                  </button>
                ))}
              </div>

              {visibleEntries.length === 0 && (
                <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("plugins.discover.noMatches")}</div>
              )}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))", gap: 12 }}>
                {visibleEntries.map((entry) => (
                  <div
                    key={entry.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => setSelectedEntryId(entry.id)}
                    onKeyDown={(e) => { if (e.key === "Enter") setSelectedEntryId(entry.id); }}
                    style={{
                      display: "flex", gap: 12, alignItems: "center", textAlign: "left",
                      border: "1px solid var(--ms-border)", borderRadius: 12, padding: 12,
                      background: "var(--ms-bg-surface)", cursor: "pointer",
                    }}
                  >
                    <PluginAvatar id={entry.id} name={entry.name} size={56} iconUrl={entry.hasIcon ? api.pluginCatalogIconUrl(selectedSource, entry.id) : undefined} />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 13.5, fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{entry.name}</div>
                      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        {entry.kind === "js" ? t("plugins.discover.kind.js") : t("plugins.discover.kind.native")}
                        {entry.author ? ` · ${entry.author}` : ""}
                      </div>
                      {entry.description && (
                        <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", lineHeight: 1.4, marginTop: 3, display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical", overflow: "hidden" }}>
                          {entry.description}
                        </div>
                      )}
                    </div>
                    <GetButton entry={entry} busy={installingCatalogId === entry.id} onGet={() => installFromCatalog(entry, catalogOfficial, currentSourceLabel())} />
                  </div>
                ))}
              </div>
            </>
          )}

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
