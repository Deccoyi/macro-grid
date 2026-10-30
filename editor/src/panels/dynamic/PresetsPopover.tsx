import { useEffect, useMemo, useRef, useState } from "react";
import { CircleOff, Gauge, Layers, Search, ToggleRight, Variable } from "lucide-react";
import { DzTitleBar } from "./DzTitleBar";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import type { DictKey } from "../../i18n/tr";
import { VariablePicker } from "../VariablePicker";
import type { ResultKind } from "./DynamizeModal";
import { buildTemplate, templateKinds, variablesFor, type TemplateKind, type TemplateWords } from "./quickTemplates";

const NAME_KEYS: Record<TemplateKind, DictKey> = { onOff: "dynamic.quick.onOff", thresholds: "dynamic.quick.thresholds", unavailable: "dynamic.quick.unavailable" };
const DESCRIPTION_KEYS: Record<TemplateKind, DictKey> = { onOff: "dynamic.preset.onOff.description", thresholds: "dynamic.preset.thresholds.description", unavailable: "dynamic.preset.unavailable.description" };
const ICONS: Record<TemplateKind, typeof Gauge> = { onOff: ToggleRight, thresholds: Gauge, unavailable: CircleOff };
const GROUPS: { id: "boolean" | "number" | "any"; key: DictKey; kinds: TemplateKind[] }[] = [
  { id: "boolean", key: "dynamic.preset.group.boolean", kinds: ["onOff"] },
  { id: "number", key: "dynamic.preset.group.number", kinds: ["thresholds"] },
  { id: "any", key: "dynamic.preset.group.any", kinds: ["unavailable"] },
];

interface PresetsPopoverProps {
  variableCatalog: VariableInfo[];
  resultKind: ResultKind;
  words: TemplateWords;
  onAdd: (kind: TemplateKind, variable: VariableInfo) => void;
  onClose: () => void;
}

/** The full preset list of the Dynamize window: a fixed-size, non-modal popover with a searchable, grouped list and a read-only preview of the rules a preset adds. */
export function PresetsPopover({ variableCatalog, resultKind, words, onAdd, onClose }: PresetsPopoverProps) {
  const { t, tn } = useT();
  const searchRef = useRef<HTMLInputElement>(null);
  const available = useMemo(() => templateKinds(resultKind), [resultKind]);
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState<TemplateKind>(() => available.find((k) => variablesFor(k, variableCatalog).length > 0) ?? available[0]!);
  const [pickedName, setPickedName] = useState<string | undefined>();

  useEffect(() => {
    searchRef.current?.focus();
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") { e.stopPropagation(); onClose(); } };
    document.addEventListener("keydown", onKey, true);
    return () => document.removeEventListener("keydown", onKey, true);
  }, [onClose]);

  const choices = variablesFor(kind, variableCatalog);
  const variable = choices.find((v) => v.name === pickedName) ?? choices[0];
  const rules = variable ? buildTemplate(kind, variable, resultKind, words) : [];
  const needle = query.trim().toLowerCase();
  const Icon = ICONS[kind];

  const symbol = (op: string) => (op === "unavailable" || op === "available" ? t(`dynamic.operator.${op}` as DictKey) : op);

  return (
    <>
      <div className="dz-presets-backdrop" onClick={(e) => { e.stopPropagation(); onClose(); }} />
      <div className="dz-presets" role="dialog" aria-label={t("dynamic.preset.title")} onClick={(e) => e.stopPropagation()}>
        <DzTitleBar icon={<Layers size={14} color="var(--ms-accent-hover)" />} title={t("dynamic.preset.title")} subtitle={t("dynamic.preset.subtitle")} onClose={onClose} />
        <div className="dz-presets-body">
          <div className="dz-presets-list">
            <div className="dz-presets-search">
              <Search size={14} />
              <input ref={searchRef} type="text" value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("dynamic.preset.search")} aria-label={t("dynamic.preset.search")} />
            </div>
            <div className="dz-presets-scroll">
              {GROUPS.map((group) => {
                const rows = group.kinds.filter((k) => available.includes(k) && t(NAME_KEYS[k]).toLowerCase().includes(needle));
                if (rows.length === 0) return null;
                return (
                  <div key={group.id}>
                    <div className="section-label dz-presets-group">{t(group.key)}</div>
                    {rows.map((k) => {
                      const RowIcon = ICONS[k];
                      const none = variablesFor(k, variableCatalog).length === 0;
                      return (
                        <button
                          key={k}
                          type="button"
                          className={k === kind ? "dz-presets-row on" : "dz-presets-row"}
                          disabled={none}
                          title={none ? t("dynamic.quick.noVariable") : undefined}
                          aria-pressed={k === kind}
                          onClick={() => { setKind(k); setPickedName(undefined); }}
                        >
                          <RowIcon size={14} />
                          <span className="pf-ellipsis">{t(NAME_KEYS[k])}</span>
                          <span className="count">{tn("dynamic.preset.rules", buildTemplate(k, { name: "x" } as VariableInfo, resultKind, words).length)}</span>
                        </button>
                      );
                    })}
                  </div>
                );
              })}
            </div>
          </div>

          <div className="dz-presets-detail">
            <div className="dz-presets-name"><Icon size={14} /> {t(NAME_KEYS[kind])}</div>
            <div className="dz-presets-desc">{t(DESCRIPTION_KEYS[kind])}</div>

            <div className="section-label">{t("dynamic.preset.variable")}</div>
            {variable ? (
              <VariablePicker
                catalog={choices}
                mode="bare"
                onInsert={setPickedName}
                renderTrigger={(open) => (
                  <button type="button" className="dz-chip mono dz-presets-var" onClick={open}>
                    <Variable size={11} />
                    <span className="pf-ellipsis">{variable.name}</span>
                  </button>
                )}
              />
            ) : (
              <div className="dz-presets-desc">{t("dynamic.quick.noVariable")}</div>
            )}

            <div className="section-label">{t("dynamic.preset.adds")}</div>
            <div className="dz-presets-preview">
              {rules.map((r, i) => {
                const cond = r.conditions[0]!;
                const swatch = resultKind === "color" ? r.result : undefined;
                return (
                  <div key={i} className="dz-presets-line">
                    <span className="idx">{i + 1}</span>
                    <span className="cond pf-ellipsis">{`${cond.variable} ${symbol(cond.operator)}${cond.value ? ` ${cond.value}` : ""}`}</span>
                    {swatch && <span className="swatch" style={{ background: swatch }} />}
                    <span className="result pf-ellipsis">{r.result || t("dynamic.noChange")}</span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
        <div className="dz-footer">
          <div className="hint">{t("dynamic.preset.note")}</div>
          <div className="spacer" />
          <button type="button" className="ghost" onClick={onClose}>{t("dynamic.cancel")}</button>
          <button type="button" className="primary" disabled={!variable} onClick={() => { if (variable) { onAdd(kind, variable); onClose(); } }}>
            {tn("dynamic.preset.add", rules.length)}
          </button>
        </div>
      </div>
    </>
  );
}
