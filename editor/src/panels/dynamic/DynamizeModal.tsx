import { useState } from "react";
import { createPortal } from "react-dom";
import { ArrowRight, CircleOff, Gauge, GripVertical, Layers, Plus, ToggleRight, Variable, X } from "lucide-react";
import { DzTitleBar } from "./DzTitleBar";
import type { DynamicBinding } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { ColorField, SelectInput, TextInput } from "../fields/controls";
import { IconPicker } from "../IconPicker";
import { useT } from "../../i18n/I18nContext";
import type { DictKey } from "../../i18n/tr";
import { VariablePicker } from "../VariablePicker";
import { PresetsPopover } from "./PresetsPopover";
import { ConditionEditor } from "./ConditionEditor";
import { combinatorOf, fromConditionNode, isValueless, newCase, newCondition, toConditionNode, type EditCase } from "./conditionEditing";
import { useBackdropClose } from "../../components/useBackdropClose";
import { applyTemplate, buildTemplate, templateKinds, variablesFor, type TemplateKind, type TemplateWords } from "./quickTemplates";

const QUICK_KEYS: Record<TemplateKind, DictKey> = { onOff: "dynamic.quick.onOff", thresholds: "dynamic.quick.thresholds", unavailable: "dynamic.quick.unavailable" };
const QUICK_ICONS: Record<TemplateKind, typeof Gauge> = { onOff: ToggleRight, thresholds: Gauge, unavailable: CircleOff };

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

export function DynamizeModal({ propertyLabel, binding, variableCatalog, resultKind = "color", iconColor, onSave, onClose }: DynamizeModalProps) {
  const { t } = useT();
  const backdrop = useBackdropClose(onClose);
  const defaultResult = typeof resultKind === "object" ? (resultKind.select[0]?.value ?? "") : resultKind === "color" ? "#c0392b" : "";
  const wideResult = resultKind === "text" || resultKind === "icon";
  const resultClass = wideResult ? "dz-result wide" : resultKind === "color" ? "dz-result color" : "dz-result";
  const [unsupported] = useState(() => binding !== undefined && binding.cases.some((c) => fromConditionNode(c.condition) === null));
  const [cases, setCases] = useState<EditCase[]>(() =>
    binding && !unsupported
      ? binding.cases.map((c) => ({ combinator: combinatorOf(c.condition), conditions: fromConditionNode(c.condition) ?? [newCondition()], result: c.result }))
      : [newCase(defaultResult)],
  );
  const [defaultValue, setDefaultValue] = useState(binding?.default ?? "");
  const [presetsOpen, setPresetsOpen] = useState(false);
  const [dragFrom, setDragFrom] = useState<number | null>(null);
  const [dragOver, setDragOver] = useState<number | null>(null);

  const moveCase = (from: number, to: number) => {
    if (from === to) return;
    setCases((prev) => {
      const next = [...prev];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved!);
      return next;
    });
  };
  const endDrag = () => { setDragFrom(null); setDragOver(null); };

  const updateCase = (index: number, fn: (c: EditCase) => void) =>
    setCases((prev) => prev.map((c, i) => (i === index ? withMutation(c, fn) : c)));

  const save = () => {
    const valid = cases.filter((c) => c.conditions.every((cond) => cond.variable && (cond.value || isValueless(cond.operator))));
    if (valid.length === 0) { onSave(null); onClose(); return; }
    onSave({
      cases: valid.map((c) => ({ condition: toConditionNode(c), result: c.result })),
      default: defaultValue || undefined,
    });
    onClose();
  };

  const words: TemplateWords = {
    on: t("dynamic.quick.word.on"),
    off: t("dynamic.quick.word.off"),
    low: t("dynamic.quick.word.low"),
    medium: t("dynamic.quick.word.medium"),
    high: t("dynamic.quick.word.high"),
    unavailable: t("dynamic.quick.word.unavailable"),
  };
  const addTemplate = (kind: TemplateKind, variable: VariableInfo) =>
    setCases((prev) => applyTemplate(prev, buildTemplate(kind, variable, resultKind, words), kind));

  const remove = () => { onSave(null); onClose(); };

  return createPortal(
    // Opened from a widget field inside PropertiesToolWindow — inside the docking workspace's isolated
    // stacking context (see PickerShell's comment on this), so this has to outrank dockview's own splitter
    // lines (z-index 99) there, not just the App-root dialogs it used to only need to beat.
    <div className="dz-backdrop" {...backdrop}>
      <div
        className="pf-root dz-window"
        onClick={(e) => e.stopPropagation()}
        style={{ width: 680, maxHeight: "82vh", background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border-strong)", display: "flex", flexDirection: "column" }}
      >
        {/* Title bar — a window title bar, not a web modal's rounded card top: square corners, no radius
           anywhere in this shell (see docs/ui/ui-guidelines.md: "like a window", never like a web modal). */}
        <DzTitleBar icon={<Variable size={14} color="var(--ms-accent-hover)" />} title={t("dynamic.title")} subtitle={propertyLabel} mono onClose={onClose} />

        {/* Body */}
        <div style={{ overflowY: "auto" }}>
          {unsupported && (
            <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", padding: 8, margin: "var(--pf-pad)" }}>
              {t("dynamic.unsupported")}
            </div>
          )}

          {!unsupported && (
            <div className="dz-quick">
              <span className="section-label">{t("dynamic.quick.title")}</span>
              {templateKinds(resultKind).map((kind) => {
                const choices = variablesFor(kind, variableCatalog);
                const Icon = QUICK_ICONS[kind];
                const label = (
                  <>
                    <Icon size={12} /> {t(QUICK_KEYS[kind])}
                  </>
                );
                if (choices.length === 0) {
                  return (
                    <button key={kind} type="button" className="dz-chip" disabled title={t("dynamic.quick.noVariable")}>{label}</button>
                  );
                }
                return (
                  <VariablePicker
                    key={kind}
                    catalog={choices}
                    mode="bare"
                    onInsert={(name) => { const picked = choices.find((v) => v.name === name); if (picked) addTemplate(kind, picked); }}
                    renderTrigger={(open) => <button type="button" className="dz-chip" onClick={open}>{label}</button>}
                  />
                );
              })}
              <button type="button" className="dz-chip dashed" onClick={() => setPresetsOpen(true)}>
                <Layers size={12} /> {t("dynamic.preset.all")}
              </button>
              <span className="dz-hint">{t("dynamic.quick.firstMatch")}</span>
            </div>
          )}

          {cases.map((c, i) => (
            <div
              key={i}
              className={dragFrom === null ? "dz-rule" : `dz-rule${dragFrom === i ? " dragging" : ""}${dragOver === i && dragFrom !== i ? (dragFrom < i ? " drop-below" : " drop-above") : ""}`}
              onDragOver={dragFrom === null ? undefined : (e) => { e.preventDefault(); e.dataTransfer.dropEffect = "move"; setDragOver(i); }}
              onDrop={dragFrom === null ? undefined : (e) => { e.preventDefault(); moveCase(dragFrom, i); endDrag(); }}
            >
              <div className="dz-kw">
                {cases.length > 1 && (
                  <span
                    className="dz-grip"
                    draggable
                    title={t("dynamic.dragRule")}
                    aria-label={t("dynamic.dragRule")}
                    onDragStart={(e) => { e.dataTransfer.effectAllowed = "move"; e.dataTransfer.setData("text/plain", String(i)); setDragFrom(i); }}
                    onDragEnd={endDrag}
                  >
                    <GripVertical size={14} />
                  </span>
                )}
                <Keyword>{i === 0 ? t("dynamic.if") : t("dynamic.elseIf")}</Keyword>
              </div>
              <div className="dz-content">
                <ConditionEditor value={c} variableCatalog={variableCatalog} onChange={(v) => updateCase(i, (cc) => { cc.combinator = v.combinator; cc.conditions = v.conditions; })} />
                <div className="dz-then">
                  <span className="section-label">{t("dynamic.then")}</span>
                  <ArrowRight size={14} className="dz-arrow" />
                  <div className={resultClass}>
                    <ResultInput value={c.result} onChange={(v) => updateCase(i, (cc) => { cc.result = v; })} kind={resultKind} iconColor={iconColor} />
                  </div>
                  <div className="spacer" />
                  {cases.length > 1 && (
                    <button type="button" className="ghost pf-btn small pf-danger" onClick={() => setCases((prev) => prev.filter((_, idx) => idx !== i))} style={{ fontSize: 12 }}>
                      {t("dynamic.removeRule")}
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}

          <div className="dz-addrow">
            <button type="button" className="ghost pf-btn small" onClick={() => setCases((prev) => [...prev, newCase(defaultResult)])}>
              <Plus size={12} /> {t("dynamic.newRule")}
            </button>
          </div>

          <div className="dz-else">
            <Keyword>{t("dynamic.else")}</Keyword>
            <div className="dz-else-row">
              <ArrowRight size={14} className="dz-arrow" />
              <div className={resultClass}>
                <ResultInput value={defaultValue} onChange={setDefaultValue} kind={resultKind} iconColor={iconColor} allowEmpty />
              </div>
              <div className="dz-else-hint">{t("dynamic.elseHint")}</div>
            </div>
          </div>
        </div>

        {presetsOpen && (
          <PresetsPopover
            variableCatalog={variableCatalog}
            resultKind={resultKind}
            words={words}
            onAdd={addTemplate}
            onClose={() => setPresetsOpen(false)}
          />
        )}

        {/* Footer */}
        <div className="dz-footer">
          <button type="button" className="ghost pf-danger" onClick={remove}>{t("dynamic.remove")}</button>
          <div className="spacer" />
          <button type="button" className="ghost" onClick={onClose}>{t("dynamic.cancel")}</button>
          <button type="button" className="primary" onClick={save}>{t("dynamic.apply")}</button>
        </div>
      </div>
    </div>,
    // Portalled to <body>: rendered inline inside a field label it inherited `.pf-actions button { color: secondary }`,
    // which turned the Apply label grey on the accent fill (same trap as PickerShell).
    document.body,
  );
}

function Keyword({ children }: { children: string }) {
  return <span className="dz-keyword">{children}</span>;
}

/** The rule's "then" value — a plain flat select for a fixed choice set, or the same ColorField
 * popover (native color wheel + hex + shared presets) every other color field in the app uses, instead
 * of a bespoke rounded/tinted pill. */
function ResultInput({ value, onChange, kind, iconColor, allowEmpty }: { value: string; onChange: (v: string) => void; kind: ResultKind; iconColor?: string; allowEmpty?: boolean }) {
  const { t } = useT();
  if (kind === "text") {
    return (
      <TextInput label={t("dynamic.then")} value={value} onChange={onChange} placeholder={allowEmpty ? t("dynamic.noChange") : t("fields.text.placeholder")} />
    );
  }
  if (kind === "icon") {
    // An empty case result means "no icon"; an empty else means "leave the widget's own icon" (allowEmpty).
    return <IconPicker value={value || undefined} color={iconColor} onChange={(icon) => onChange(icon ?? "")} />;
  }
  if (typeof kind === "object") {
    return (
      <SelectInput label={t("dynamic.then")} value={value} onChange={onChange}>
        {allowEmpty && <option value="">{t("dynamic.noChange")}</option>}
        {kind.select.map((o) => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </SelectInput>
    );
  }
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
      <div style={{ flex: 1, minWidth: 0 }}>
        <ColorField value={value} onChange={onChange} />
      </div>
      {allowEmpty && value && (
        <button type="button" className="ghost pf-icon-btn small" title={t("dynamic.noChange")} aria-label={t("dynamic.noChange")} onClick={() => onChange("")}>
          <X size={12} />
        </button>
      )}
    </div>
  );
}

function withMutation<T>(obj: T, fn: (draft: T) => void): T {
  const draft = structuredClone(obj);
  fn(draft);
  return draft;
}
