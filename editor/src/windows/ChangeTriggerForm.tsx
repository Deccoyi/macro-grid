import { Variable } from "lucide-react";
import type { AutomationTrigger, VariableInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { VariablePicker } from "../panels/VariablePicker";
import { Field, NumberInput } from "../panels/fields/controls";
import { AUTOMATION_MAX_MIN_INTERVAL } from "./automation";

/** Rules saved before the filters were removed may still carry one; the server keeps honouring it. */
function hasOldFilter(trigger: AutomationTrigger): boolean {
  return (trigger.mode !== null && trigger.mode !== undefined && trigger.mode !== "any")
    || (trigger.from !== null && trigger.from !== undefined)
    || (trigger.to !== null && trigger.to !== undefined)
    || (trigger.threshold !== null && trigger.threshold !== undefined);
}

interface ChangeTriggerFormProps {
  trigger: AutomationTrigger;
  catalog: VariableInfo[];
  onChange: (change: Partial<AutomationTrigger>) => void;
}

/** The form of a "when a value changes" rule: the value to watch and the minimum time between starts. Any change starts it; an If step in the rule can look at the old and new value. */
export function ChangeTriggerForm({ trigger, catalog, onChange }: ChangeTriggerFormProps) {
  const { t } = useT();

  return (
    <>
      <Field label={t("automation.change.variable")}>
        <VariablePicker
          catalog={catalog}
          mode="bare"
          onInsert={(name) => {
            // A different variable starts again, so no old filter stays that the form cannot show.
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

      {hasOldFilter(trigger) && (
        <div className="st-hint">
          {t("automation.change.oldFilter")}{" "}
          <button type="button" className="st-btn" onClick={() => onChange({ mode: "any", from: null, to: null, threshold: null })}>
            {t("automation.change.removeFilter")}
          </button>
        </div>
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
