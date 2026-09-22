import type { FieldGroupProps } from "./AppearanceFields";
import { SectionLabel } from "./controls";

const num = (v: unknown, fallback: number) => (typeof v === "number" ? v : fallback);

/** For "slider"/"knob": the value range they sweep, plus an honest note about what actually happens when you drag them today. */
export function RangeFields({ widget, onChange }: FieldGroupProps) {
  const props = widget.props ?? {};
  const setProp = (key: string, value: number) => onChange((w) => { w.props = { ...(w.props ?? {}), [key]: value }; });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <SectionLabel>İçerik</SectionLabel>
      <label className="field">
        Üst yazı (opsiyonel)
        <input type="text" value={widget.text ?? ""} onChange={(e) => onChange((w) => { w.text = e.target.value; })} />
      </label>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
        <label className="field">
          Min
          <input type="number" value={num(props.min, 0)} onChange={(e) => setProp("min", Number(e.target.value))} />
        </label>
        <label className="field">
          Maks
          <input type="number" value={num(props.max, 100)} onChange={(e) => setProp("max", Number(e.target.value))} />
        </label>
        <label className="field">
          Adım
          <input type="number" min={0.01} value={num(props.step, 1)} onChange={(e) => setProp("step", Number(e.target.value))} />
        </label>
      </div>

      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>
        Bu widget'ın sürüklenen değerini şu an hiçbir şey kullanmıyor — ses seviyesi gibi gerçek bir bağlantı, o değeri işleyecek bir plugin (Aşama 6, "Ses" plugin'i) kurulunca çalışacak.
        "Basınca"/"Uzun basınca" gibi olaylara aksiyon bağlamak (ör. sustur) yine de çalışır.
      </p>
    </div>
  );
}
