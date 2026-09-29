import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import dynamicIconImports from "lucide-react/dynamicIconImports";
import { ChevronDown, ChevronRight, Circle } from "lucide-react";
import type { PluginInfo, PluginTreeItem } from "../api/types";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { setPluginTreeSelection, usePluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { usePluginTree } from "../state/usePluginTree";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";

/** An icon by lucide-react name — the same set a status item or a plugin manifest icon uses. Falls back to a
 * plain dot for a missing or unknown name (see IPluginTreeProvider's doc comment on PluginTreeItem.Icon). */
function RowIcon({ name }: { name?: string | null }) {
  const Loaded = useMemo(() => {
    const load = name ? dynamicIconImports[name as keyof typeof dynamicIconImports] : undefined;
    return load ? lazy(load) : null;
  }, [name]);
  if (!Loaded) return <Circle size={6} fill="currentColor" style={{ flex: "0 0 auto", opacity: 0.5 }} />;
  return (
    <Suspense fallback={<Circle size={6} fill="currentColor" style={{ flex: "0 0 auto", opacity: 0.5 }} />}>
      <Loaded size={13} style={{ flex: "0 0 auto" }} />
    </Suspense>
  );
}

function Row({
  depth, expandable, expanded, onToggleExpand, icon, label, selected, disabled, onClick,
}: {
  depth: number;
  expandable?: boolean;
  expanded?: boolean;
  onToggleExpand?: () => void;
  icon?: React.ReactNode;
  label: string;
  selected?: boolean;
  disabled?: boolean;
  onClick?: () => void;
}) {
  const [hovered, setHovered] = useState(false);
  return (
    <div
      tabIndex={disabled ? undefined : 0}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onClick={disabled ? undefined : onClick}
      style={{
        padding: `4px 10px 4px ${10 + depth * 16}px`,
        marginLeft: selected ? -2 : 0,
        borderLeft: selected ? "2px solid var(--ms-accent)" : "2px solid transparent",
        display: "flex",
        alignItems: "center",
        gap: 6,
        cursor: disabled ? "default" : "pointer",
        color: disabled ? "var(--ms-text-disabled)" : selected ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
        background: selected ? "var(--ms-accent-bg-muted)" : hovered ? "var(--ms-bg-surface-raised)" : "transparent",
      }}
    >
      {expandable ? (
        <span
          onClick={(e) => { e.stopPropagation(); onToggleExpand?.(); }}
          style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 14, flex: "0 0 auto", color: "inherit" }}
        >
          {expanded ? <ChevronDown size={13} strokeWidth={1.75} /> : <ChevronRight size={13} strokeWidth={1.75} />}
        </span>
      ) : (
        <span style={{ width: 14, flex: "0 0 auto" }} />
      )}
      {icon}
      <span style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
        {label}
      </span>
    </div>
  );
}

/** The Plugins tool window (docs/design/plugins-tool-window.md): one row per installed plugin, expandable
 * only when it implements the optional IPluginTreeProvider (PluginInfo.hasTreeItems). Nothing about a
 * plugin's own tree is fetched until this window is on screen (PanelHost only mounts an open tool window's
 * content) and, within it, not until the plugin row or a deeper node is actually expanded. Selecting a row
 * or a tree item shows its settings in the Properties tool window (see PluginTreeItemProperties.tsx) through
 * the module-level selection store, since neither tool window is an ancestor of the other. */
export function PluginsToolWindow() {
  const { t } = useT();
  const { clearProfileProperties } = useWorkspaceUi();
  const selection = usePluginTreeSelection();
  const tree = usePluginTree();

  const [plugins, setPlugins] = useState<PluginInfo[] | null>(null);
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    let cancelled = false;
    api.listPlugins().then((list) => { if (!cancelled) setPlugins(list); }).catch(() => { if (!cancelled) setPlugins([]); });
    return () => { cancelled = true; };
  }, []);

  const toggleTop = (plugin: PluginInfo) => {
    const willExpand = !expandedIds.has(plugin.id);
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (willExpand) next.add(plugin.id); else next.delete(plugin.id);
      return next;
    });
    tree.setExpanded(plugin.id, null, willExpand);
    if (willExpand) tree.ensureLevelLoaded(plugin.id, null);
  };

  const toggleNode = (pluginId: string, itemId: string) => {
    const key = `${pluginId}::${itemId}`;
    const willExpand = !expandedIds.has(key);
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (willExpand) next.add(key); else next.delete(key);
      return next;
    });
    tree.setExpanded(pluginId, itemId, willExpand);
    if (willExpand) tree.ensureLevelLoaded(pluginId, itemId);
  };

  const selectPlugin = (plugin: PluginInfo) => {
    clearProfileProperties();
    setPluginTreeSelection({ kind: "plugin", pluginId: plugin.id, pluginName: plugin.name, hasSettings: plugin.hasSettings });
  };

  const selectItem = (plugin: PluginInfo, item: PluginTreeItem) => {
    clearProfileProperties();
    setPluginTreeSelection({
      kind: "item", pluginId: plugin.id, pluginName: plugin.name, itemId: item.id, label: item.label,
      hasSettings: item.hasSettings ?? false,
    });
  };

  const renderItems = (plugin: PluginInfo, parentId: string | null, depth: number): React.ReactNode => {
    const level = tree.getLevel(plugin.id, parentId);
    if (level === "loading") return <LoadingRow depth={depth} text={t("app.loading")} />;
    if (level === "error") return <LoadingRow depth={depth} text={t("pluginsTree.loadError")} />;
    if (!level) return null;

    return (
      <>
        {level.items.map((item) => {
          const key = `${plugin.id}::${item.id}`;
          const expanded = expandedIds.has(key);
          const selected = selection?.kind === "item" && selection.pluginId === plugin.id && selection.itemId === item.id;
          return (
            <div key={item.id}>
              <Row
                depth={depth}
                expandable={item.hasChildren}
                expanded={expanded}
                onToggleExpand={() => toggleNode(plugin.id, item.id)}
                icon={<RowIcon name={item.icon} />}
                label={item.label}
                selected={selected}
                onClick={() => selectItem(plugin, item)}
              />
              {expanded && renderItems(plugin, item.id, depth + 1)}
            </div>
          );
        })}
        {level.continuationToken && (
          <Row depth={depth} label={t("pluginsTree.showMore")} onClick={() => tree.loadMore(plugin.id, parentId)} />
        )}
      </>
    );
  };

  return (
    <div style={{ flex: 1, overflowY: "auto", padding: "6px 0" }}>
      {plugins === null && (
        <div style={{ padding: "4px 10px", fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("app.loading")}</div>
      )}
      {plugins !== null && plugins.length === 0 && (
        <div style={{ padding: "4px 10px", fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("plugins.none")}</div>
      )}
      {plugins?.map((plugin) => {
        const expanded = expandedIds.has(plugin.id);
        const selected = selection?.kind === "plugin" && selection.pluginId === plugin.id;
        return (
          <div key={plugin.id}>
            <Row
              depth={0}
              expandable={plugin.hasTreeItems}
              expanded={expanded}
              onToggleExpand={() => toggleTop(plugin)}
              icon={plugin.hasIcon ? <img src={api.getPluginIconUrl(plugin.id)} alt="" width={13} height={13} style={{ flex: "0 0 auto", objectFit: "contain" }} /> : <RowIcon name={null} />}
              label={plugin.name}
              selected={selected}
              onClick={() => selectPlugin(plugin)}
            />
            {expanded && renderItems(plugin, null, 1)}
          </div>
        );
      })}
    </div>
  );
}

function LoadingRow({ depth, text }: { depth: number; text: string }) {
  return (
    <div style={{ padding: `4px 10px 4px ${10 + depth * 16 + 20}px`, fontSize: 12, color: "var(--ms-text-secondary)" }}>
      {text}
    </div>
  );
}
