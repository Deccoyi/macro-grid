import type { WidgetType } from "@macro/renderer";

const PALETTE: { type: WidgetType; label: string }[] = [
  { type: "button", label: "Buton" },
  { type: "toggle", label: "Toggle" },
  { type: "slider", label: "Slider" },
  { type: "knob", label: "Knob" },
  { type: "label", label: "Etiket" },
  { type: "image", label: "Görsel" },
  { type: "web", label: "Web" },
];

export interface WidgetPaletteProps {
  onAdd: (type: WidgetType) => void;
}

export function WidgetPalette({ onAdd }: WidgetPaletteProps) {
  return (
    <div style={{ padding: 10, display: "flex", flexDirection: "column", gap: 6 }}>
      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>
        Widget ekle
      </div>
      {PALETTE.map((item) => (
        <button key={item.type} className="ghost" style={{ textAlign: "left" }} onClick={() => onAdd(item.type)}>
          {item.label}
        </button>
      ))}
    </div>
  );
}
