import { Variable } from "lucide-react";
import type { AutomationTrigger, VariableInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { VariablePicker } from "../panels/VariablePicker";
import { Field, NumberInput, Seg, SelectInput, TextInput } from "../panels/fields/controls";
import { AUTOMATION_MAX_MIN_INTERVAL } from "./automation";

type Choice = "any" | "becomes" | "crosses" | "contains";

/** The editor's four choices map onto the server's three modes: "becomes" is mode "any" with a "to" value. */
function choiceOf(trigger: AutomationTrigger): Choice {
  if (trigger.mode === "crosses") return "crosses";
  if (trigger.mode === "contains") return "contains";
  return trigger.to === null || trigger.to === undefined ? "any" : "becomes";
}

interface ChangeTriggerFormProps {
  trigger: AutomationTrigger;
  catalog: VariableInfo[];
  onChange: (change: Partial<AutomationTrigger>) => void;
}

/** The form of a "when a value changes" rule: the value to watch, what kind of change counts, and the minimum time between starts. */
export function ChangeTriggerForm({ trigger, catalog, onChange }: ChangeTriggerFormProps) {
  const { t } = useT();
  const info = catalog.find((v) => v.name === trigger.variable);
  const type = info?.type ?? "text";
  const choice = choiceOf(trigger);
  const choices: Choice[] = type === "boolean" ? [] : type === "number" ? ["any", "crosses"] : ["any", "becomes", "contains"];

  const pickChoice = (next: Choice) => {
    if (next === choice) return;
    if (next === "any") onChange({ mode: "any", from: null, to: null, threshold: null });
    else if (next === "becomes") onChange({ mode: "any", from: null, to: "", threshold: null });
    else if (next === "contains") onChange({ mode: "contains", from: null, to: "", threshold: null });
    else onChange({ mode: "crosses", from: null, to: null, threshold: trigger.threshold ?? 0 });
  };

  const boolSelect = (label: DictKey, value: string | null | undefined, set: (v: string | null) => void) => (
    <SelectInput label={t(label)} value={value ?? ""} onChange={(v) => set(v === "" ? null : v)}>
      <option value="">{t("automation.change.anyValue")}</option>
      <option value="true">{t("automation.change.true")}</option>
      <option value="false">{t("automation.change.false")}</option>
    </SelectInput>
  );

  return (
    <>
      <Field label={t("automation.change.variable")}>
        <VariablePicker
          catalog={catalog}
          mode="bare"
          onInsert={(name) => {
            // The filters were typed for the old variable: a different one starts again, so no value stays that the new form cannot show.
            if (name === trigger.variable) return;
            onChange({ variable: name, mode: "any", from: null, to: null, threshold: null });
          }}
          renderTrigger={(open) => (
            <button type="button" className="dz-chip mono" onClick={open}>
              <Variable size={11} />
              <span className="pf-ellipsis">{trigger.variable || t("dynamic.pickVariable")}</span>
            </button>
          )}
        />
      </Field>

      {type === "boolean" && (
        <>
          {boolSelect("automation.change.from", trigger.from, (from) => onChange({ from, mode: "any" }))}
          {boolSelect("automation.change.to", trigger.to, (to) => onChange({ to, mode: "any" }))}
        </>
      )}

      {choices.length > 0 && (
        <Seg
          value={choice}
          options={choices.map((c) => ({ value: c, label: t(`automation.change.mode.${c}` as DictKey) }))}
          onChange={pickChoice}
        />
      )}

      {choice === "crosses" && (
        <Field label={t("automation.change.threshold")}>
          <div style={{ width: 160 }}>
            <NumberInput
              value={trigger.threshold ?? 0} unit={info?.unit ?? undefined}
              onChange={(v) => onChange({ threshold: Number.isFinite(v) ? v : 0 })}
            />
          </div>
        </Field>
      )}

      {(choice === "becomes" || choice === "contains") && (
        <Field label={t("automation.change.text")}>
          <TextInput value={trigger.to ?? ""} maxLength={200} onChange={(to) => onChange({ to })} />
        </Field>
      )}

      <Field label={t("automation.change.minInterval")}>
        <div style={{ width: 140 }}>
          <NumberInput
            value={trigger.minIntervalSeconds ?? 0} min={0} max={AUTOMATION_MAX_MIN_INTERVAL} unit={t("automation.cooldown.unit")}
            onChange={(v) => onChange({ minIntervalSeconds: Math.min(AUTOMATION_MAX_MIN_INTERVAL, Math.max(0, Math.floor(v || 0))) })}
          />
        </div>
      </Field>

      <div className="st-hint">{t("automation.change.hint")}</div>
    </>
  );
}
