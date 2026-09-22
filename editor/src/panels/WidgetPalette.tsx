import type { WidgetType } from "@macro/renderer";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";

const PALETTE: { type: WidgetType; key: DictKey }[] = [
  { type: "button", key: "widget.type.button" },
  { type: "toggle", key: "widget.type.toggle" },
  { type: "slider", key: "widget.type.slider" },
  { type: "knob", key: "widget.type.knob" },
  { type: "label", key: "widget.type.label" },
  { type: "image", key: "widget.type.image" },
  { type: "web", key: "widget.type.web" },
];

export interface WidgetPaletteProps {
  onAdd: (type: WidgetType) => void;
}

export function WidgetPalette({ onAdd }: WidgetPaletteProps) {
  const { t } = useT();
  return (
    <div style={{ padding: 10, display: "flex", flexDirection: "column", gap: 6 }}>
      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>
        {t("palette.title")}
      </div>
      {PALETTE.map((item) => (
        <button key={item.type} className="ghost" style={{ textAlign: "left" }} onClick={() => onAdd(item.type)}>
          {t(item.key)}
        </button>
      ))}
    </div>
  );
}
