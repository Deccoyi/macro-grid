import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";

const num = (v: unknown, fallback: number) => (typeof v === "number" ? v : fallback);

/** For "slider"/"knob": the value range they sweep, plus an honest note about what actually happens when you drag them today. */
export function RangeFields({ widget, onChange }: FieldGroupProps) {
  const { t } = useT();
  const props = widget.props ?? {};
  const setProp = (key: string, value: number) => onChange((w) => { w.props = { ...(w.props ?? {}), [key]: value }; });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <label className="field">
        {t("fields.range.caption")}
        <input type="text" value={widget.text ?? ""} onChange={(e) => onChange((w) => { w.text = e.target.value; })} />
      </label>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
        <label className="field">
          {t("fields.range.min")}
          <input type="number" value={num(props.min, 0)} onChange={(e) => setProp("min", Number(e.target.value))} />
        </label>
        <label className="field">
          {t("fields.range.max")}
          <input type="number" value={num(props.max, 100)} onChange={(e) => setProp("max", Number(e.target.value))} />
        </label>
        <label className="field">
          {t("fields.range.step")}
          <input type="number" min={0.01} value={num(props.step, 1)} onChange={(e) => setProp("step", Number(e.target.value))} />
        </label>
      </div>

      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>
        {t("fields.range.note")}
      </p>
    </div>
  );
}
