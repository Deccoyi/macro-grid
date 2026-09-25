import { Variable, X } from "lucide-react";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { VariablePicker } from "../VariablePicker";
import type { FieldGroupProps } from "./AppearanceFields";

const num = (v: unknown, fallback: number) => (typeof v === "number" ? v : fallback);
const str = (v: unknown): string | undefined => (typeof v === "string" && v ? v : undefined);

interface RangeFieldsProps extends FieldGroupProps {
  variableCatalog: VariableInfo[];
}

/** For "slider"/"knob": the value range they sweep, which live variable (if any) drives its position
 * from the server side, and — via the shared Inspector "Aksiyonlar" section below this — what a drag
 * commit actually does (bind "valueChange" there, e.g. to "Ana ses seviyesi"). */
export function RangeFields({ widget, onChange, variableCatalog }: RangeFieldsProps) {
  const { t } = useT();
  const props = widget.props ?? {};
  const setProp = (key: string, value: number) => onChange((w) => { w.props = { ...(w.props ?? {}), [key]: value }; });
  const valueVariable = str(props.valueVariable);

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

      <label className="field">
        {t("fields.range.valueVariable")}
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <VariablePicker
            catalog={variableCatalog}
            mode="bare"
            onInsert={(name) => onChange((w) => { w.props = { ...(w.props ?? {}), valueVariable: name }; })}
            renderTrigger={(open) => (
              <button type="button" className="ghost" onClick={open} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12 }}>
                <Variable size={11} />
                {valueVariable ?? t("fields.range.valueVariable.none")}
              </button>
            )}
          />
          {valueVariable && (
            <button
              type="button"
              className="ghost"
              title={t("fields.range.valueVariable.clear")}
              onClick={() => onChange((w) => { const p = { ...(w.props ?? {}) }; delete p.valueVariable; w.props = p; })}
              style={{ display: "flex", padding: 5 }}
            >
              <X size={12} />
            </button>
          )}
        </div>
      </label>

      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>
        {t("fields.range.note")}
      </p>
    </div>
  );
}
