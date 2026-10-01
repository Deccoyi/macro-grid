import { Plus, Trash2, Variable } from "lucide-react";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import type { DictKey } from "../../i18n/tr";
import { VariablePicker } from "../VariablePicker";
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
    <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
      {value.conditions.map((cond, ci) => {
        const variableInfo = variableCatalog.find((v) => v.name === cond.variable);
        const valueInput = valueInputFor(variableInfo);
        const operators = (Object.keys(OPERATOR_KEYS) as EditCondition["operator"][])
          .filter((op) => allowsOrdering(valueInput) || op === "==" || op === "!=" || isValueless(op) || op === cond.operator);
        const setValue = (field: "value" | "value2") => (v: string) => update((cc) => { cc.conditions[ci]![field] = v; });
        return (
        <div key={ci} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {ci > 0 && (
            <div className="seg" style={{ width: "auto", alignSelf: "flex-start" }}>
              {(["and", "or", "xor"] as const).map((op) => (
                <button
                  key={op}
                  type="button"
                  className={value.combinator === op ? "on" : undefined}
                  onClick={() => update((cc) => { cc.combinator = op; })}
                >
                  {t(COMBINATOR_KEYS[op])}
                </button>
              ))}
            </div>
          )}
          <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <button
              type="button"
              className={cond.negate ? "active" : "ghost"}
              onClick={() => update((cc) => { cc.conditions[ci]!.negate = !cc.conditions[ci]!.negate; })}
              title={t("dynamic.negate")}
              style={{ fontSize: 11 }}
            >
              {t("dynamic.negate")}
            </button>

            <VariablePicker
              catalog={variableCatalog}
              mode="bare"
              onInsert={(name) => update((cc) => {
                const target = cc.conditions[ci]!;
                target.variable = name;
                fitConditionToVariable(target, variableCatalog.find((v) => v.name === name), true);
              })}
              renderTrigger={(open) => (
                <button type="button" className="ghost" onClick={open} style={chipStyle}>
                  <Variable size={11} />
                  {cond.variable || t("dynamic.pickVariable")}
                </button>
              )}
            />

            <select
              value={cond.operator}
              onChange={(e) => update((cc) => {
                const target = cc.conditions[ci]!;
                target.operator = e.target.value as EditCondition["operator"];
                fitConditionToVariable(target, variableCatalog.find((v) => v.name === target.variable));
              })}
              style={{ width: "auto" }}
            >
              {operators.map((op) => (
                <option key={op} value={op}>{t(OPERATOR_KEYS[op])}</option>
              ))}
            </select>

            {!isValueless(cond.operator) && <ConditionValue value={cond.value} onChange={setValue("value")} input={valueInput} invalid={isInvalidNumber(variableInfo, cond.value)} />}
            {cond.operator === "between" && (
              <>
                <span style={{ color: "var(--ms-text-disabled)", fontSize: 12 }}>–</span>
                <ConditionValue value={cond.value2} onChange={setValue("value2")} input={valueInput} />
              </>
            )}

            <div style={{ flex: 1 }} />
            {value.conditions.length > 1 && (
              <button type="button" className="ghost" onClick={() => update((cc) => { cc.conditions.splice(ci, 1); })} style={{ display: "flex", padding: 4 }}>
                <Trash2 size={13} />
              </button>
            )}
          </div>
        </div>
        );
      })}

      <button
        type="button"
        className="ghost"
        onClick={() => update((cc) => { cc.conditions.push(newCondition()); })}
        style={{ alignSelf: "flex-start", display: "flex", alignItems: "center", gap: 5, fontSize: 12 }}
      >
        <Plus size={11} /> {t("dynamic.addCondition")}
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
      <select
        value={current}
        onChange={(e) => onChange(e.target.value)}
        title={t(input.kind === "boolean" ? "dynamic.value.boolHint" : "dynamic.value.choiceHint")}
        style={{ width: "auto", minWidth: 108 }}
      >
        {!choices.some((c) => c.value === current) && <option value={current}>{current}</option>}
        {choices.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
      </select>
    );
  }
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={input.unit ? `50 ${input.unit}` : t("dynamic.value.placeholder")}
        title={invalid ? t("dynamic.value.notNumber") : t("dynamic.value.hint")}
        aria-invalid={invalid || undefined}
        style={{ width: 108, textAlign: "center", fontFamily: "ui-monospace, monospace", ...(invalid ? { borderColor: "var(--ms-danger)" } : null) }}
      />
      {input.unit && <span style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{input.unit}</span>}
    </span>
  );
}

export const chipStyle: React.CSSProperties = {
  display: "inline-flex", alignItems: "center", gap: 5, background: "var(--ms-bg-inset)",
  border: "1px solid var(--ms-border)", borderRadius: 4, padding: "4px 8px",
  fontSize: 12, fontFamily: "ui-monospace, monospace", color: "var(--ms-text-primary)",
};

