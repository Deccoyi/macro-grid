import { useState } from "react";
import { ArrowRight, Plus, Trash2, Variable, X } from "lucide-react";
import type { DynamicBinding } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { VariablePicker } from "../VariablePicker";
import { combinatorOf, fromConditionNode, newCase, newCondition, OPERATOR_LABELS, toConditionNode, type EditCase, type EditCondition } from "./conditionEditing";

/** What each rule's "then" value is: a free color, or a fixed set of choices (e.g. animation names). */
export type ResultKind = "color" | { select: { value: string; label: string }[] };

export interface DynamizeModalProps {
  propertyLabel: string;
  binding: DynamicBinding | undefined;
  variableCatalog: VariableInfo[];
  resultKind?: ResultKind;
  onSave: (binding: DynamicBinding | null) => void;
  onClose: () => void;
}

const COMBINATOR_LABELS: Record<EditCase["combinator"], string> = { and: "VE", or: "VEYA", xor: "XOR" };

export function DynamizeModal({ propertyLabel, binding, variableCatalog, resultKind = "color", onSave, onClose }: DynamizeModalProps) {
  const defaultResult = typeof resultKind === "object" ? (resultKind.select[0]?.value ?? "") : "#c0392b";
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
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 60, display: "flex", alignItems: "center", justifyContent: "center" }} onClick={onClose}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 620, maxHeight: "82vh", background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border)", borderRadius: 10, boxShadow: "0 24px 60px rgba(0,0,0,.5)", display: "flex", flexDirection: "column" }}
      >
        {/* Header */}
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "16px 20px", borderBottom: "1px solid var(--ms-border)" }}>
          <div style={{ width: 30, height: 30, borderRadius: 7, background: "var(--ms-accent-bg-muted)", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
            <Variable size={16} color="var(--ms-accent-hover)" />
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
            <div style={{ fontSize: 14, fontWeight: 600 }}>Mantık kur</div>
            <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", fontFamily: "ui-monospace, monospace" }}>{propertyLabel}</div>
          </div>
          <div style={{ flex: 1 }} />
          <button type="button" className="ghost" onClick={onClose} aria-label="Kapat" style={{ display: "flex", padding: 5 }}>
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: "16px 20px", display: "flex", flexDirection: "column", gap: 10, overflowY: "auto" }}>
          {unsupported && (
            <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", padding: 8, borderRadius: 6 }}>
              Bu kural düzenleyicinin gösterebileceğinden daha karmaşık (muhtemelen elle/JSON ile oluşturulmuş). Kaydedersen aşağıdaki basit haliyle değiştirilir.
            </div>
          )}

          {cases.map((c, i) => (
            <div key={i} style={{ borderRadius: 10, border: `1px solid ${i === 0 ? "var(--ms-border)" : "var(--ms-border)"}`, background: "var(--ms-bg-canvas)", padding: "14px 16px", display: "flex", flexDirection: "column", gap: 10 }}>
              <Keyword>{i === 0 ? "Eğer" : "Yoksa eğer"}</Keyword>

              {c.conditions.map((cond, ci) => (
                <div key={ci} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {ci > 0 && (
                    <div style={{ display: "flex", gap: 0, alignSelf: "flex-start" }}>
                      {(["and", "or", "xor"] as const).map((op) => (
                        <button
                          key={op}
                          type="button"
                          onClick={() => updateCase(i, (cc) => { cc.combinator = op; })}
                          style={pillComboStyle(c.combinator === op)}
                        >
                          {COMBINATOR_LABELS[op]}
                        </button>
                      ))}
                    </div>
                  )}
                  <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <button
                      type="button"
                      onClick={() => updateCase(i, (cc) => { cc.conditions[ci]!.negate = !cc.conditions[ci]!.negate; })}
                      style={pillNotStyle(cond.negate)}
                      title="Bu koşulu tersine çevir"
                    >
                      değilse
                    </button>

                    <VariablePicker
                      catalog={variableCatalog}
                      mode="bare"
                      onInsert={(name) => updateCase(i, (cc) => { cc.conditions[ci]!.variable = name; })}
                      renderTrigger={(open) => (
                        <button type="button" onClick={open} style={pillVarStyle}>
                          <Variable size={11} />
                          {cond.variable || "değişken seç"}
                        </button>
                      )}
                    />

                    <select
                      value={cond.operator}
                      onChange={(e) => updateCase(i, (cc) => { cc.conditions[ci]!.operator = e.target.value as EditCondition["operator"]; })}
                      style={pillSelectStyle}
                    >
                      {Object.entries(OPERATOR_LABELS).map(([op, label]) => (
                        <option key={op} value={op}>{label}</option>
                      ))}
                    </select>

                    <input
                      type="text"
                      value={cond.value}
                      onChange={(e) => updateCase(i, (cc) => { cc.conditions[ci]!.value = e.target.value; })}
                      placeholder="50"
                      style={pillInputStyle}
                    />
                    {cond.operator === "between" && (
                      <>
                        <span style={{ color: "var(--ms-text-disabled)", fontSize: 12 }}>–</span>
                        <input
                          type="text"
                          value={cond.value2}
                          onChange={(e) => updateCase(i, (cc) => { cc.conditions[ci]!.value2 = e.target.value; })}
                          placeholder="80"
                          style={pillInputStyle}
                        />
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
              ))}

              <button
                type="button"
                className="ghost"
                onClick={() => updateCase(i, (cc) => { cc.conditions.push(newCondition()); })}
                style={{ alignSelf: "flex-start", display: "flex", alignItems: "center", gap: 5, fontSize: 12 }}
              >
                <Plus size={11} /> Koşul ekle
              </button>

              <div style={{ height: 1, background: "var(--ms-border)", margin: "2px 0" }} />

              <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                <Keyword muted>İse</Keyword>
                <ArrowRight size={13} color="var(--ms-border-strong)" />
                <ResultInput value={c.result} onChange={(v) => updateCase(i, (cc) => { cc.result = v; })} kind={resultKind} />
                <div style={{ flex: 1 }} />
                {cases.length > 1 && (
                  <button type="button" className="ghost" onClick={() => setCases((prev) => prev.filter((_, idx) => idx !== i))} style={{ color: "var(--ms-danger)", fontSize: 12 }}>
                    Kuralı sil
                  </button>
                )}
              </div>
            </div>
          ))}

          <button
            type="button"
            onClick={() => setCases((prev) => [...prev, newCase(defaultResult)])}
            style={{ border: "1px dashed var(--ms-border-strong)", background: "transparent", color: "var(--ms-text-secondary)", padding: 10, display: "flex", alignItems: "center", justifyContent: "center", gap: 6, borderRadius: 10, cursor: "pointer" }}
          >
            <Plus size={14} /> Yeni kural
          </button>

          <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "11px 16px", borderRadius: 10, border: "1px dashed var(--ms-border)" }}>
            <Keyword muted>Yoksa</Keyword>
            <div style={{ flex: 1, fontSize: 12, color: "var(--ms-text-disabled)" }}>değişmesin, ya da bir varsayılan değer seç</div>
            <ResultInput value={defaultValue} onChange={setDefaultValue} kind={resultKind} allowEmpty />
          </div>
        </div>

        {/* Footer */}
        <div style={{ display: "flex", alignItems: "center", padding: "14px 20px", borderTop: "1px solid var(--ms-border)" }}>
          <button type="button" className="ghost" onClick={remove} style={{ color: "var(--ms-danger)" }}>Dinamizasyonu kaldır</button>
          <div style={{ flex: 1 }} />
          <div style={{ display: "flex", gap: 8 }}>
            <button type="button" className="ghost" onClick={onClose}>Vazgeç</button>
            <button type="button" className="primary" onClick={save}>Uygula</button>
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

function ResultInput({ value, onChange, kind, allowEmpty }: { value: string; onChange: (v: string) => void; kind: ResultKind; allowEmpty?: boolean }) {
  if (typeof kind === "object") {
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} style={{ ...pillSelectStyle, width: 130 }}>
        {allowEmpty && <option value="">(değişmesin)</option>}
        {kind.select.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>
    );
  }
  const isColor = /^#([0-9a-f]{6})$/i.test(value);
  return (
    <label
      style={{
        display: "inline-flex", alignItems: "center", gap: 7, borderRadius: 999, padding: "5px 12px 5px 9px",
        fontSize: 12.5, fontWeight: 600, fontFamily: "ui-monospace, monospace", cursor: "pointer",
        background: isColor ? hexToRgba(value, 0.14) : "transparent",
        color: isColor ? value : "var(--ms-text-disabled)",
        border: `1px solid ${isColor ? hexToRgba(value, 0.35) : "var(--ms-border-strong)"}`,
        borderStyle: isColor ? "solid" : "dashed",
      }}
    >
      <span style={{ width: 12, height: 12, borderRadius: "50%", background: isColor ? value : "transparent", border: isColor ? "none" : "1px dashed var(--ms-text-disabled)", flexShrink: 0 }} />
      {value || (allowEmpty ? "renk seç" : "#c0392b")}
      <input type="color" value={isColor ? value : "#000000"} onChange={(e) => onChange(e.target.value)} style={{ position: "absolute", width: 1, height: 1, opacity: 0, pointerEvents: "none" }} tabIndex={-1} />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={{ position: "absolute", width: 1, height: 1, opacity: 0 }}
        tabIndex={-1}
        aria-hidden
      />
    </label>
  );
}

const pillBase: React.CSSProperties = {
  display: "inline-flex", alignItems: "center", gap: 5, borderRadius: 999, padding: "5px 11px 5px 9px",
  fontSize: 12.5, fontWeight: 600, border: "1px solid transparent", cursor: "pointer", whiteSpace: "nowrap",
  fontFamily: "inherit", lineHeight: 1.2,
};

const pillVarStyle: React.CSSProperties = {
  ...pillBase,
  background: "rgba(56,131,246,.14)", color: "#7ab0fb", borderColor: "rgba(56,131,246,.3)",
  fontFamily: "ui-monospace, monospace", fontWeight: 500,
};

const pillSelectStyle: React.CSSProperties = {
  borderRadius: 999, padding: "5px 10px", fontSize: 12.5, fontWeight: 600,
  background: "rgba(167,139,250,.14)", color: "#c1adfc", border: "1px solid rgba(167,139,250,.3)",
  width: "auto",
};

const pillInputStyle: React.CSSProperties = {
  background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", color: "var(--ms-text-primary)",
  borderRadius: 999, fontSize: 12.5, fontFamily: "ui-monospace, monospace", fontWeight: 600,
  padding: "5px 12px", width: 46, textAlign: "center",
};

function pillNotStyle(on: boolean): React.CSSProperties {
  return on
    ? { ...pillBase, background: "rgba(192,57,43,.16)", color: "#ef6a5a", borderColor: "rgba(192,57,43,.4)" }
    : { ...pillBase, background: "transparent", color: "var(--ms-text-disabled)", border: "1px dashed var(--ms-border-strong)" };
}

function pillComboStyle(on: boolean): React.CSSProperties {
  return {
    padding: "5px 10px", fontSize: 11, fontWeight: 700, cursor: "pointer", border: "1px solid var(--ms-border)",
    background: on ? "var(--ms-accent-bg-muted)" : "var(--ms-bg-inset)",
    color: on ? "var(--ms-accent-hover)" : "var(--ms-text-secondary)",
    borderColor: on ? "rgba(217,119,6,.4)" : "var(--ms-border)",
    marginLeft: -1,
  };
}

function hexToRgba(hex: string, alpha: number): string {
  const m = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex);
  if (!m) return `rgba(255,255,255,${alpha})`;
  const [r, g, b] = [m[1]!, m[2]!, m[3]!].map((h) => parseInt(h, 16));
  return `rgba(${r},${g},${b},${alpha})`;
}

function withMutation<T>(obj: T, fn: (draft: T) => void): T {
  const draft = structuredClone(obj);
  fn(draft);
  return draft;
}
