import { useState } from "react";
import { ArrowRight, Plus, Trash2, Variable, X } from "lucide-react";
import type { DynamicBinding } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { ColorField } from "../fields/controls";
import { IconPicker } from "../IconPicker";
import { useT } from "../../i18n/I18nContext";
import type { DictKey } from "../../i18n/tr";
import { VariablePicker } from "../VariablePicker";
import { combinatorOf, fromConditionNode, newCase, newCondition, toConditionNode, type EditCase, type EditCondition } from "./conditionEditing";
import { useBackdropClose } from "../../components/useBackdropClose";
import { allowsOrdering, fitConditionToVariable, normalizeBoolText, valueInputFor, type ValueInput } from "./variableTypes";

const OPERATOR_KEYS: Record<EditCondition["operator"], DictKey> = {
  ">": "dynamic.operator.>",
  ">=": "dynamic.operator.>=",
  "<": "dynamic.operator.<",
  "<=": "dynamic.operator.<=",
  "==": "dynamic.operator.==",
  "!=": "dynamic.operator.!=",
  between: "dynamic.operator.between",
};

/** What each rule's "then" value is: a free color, free text (may contain {variables}), an icon, or a fixed set of choices (e.g. animation names). */
export type ResultKind = "color" | "text" | "icon" | { select: { value: string; label: string }[] };

interface DynamizeModalProps {
  propertyLabel: string;
  binding: DynamicBinding | undefined;
  variableCatalog: VariableInfo[];
  resultKind?: ResultKind;
  /** Color the icon choices are baked with (the widget's text color), for the "icon" result kind. */
  iconColor?: string;
  onSave: (binding: DynamicBinding | null) => void;
  onClose: () => void;
}

const COMBINATOR_KEYS: Record<EditCase["combinator"], DictKey> = { and: "dynamic.combinator.and", or: "dynamic.combinator.or", xor: "dynamic.combinator.xor" };

export function DynamizeModal({ propertyLabel, binding, variableCatalog, resultKind = "color", iconColor, onSave, onClose }: DynamizeModalProps) {
  const { t } = useT();
  const backdrop = useBackdropClose(onClose);
  const defaultResult = typeof resultKind === "object" ? (resultKind.select[0]?.value ?? "") : resultKind === "color" ? "#c0392b" : "";
  const wideResult = resultKind === "text" || resultKind === "icon";
  const [unsupported] = useState(() => binding !== undefined && binding.cases.some((c) => fromConditionNode(c.condition) === null));
  const [cases, setCases] = useState<EditCase[]>(() =>
    binding && !unsupported
      ? binding.cases.map((c) => ({ combinator: combinatorOf(c.condition), conditions: fromConditionNode(c.condition) ?? [newCondition()], result: c.result }))
      : [newCase(defaultResult)],
  );
  const [defaultValue, setDefaultValue] = useState(binding?.default ?? "");

  const updateCase = (index: number, fn: (c: EditCase) => void) =>
    setCases((prev) => prev.map((c, i) => (i === index ? withMutation(c, fn) : c)));

  const save = () => {
    const valid = cases.filter((c) => c.conditions.every((cond) => cond.variable && cond.value));
    if (valid.length === 0) { onSave(null); onClose(); return; }
    onSave({
      cases: valid.map((c) => ({ condition: toConditionNode(c), result: c.result })),
      default: defaultValue || undefined,
    });
    onClose();
  };

  const remove = () => { onSave(null); onClose(); };

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 60, display: "flex", alignItems: "center", justifyContent: "center" }} {...backdrop}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 600, maxHeight: "82vh", background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border-strong)", display: "flex", flexDirection: "column" }}
      >
        {/* Header — a window title bar, not a web modal's rounded card top: square corners, no radius
           anywhere in this shell (see docs/ui/ui-guidelines.md: "like a window", never like a web modal). */}
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "13px 18px", borderBottom: "1px solid var(--ms-border)" }}>
          <div style={{ width: 26, height: 26, background: "var(--ms-accent-bg-muted)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
            <Variable size={14} color="var(--ms-accent-hover)" />
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
            <div style={{ fontSize: 14, fontWeight: 600 }}>{t("dynamic.title")}</div>
            <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", fontFamily: "ui-monospace, monospace" }}>{propertyLabel}</div>
          </div>
          <div style={{ flex: 1 }} />
          <button type="button" className="ghost" onClick={onClose} aria-label={t("header.close")} style={{ display: "flex", padding: 5 }}>
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: "16px 20px", display: "flex", flexDirection: "column", gap: 10, overflowY: "auto" }}>
          {unsupported && (
            <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", padding: 8 }}>
              {t("dynamic.unsupported")}
            </div>
          )}

          {cases.map((c, i) => (
            <div key={i} style={{ border: "1px solid var(--ms-border)", background: "var(--ms-bg-canvas)", padding: "12px 14px", display: "flex", flexDirection: "column", gap: 9 }}>
              <Keyword>{i === 0 ? t("dynamic.if") : t("dynamic.elseIf")}</Keyword>

              {c.conditions.map((cond, ci) => {
                const valueInput = valueInputFor(variableCatalog.find((v) => v.name === cond.variable));
                const operators = (Object.keys(OPERATOR_KEYS) as EditCondition["operator"][])
                  .filter((op) => allowsOrdering(valueInput) || op === "==" || op === "!=" || op === cond.operator);
                const setValue = (field: "value" | "value2") => (v: string) => updateCase(i, (cc) => { cc.conditions[ci]![field] = v; });
                return (
                <div key={ci} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {ci > 0 && (
                    <div className="seg" style={{ width: "auto", alignSelf: "flex-start" }}>
                      {(["and", "or", "xor"] as const).map((op) => (
                        <button
                          key={op}
                          type="button"
                          className={c.combinator === op ? "on" : undefined}
                          onClick={() => updateCase(i, (cc) => { cc.combinator = op; })}
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
                      onClick={() => updateCase(i, (cc) => { cc.conditions[ci]!.negate = !cc.conditions[ci]!.negate; })}
                      title={t("dynamic.negate")}
                      style={{ fontSize: 11 }}
                    >
                      {t("dynamic.negate")}
                    </button>

                    <VariablePicker
                      catalog={variableCatalog}
                      mode="bare"
                      onInsert={(name) => updateCase(i, (cc) => {
                        const target = cc.conditions[ci]!;
                        target.variable = name;
                        fitConditionToVariable(target, variableCatalog.find((v) => v.name === name));
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
                      onChange={(e) => updateCase(i, (cc) => { cc.conditions[ci]!.operator = e.target.value as EditCondition["operator"]; })}
                      style={{ width: "auto" }}
                    >
                      {operators.map((op) => (
                        <option key={op} value={op}>{t(OPERATOR_KEYS[op])}</option>
                      ))}
                    </select>

                    <ConditionValue value={cond.value} onChange={setValue("value")} input={valueInput} />
                    {cond.operator === "between" && (
                      <>
                        <span style={{ color: "var(--ms-text-disabled)", fontSize: 12 }}>–</span>
                        <ConditionValue value={cond.value2} onChange={setValue("value2")} input={valueInput} />
                      </>
                    )}

                    <div style={{ flex: 1 }} />
                    {c.conditions.length > 1 && (
                      <button type="button" className="ghost" onClick={() => updateCase(i, (cc) => { cc.conditions.splice(ci, 1); })} style={{ display: "flex", padding: 4 }}>
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
                onClick={() => updateCase(i, (cc) => { cc.conditions.push(newCondition()); })}
                style={{ alignSelf: "flex-start", display: "flex", alignItems: "center", gap: 5, fontSize: 12 }}
              >
                <Plus size={11} /> {t("dynamic.addCondition")}
              </button>

              <hr className="sep" style={{ margin: "1px 0" }} />

              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <Keyword muted>{t("dynamic.then")}</Keyword>
                <ArrowRight size={13} color="var(--ms-border-strong)" />
                <div style={{ width: wideResult ? 260 : 150 }}>
                  <ResultInput value={c.result} onChange={(v) => updateCase(i, (cc) => { cc.result = v; })} kind={resultKind} iconColor={iconColor} />
                </div>
                <div style={{ flex: 1 }} />
                {cases.length > 1 && (
                  <button type="button" className="ghost" onClick={() => setCases((prev) => prev.filter((_, idx) => idx !== i))} style={{ color: "var(--ms-danger)", fontSize: 12 }}>
                    {t("dynamic.removeRule")}
                  </button>
                )}
              </div>
            </div>
          ))}

          <button
            type="button"
            className="ghost"
            onClick={() => setCases((prev) => [...prev, newCase(defaultResult)])}
            style={{ border: "1px dashed var(--ms-border-strong)", padding: 9, display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }}
          >
            <Plus size={14} /> {t("dynamic.newRule")}
          </button>

          <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 14px", border: "1px dashed var(--ms-border)" }}>
            <Keyword muted>{t("dynamic.else")}</Keyword>
            <div style={{ flex: 1, fontSize: 12, color: "var(--ms-text-disabled)" }}>{t("dynamic.elseHint")}</div>
            <div style={{ width: wideResult ? 260 : 150 }}>
              <ResultInput value={defaultValue} onChange={setDefaultValue} kind={resultKind} iconColor={iconColor} allowEmpty />
            </div>
          </div>
        </div>

        {/* Footer */}
        <div style={{ display: "flex", alignItems: "center", padding: "14px 20px", borderTop: "1px solid var(--ms-border)" }}>
          <button type="button" className="ghost" onClick={remove} style={{ color: "var(--ms-danger)" }}>{t("dynamic.remove")}</button>
          <div style={{ flex: 1 }} />
          <div style={{ display: "flex", gap: 8 }}>
            <button type="button" className="ghost" onClick={onClose}>{t("dynamic.cancel")}</button>
            <button type="button" className="primary" onClick={save}>{t("dynamic.apply")}</button>
          </div>
        </div>
      </div>
    </div>
  );
}

function Keyword({ children, muted }: { children: string; muted?: boolean }) {
  return (
    <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: ".04em", textTransform: "uppercase", color: muted ? "var(--ms-text-disabled)" : "var(--ms-text-secondary)", flexShrink: 0 }}>
      {children}
    </span>
  );
}

/** The compared value of a condition: a true/false select for a boolean, a select of the declared values,
 * or free input (with the unit as a suffix for a number). A stored value outside the choices stays visible. */
function ConditionValue({ value, onChange, input }: { value: string; onChange: (v: string) => void; input: ValueInput }) {
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
        title={t("dynamic.value.hint")}
        style={{ width: 108, textAlign: "center", fontFamily: "ui-monospace, monospace" }}
      />
      {input.unit && <span style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{input.unit}</span>}
    </span>
  );
}

/** The rule's "then" value — a plain flat select for a fixed choice set, or the same ColorField
 * popover (native color wheel + hex + shared presets) every other color field in the app uses, instead
 * of a bespoke rounded/tinted pill. */
function ResultInput({ value, onChange, kind, iconColor, allowEmpty }: { value: string; onChange: (v: string) => void; kind: ResultKind; iconColor?: string; allowEmpty?: boolean }) {
  const { t } = useT();
  if (kind === "text") {
    return (
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={allowEmpty ? t("dynamic.noChange") : t("fields.text.placeholder")}
        style={{ width: "100%" }}
      />
    );
  }
  if (kind === "icon") {
    // An empty case result means "no icon"; an empty else means "leave the widget's own icon" (allowEmpty).
    return <IconPicker value={value || undefined} color={iconColor} onChange={(icon) => onChange(icon ?? "")} />;
  }
  if (typeof kind === "object") {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} style={{ width: "100%" }}>
        {allowEmpty && <option value="">{t("dynamic.noChange")}</option>}
        {kind.select.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    );
  }
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
      <div style={{ flex: 1, minWidth: 0 }}>
        <ColorField value={value} onChange={onChange} />
      </div>
      {allowEmpty && value && (
        <button type="button" className="ghost" title={t("dynamic.noChange")} onClick={() => onChange("")} style={{ display: "flex", padding: 4 }}>
          <X size={12} />
        </button>
      )}
    </div>
  );
}

const chipStyle: React.CSSProperties = {
  display: "inline-flex", alignItems: "center", gap: 5, background: "var(--ms-bg-inset)",
  border: "1px solid var(--ms-border)", borderRadius: 4, padding: "4px 8px",
  fontSize: 12, fontFamily: "ui-monospace, monospace", color: "var(--ms-text-primary)",
};

function withMutation<T>(obj: T, fn: (draft: T) => void): T {
  const draft = structuredClone(obj);
  fn(draft);
  return draft;
}
