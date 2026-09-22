import type { FieldGroupProps } from "./AppearanceFields";
import { SectionLabel } from "./controls";

/** For "image" widgets: a picture (props.src) with an optional caption underneath. */
export function ImageFields({ widget, onChange }: FieldGroupProps) {
  const src = typeof widget.props?.src === "string" ? widget.props.src : "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <SectionLabel>İçerik</SectionLabel>
      <label className="field">
        Görsel URL
        <input
          type="text"
          value={src}
          onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), src: e.target.value }; })}
          placeholder="https://... veya data:image/..."
        />
      </label>
      <label className="field">
        Altyazı (opsiyonel)
        <input
          type="text"
          value={widget.text ?? ""}
          onChange={(e) => onChange((w) => { w.text = e.target.value; })}
        />
      </label>
    </div>
  );
}
