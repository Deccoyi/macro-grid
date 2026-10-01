import { useMemo, useRef, useState } from "react";
import { ChevronDown, ChevronRight, CircleGauge, FlaskConical, Globe, Image as ImageIcon, MoreVertical, Puzzle, RectangleHorizontal, ShieldAlert, SlidersHorizontal, ToggleRight, Type, type LucideIcon } from "lucide-react";
import type { WidgetType } from "@macro/renderer";
import type { PluginWidgetInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { usePluginWidgets } from "../state/usePluginWidgets";
import type { DictKey } from "../i18n/tr";
import { ContextMenu, type ContextMenuEntry } from "./ContextMenu";
import { matchesSearch } from "./paletteSearch";
import { groupToolbox, SEARCH_MIN_TILES, type ToolboxEntry, type ToolboxView } from "./toolboxLayout";

// Icon per widget type — see docs/ui/editor-icons.md ("Toolbox (widget types)").
const PALETTE: { type: WidgetType; key: DictKey; icon: LucideIcon }[] = [
  { type: "button", key: "widget.type.button", icon: RectangleHorizontal },
  { type: "toggle", key: "widget.type.toggle", icon: ToggleRight },
  { type: "slider", key: "widget.type.slider", icon: SlidersHorizontal },
  { type: "knob", key: "widget.type.knob", icon: CircleGauge },
  { type: "label", key: "widget.type.label", icon: Type },
  { type: "image", key: "widget.type.image", icon: ImageIcon },
  { type: "web", key: "widget.type.web", icon: Globe },
];

const VIEW_KEY = "macro-grid.editor.toolboxView";
const COLLAPSED_KEY = "macro-grid.editor.toolboxCollapsed";

/** The groups the person folded, remembered per person in this window's storage (a group id is "plugin:<id>" or "category:<name>"). */
function useCollapsedGroups(): [Set<string>, (id: string) => void] {
  const [collapsed, setCollapsed] = useState<Set<string>>(() => {
    try {
      const saved: unknown = JSON.parse(localStorage.getItem(COLLAPSED_KEY) ?? "[]");
      return new Set(Array.isArray(saved) ? saved.filter((v): v is string => typeof v === "string") : []);
    } catch {
      return new Set();
    }
  });
  const toggle = (id: string) =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      try {
        localStorage.setItem(COLLAPSED_KEY, JSON.stringify([...next]));
      } catch {
        // Storage can be unavailable; the folds then last until the window closes.
      }
      return next;
    });
  return [collapsed, toggle];
}

/** The way the Toolbox is arranged is remembered per person in this window's storage; a private window simply starts with the default. */
function useToolboxView(): [ToolboxView, (view: ToolboxView) => void] {
  const [view, setView] = useState<ToolboxView>(() => {
    try {
      const saved = localStorage.getItem(VIEW_KEY);
      return saved === "alphabetical" ? "alphabetical" : "plugin";
    } catch {
      return "plugin";
    }
  });
  const change = (next: ToolboxView) => {
    setView(next);
    try {
      localStorage.setItem(VIEW_KEY, next);
    } catch {
      // Storage can be unavailable; the choice then lasts until the window closes.
    }
  };
  return [view, change];
}

/**
 * A plugin's own icon. Only its shape is used (the alpha of the image), painted in the tile's text color, so it follows the theme and the hover state
 * like the built-in icons do. The file is drawn as an image, so nothing in it can run.
 */
function PluginIcon({ src }: { src: string }) {
  const mask = `url("${src}") center / contain no-repeat`;
  return <span aria-hidden style={{ width: 16, height: 16, display: "inline-block", background: "currentColor", WebkitMask: mask, mask }} />;
}

interface WidgetPaletteProps {
  onAdd: (type: WidgetType) => void;
  onAddPluginWidget: (info: PluginWidgetInfo) => void;
  /** True while no page tab is open — clicking a tile would otherwise add to a page nothing visible
   * represents as open (see DocumentArea's empty state). */
  disabled?: boolean;
}

type Item = { type: WidgetType; key: DictKey; icon: LucideIcon } | PluginWidgetInfo;

const tileBase = (disabled?: boolean): React.CSSProperties => ({
  height: 52,
  position: "relative",
  display: "flex",
  flexDirection: "column",
  alignItems: "center",
  justifyContent: "center",
  gap: 4,
  background: "var(--ms-bg-surface-raised)",
  border: "1px solid var(--ms-border)",
  borderRadius: 3,
  color: disabled ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)",
  cursor: disabled ? "default" : "pointer",
  opacity: disabled ? 0.6 : 1,
});

export function WidgetPalette({ onAdd, onAddPluginWidget, disabled }: WidgetPaletteProps) {
  const { t } = useT();
  const pluginWidgets = usePluginWidgets();
  const [query, setQuery] = useState("");
  const [view, setView] = useToolboxView();
  const [collapsed, toggleGroup] = useCollapsedGroups();
  const [menu, setMenu] = useState<{ x: number; y: number } | null>(null);
  const kebab = useRef<HTMLButtonElement>(null);

  const entries = useMemo<ToolboxEntry<Item>[]>(
    () => [
      ...PALETTE.map((item) => ({ name: t(item.key), builtin: true, item })),
      ...pluginWidgets.map((info) => ({ name: info.name, plugin: info.plugin, pluginName: info.pluginName, builtin: false, item: info as Item })),
    ],
    [pluginWidgets, t],
  );
  const showSearch = entries.length > SEARCH_MIN_TILES || query !== "";
  const matching = entries.filter((e) => {
    const info = e.builtin ? null : (e.item as PluginWidgetInfo);
    return matchesSearch(query, e.name, info?.description, info?.pluginName, info?.category);
  });
  const groups = groupToolbox(matching, view);
  // While something is searched for, every group is open so a match is never hidden in a folded one; an untitled group has no header to fold.
  const isFolded = (id: string) => query === "" && collapsed.has(id);

  const openMenu = () => {
    const rect = kebab.current?.getBoundingClientRect();
    if (rect) setMenu({ x: Math.max(8, rect.right - 200), y: rect.bottom + 2 });
  };
  const menuItems: ContextMenuEntry[] = [
    { label: t("palette.view.plugin"), checked: view === "plugin", onSelect: () => setView("plugin") },
    { label: t("palette.view.alphabetical"), checked: view === "alphabetical", onSelect: () => setView("alphabetical") },
  ];

  const renderTile = (entry: ToolboxEntry<Item>) => {
    if (entry.builtin) {
      const item = entry.item as (typeof PALETTE)[number];
      const Icon = item.icon;
      return (
        <button
          key={item.type}
          onClick={() => onAdd(item.type)}
          title={item.type === "web" ? t("fields.web.experimentalText") : undefined}
          disabled={disabled}
          style={tileBase(disabled)}
          onMouseEnter={(e) => { if (!disabled) { e.currentTarget.style.borderColor = "var(--ms-border-strong)"; e.currentTarget.style.color = "var(--ms-text-primary)"; } }}
          onMouseLeave={(e) => { e.currentTarget.style.borderColor = "var(--ms-border)"; e.currentTarget.style.color = disabled ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)"; }}
        >
          <Icon size={16} strokeWidth={1.75} />
          <span style={{ fontSize: 10.5 }}>{t(item.key)}</span>
          {item.type === "web" && (
            <FlaskConical size={11} strokeWidth={2} color="var(--ms-warning, #facc15)" aria-label={t("fields.web.experimentalTitle")} style={{ position: "absolute", top: 4, right: 4 }} />
          )}
        </button>
      );
    }
    const info = entry.item as PluginWidgetInfo;
    return (
      <button
        key={`${info.plugin}:${info.widget}`}
        onClick={() => onAddPluginWidget(info)}
        title={[info.description, info.verified ? null : t("palette.unverifiedHint")].filter(Boolean).join("\n") || undefined}
        disabled={disabled}
        style={tileBase(disabled)}
        onMouseEnter={(e) => { if (!disabled) { e.currentTarget.style.borderColor = "var(--ms-border-strong)"; e.currentTarget.style.color = "var(--ms-text-primary)"; } }}
        onMouseLeave={(e) => { e.currentTarget.style.borderColor = "var(--ms-border)"; e.currentTarget.style.color = disabled ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)"; }}
      >
        {info.icon ? <PluginIcon src={info.icon} /> : <Puzzle size={16} strokeWidth={1.75} />}
        <span style={{ fontSize: 10.5, maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{info.name}</span>
        {!info.verified && (
          <ShieldAlert size={11} strokeWidth={2} color="var(--ms-warning, #facc15)" aria-label={t("palette.unverified")} style={{ position: "absolute", top: 4, right: 4 }} />
        )}
      </button>
    );
  };

  return (
    <div style={{ padding: 8, display: "flex", flexDirection: "column", gap: 8, height: "100%", overflow: "auto" }}>
      <div style={{ display: "flex", alignItems: "center", minHeight: 20 }}>
        <span style={{ flex: 1, fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>{t("palette.title")}</span>
        <button
          ref={kebab}
          type="button"
          className="ghost"
          title={t("palette.menu")}
          aria-label={t("palette.menu")}
          aria-haspopup="menu"
          onClick={openMenu}
          style={{ width: 20, height: 20, padding: 0, display: "flex", alignItems: "center", justifyContent: "center", border: "none", background: "transparent", color: "var(--ms-text-secondary)", cursor: "pointer" }}
        >
          <MoreVertical size={14} strokeWidth={1.75} />
        </button>
      </div>
      {menu && <ContextMenu x={menu.x} y={menu.y} items={menuItems} onClose={() => setMenu(null)} />}
      {showSearch && (
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t("palette.search")}
          aria-label={t("palette.search")}
          style={{ height: 24, padding: "0 6px", fontSize: 12, background: "var(--ms-bg-input, var(--ms-bg-surface-raised))", border: "1px solid var(--ms-border)", borderRadius: 3, color: "var(--ms-text-primary)" }}
        />
      )}
      {query !== "" && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{matching.length === 0 ? t("palette.noMatch") : t("palette.results", String(matching.length))}</div>}
      {groups.map((group) => (
        <div key={group.id} style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {group.title !== null && (
            <button
              type="button"
              onClick={() => toggleGroup(group.id)}
              aria-expanded={!isFolded(group.id)}
              style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em", display: "flex", alignItems: "center", gap: 4, background: "transparent", border: "none", padding: 0, cursor: "pointer", textAlign: "left" }}
            >
              {isFolded(group.id) ? <ChevronRight size={12} strokeWidth={2} /> : <ChevronDown size={12} strokeWidth={2} />}
              {group.plugin && view === "plugin" && <Puzzle size={11} strokeWidth={2} />}
              <span style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={group.plugin && view === "plugin" ? t("palette.pluginWidgets") : undefined}>{group.title}</span>
            </button>
          )}
          {!isFolded(group.id) && <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 6 }}>{group.entries.map(renderTile)}</div>}
        </div>
      ))}
    </div>
  );
}
