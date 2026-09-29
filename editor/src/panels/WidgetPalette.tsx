import { CircleGauge, Globe, Image as ImageIcon, RectangleHorizontal, SlidersHorizontal, ToggleRight, Type, type LucideIcon } from "lucide-react";
import type { WidgetType } from "@macro/renderer";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";

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
  /** True while no page tab is open — clicking a tile would otherwise add to a page nothing visible
   * represents as open (see DocumentArea's empty state). */
  disabled?: boolean;
}

export function WidgetPalette({ onAdd, disabled }: WidgetPaletteProps) {
  const { t } = useT();
  return (
    <div style={{ padding: 8, display: "flex", flexDirection: "column", gap: 8, height: "100%", overflow: "auto" }}>
      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>
        {t("palette.title")}
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(2, 1fr)", gap: 6 }}>
        {PALETTE.map((item) => {
          const Icon = item.icon;
          return (
            <button
              key={item.type}
              onClick={() => onAdd(item.type)}
              title={item.type === "web" ? t("fields.web.experimentalText") : undefined}
              disabled={disabled}
              style={{
                height: 52,
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
              {item.type === "web" && <span style={{ fontSize: 8.5, marginTop: -3, textTransform: "uppercase", letterSpacing: ".04em", color: "var(--ms-accent)" }}>{t("widget.experimental")}</span>}
            </button>
          );
        })}
      </div>
    </div>
  );
}
