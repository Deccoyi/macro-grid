import { useState } from "react";
import { CircleGauge, FlaskConical, Globe, Image as ImageIcon, Puzzle, RectangleHorizontal, ShieldAlert, SlidersHorizontal, ToggleRight, Type, type LucideIcon } from "lucide-react";
import type { WidgetType } from "@macro/renderer";
import type { PluginWidgetInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { usePluginWidgets } from "../state/usePluginWidgets";
import type { DictKey } from "../i18n/tr";
import { matchesSearch } from "./paletteSearch";

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

interface WidgetPaletteProps {
  onAdd: (type: WidgetType) => void;
  onAddPluginWidget: (info: PluginWidgetInfo) => void;
  /** True while no page tab is open — clicking a tile would otherwise add to a page nothing visible
   * represents as open (see DocumentArea's empty state). */
  disabled?: boolean;
}

export function WidgetPalette({ onAdd, onAddPluginWidget, disabled }: WidgetPaletteProps) {
  const { t } = useT();
  const allPluginWidgets = usePluginWidgets();
  const [query, setQuery] = useState("");
  const pluginWidgets = allPluginWidgets.filter((w) => matchesSearch(query, w.name, w.description, w.pluginName));
  const builtIn = PALETTE.filter((item) => matchesSearch(query, t(item.key)));
  // One group per plugin, in the order the plugins were listed.
  const groups = [...new Set(pluginWidgets.map((w) => w.plugin))].map((plugin) => ({ plugin, name: pluginWidgets.find((w) => w.plugin === plugin)!.pluginName, widgets: pluginWidgets.filter((w) => w.plugin === plugin) }));
  return (
    <div style={{ padding: 8, display: "flex", flexDirection: "column", gap: 8, height: "100%", overflow: "auto" }}>
      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>
        {t("palette.title")}
      </div>
      <input
        type="search"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder={t("palette.search")}
        aria-label={t("palette.search")}
        style={{ height: 24, padding: "0 6px", fontSize: 12, background: "var(--ms-bg-input, var(--ms-bg-surface-raised))", border: "1px solid var(--ms-border)", borderRadius: 3, color: "var(--ms-text-primary)" }}
      />
      {builtIn.length === 0 && pluginWidgets.length === 0 && <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("palette.noMatch")}</div>}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 6 }}>
        {builtIn.map((item) => {
          const Icon = item.icon;
          return (
            <button
              key={item.type}
              onClick={() => onAdd(item.type)}
              title={item.type === "web" ? t("fields.web.experimentalText") : undefined}
              disabled={disabled}
              style={{
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
              }}
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
        })}
      </div>
      {groups.map((group) => (
        <div key={group.plugin} style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em", display: "flex", alignItems: "center", gap: 4 }}>
            <Puzzle size={11} strokeWidth={2} />
            <span style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={t("palette.pluginWidgets")}>{group.name}</span>
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 6 }}>
            {group.widgets.map((info) => (
              <button
                key={info.widget}
                onClick={() => onAddPluginWidget(info)}
                title={[info.description, info.verified ? null : t("palette.unverifiedHint")].filter(Boolean).join("\n") || undefined}
                disabled={disabled}
                style={{
                  height: 52, position: "relative", display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 4,
                  background: "var(--ms-bg-surface-raised)", border: "1px solid var(--ms-border)", borderRadius: 3,
                  color: disabled ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)", cursor: disabled ? "default" : "pointer", opacity: disabled ? 0.6 : 1,
                }}
              >
                <Puzzle size={16} strokeWidth={1.75} />
                <span style={{ fontSize: 10.5, maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{info.name}</span>
                {!info.verified && (
                  <ShieldAlert size={11} strokeWidth={2} color="var(--ms-warning, #facc15)" aria-label={t("palette.unverified")} style={{ position: "absolute", top: 4, right: 4 }} />
                )}
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
