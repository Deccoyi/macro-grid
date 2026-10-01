import { Ban, Plus, Trash2, Variable } from "lucide-react";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import type { DictKey } from "../../i18n/tr";
import { VariablePicker } from "../VariablePicker";
import { Seg, SelectInput } from "../fields/controls";
import { isValueless, newCondition, type EditCase, type EditCondition } from "./conditionEditing";
import { allowsOrdering, fitConditionToVariable, isInvalidNumber, normalizeBoolText, valueInputFor, type ValueInput } from "./variableTypes";

const OPERATOR_KEYS: Record<EditCondition["operator"], DictKey> = {
  ">": "dynamic.operator.>",
  ">=": "dynamic.operator.>=",
  "<": "dynamic.operator.<",
  "<=": "dynamic.operator.<=",
  "==": "dynamic.operator.==",
  "!=": "dynamic.operator.!=",
  between: "dynamic.operator.between",
  unavailable: "dynamic.operator.unavailable",
  available: "dynamic.operator.available",
};

const COMBINATOR_KEYS: Record<EditCase["combinator"], DictKey> = { and: "dynamic.combinator.and", or: "dynamic.combinator.or", xor: "dynamic.combinator.xor" };

/** The condition part of an edit: one or more comparisons joined by one combinator. Shared by the dynamic-rule dialog and the If step of an action list. */
export type ConditionEdit = Pick<EditCase, "combinator" | "conditions">;

interface ConditionEditorProps {
  value: ConditionEdit;
  variableCatalog: VariableInfo[];
  onChange: (value: ConditionEdit) => void;
}

export function ConditionEditor({ value, variableCatalog, onChange }: ConditionEditorProps) {
  const { t } = useT();
  const update = (fn: (draft: ConditionEdit) => void) => {
    const draft = structuredClone({ combinator: value.combinator, conditions: value.conditions });
    fn(draft);
    onChange(draft);
  };

  return (
    <div className={value.conditions.length > 1 ? "dz-conditions pf-nest" : "dz-conditions"}>
      {value.conditions.map((cond, ci) => {
        const variableInfo = variableCatalog.find((v) => v.name === cond.variable);
        const valueInput = valueInputFor(variableInfo);
        const operators = (Object.keys(OPERATOR_KEYS) as EditCondition["operator"][])
          .filter((op) => allowsOrdering(valueInput) || op === "==" || op === "!=" || isValueless(op) || op === cond.operator);
        const setValue = (field: "value" | "value2") => (v: string) => update((cc) => { cc.conditions[ci]![field] = v; });
        return (
          <div key={ci} className="dz-conditions">
            {ci > 0 && (
              <div className="dz-seg">
                <Seg<EditCase["combinator"]>
                  value={value.combinator}
                  onChange={(op) => update((cc) => { cc.combinator = op; })}
                  options={(["and", "or", "xor"] as const).map((op) => ({ value: op, label: t(COMBINATOR_KEYS[op]) }))}
                />
              </div>
            )}
            <div className="dz-cond">
              <div className="dz-subject">
                <VariablePicker
                  catalog={variableCatalog}
                  mode="bare"
                  onInsert={(name) => update((cc) => {
                    const target = cc.conditions[ci]!;
                    target.variable = name;
                    fitConditionToVariable(target, variableCatalog.find((v) => v.name === name), true);
                  })}
                  renderTrigger={(open) => (
                    <button type="button" className="dz-chip mono" onClick={open}>
                      <Variable size={11} />
                      <span className="pf-ellipsis">{cond.variable || t("dynamic.pickVariable")}</span>
                    </button>
                  )}
                />
              </div>

              <SelectInput
                label={t("dynamic.operator.label")}
                value={cond.operator}
                onChange={(op) => update((cc) => {
                  const target = cc.conditions[ci]!;
                  target.operator = op as EditCondition["operator"];
                  fitConditionToVariable(target, variableCatalog.find((v) => v.name === target.variable));
                })}
              >
                {operators.map((op) => (
                  <option key={op} value={op}>{t(OPERATOR_KEYS[op])}</option>
                ))}
              </SelectInput>

              <div className="dz-value">
                {!isValueless(cond.operator) && <ConditionValue value={cond.value} onChange={setValue("value")} input={valueInput} invalid={isInvalidNumber(variableInfo, cond.value)} />}
                {cond.operator === "between" && (
                  <>
                    <span className="dz-dash">–</span>
                    <ConditionValue value={cond.value2} onChange={setValue("value2")} input={valueInput} />
                  </>
                )}
              </div>

              <button
                type="button"
                className={cond.negate ? "active pf-icon-btn small" : "ghost pf-icon-btn small"}
                aria-pressed={Boolean(cond.negate)}
                aria-label={t("dynamic.negate")}
                onClick={() => update((cc) => { cc.conditions[ci]!.negate = !cc.conditions[ci]!.negate; })}
                title={t("dynamic.negate")}
              >
                <Ban size={13} />
              </button>
              {value.conditions.length > 1 ? (
                <button type="button" className="ghost pf-icon-btn small" aria-label={t("dynamic.removeCondition")} title={t("dynamic.removeCondition")} onClick={() => update((cc) => { cc.conditions.splice(ci, 1); })}>
                  <Trash2 size={13} />
                </button>
              ) : <span />}
            </div>
          </div>
        );
      })}

      <button
        type="button"
        className="ghost pf-btn small"
        onClick={() => update((cc) => { cc.conditions.push(newCondition()); })}
        style={{ alignSelf: "flex-start", width: "auto" }}
      >
        <Plus size={12} /> {t("dynamic.addCondition")}
      </button>
    </div>
  );
}

/** The compared value of a condition: a true/false select for a boolean, a select of the declared values,
 * or free input (with the unit as a suffix for a number). A stored value outside the choices stays visible. */
function ConditionValue({ value, onChange, input, invalid }: { value: string; onChange: (v: string) => void; input: ValueInput; invalid?: boolean }) {
  const { t } = useT();
  if (input.kind !== "free") {
    const current = input.kind === "boolean" ? normalizeBoolText(value) : value;
    const choices = input.kind === "boolean"
      ? [{ value: "true", label: t("dynamic.bool.true") }, { value: "false", label: t("dynamic.bool.false") }]
      : input.values.map((v) => ({ value: v, label: v }));
    return (
      <SelectInput label={t("dynamic.value.label")} value={current} onChange={onChange}>
        {!choices.some((c) => c.value === current) && <option value={current}>{current}</option>}
        {choices.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
      </SelectInput>
    );
  }
  return (
    <>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-label={t("dynamic.value.label")}
        placeholder={input.unit ? `50 ${input.unit}` : t("dynamic.value.placeholder")}
        title={invalid ? t("dynamic.value.notNumber") : t("dynamic.value.hint")}
        aria-invalid={invalid || undefined}
        style={invalid ? { borderColor: "var(--ms-danger)" } : undefined}
      />
      {input.unit && <span className="dz-unit">{input.unit}</span>}
    </>
  );
}
