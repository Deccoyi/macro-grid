import { Variable, X } from "lucide-react";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { VariablePicker } from "../VariablePicker";
import type { FieldGroupProps } from "./AppearanceFields";
import { Field, FieldGrid, NumberInput, TextInput } from "./controls";

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
    <>
      <Field label={t("fields.range.caption")}>
        <TextInput value={widget.text ?? ""} onChange={(v) => onChange((w) => { w.text = v; })} />
      </Field>

      <FieldGrid cols={3}>
        <Field single label={t("fields.range.min")}>
          <NumberInput value={num(props.min, 0)} onChange={(v) => setProp("min", v)} />
        </Field>
        <Field single label={t("fields.range.max")}>
          <NumberInput value={num(props.max, 100)} onChange={(v) => setProp("max", v)} />
        </Field>
        <Field single label={t("fields.range.step")}>
          <NumberInput min={0.01} value={num(props.step, 1)} onChange={(v) => setProp("step", v)} />
        </Field>
      </FieldGrid>

      <Field label={t("fields.range.valueVariable")} hint={t("fields.range.note")}>
        <div className="pf-row">
          <div className="grow">
            <VariablePicker
              catalog={variableCatalog.filter((v) => !v.name.toLowerCase().startsWith("self."))}
              mode="bare"
              onInsert={(name) => onChange((w) => { w.props = { ...(w.props ?? {}), valueVariable: name }; })}
              renderTrigger={(open) => (
                <button type="button" className="pf-ctl pf-row" onClick={open} style={{ width: "100%" }}>
                  <Variable size={12} />
                  <span className="pf-ellipsis">{valueVariable ?? t("fields.range.valueVariable.none")}</span>
                </button>
              )}
            />
          </div>
          {valueVariable && (
            <button
              type="button"
              className="ghost pf-icon-btn"
              title={t("fields.range.valueVariable.clear")}
              aria-label={t("fields.range.valueVariable.clear")}
              onClick={() => onChange((w) => { const p = { ...(w.props ?? {}) }; delete p.valueVariable; w.props = p; })}
            >
              <X size={14} />
            </button>
          )}
        </div>
      </Field>
    </>
  );
}
