import type { ActionBinding, ConditionNode } from "@macro/renderer";
import type { VariableInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { ConditionEditor } from "./dynamic/ConditionEditor";
import { combinatorOf, fromConditionNode, newCondition, toConditionNode } from "./dynamic/conditionEditing";

export type IfWhen = "condition" | "previousFailed" | "previousOk";

const WHEN_KEYS: Record<IfWhen, DictKey> = {
  condition: "action.logic.when.condition",
  previousFailed: "action.logic.when.previousFailed",
  previousOk: "action.logic.when.previousOk",
};

export function whenOf(binding: ActionBinding): IfWhen {
  const when = binding.settings.when;
  return when === "previousFailed" || when === "previousOk" ? when : "condition";
}

interface IfStepEditorProps {
  binding: ActionBinding;
  variableCatalog: VariableInfo[];
  onChange: (settings: Record<string, unknown>) => void;
}

/** The body of an If row: what the step tests (a condition, or how the step before ended) and, for a condition, the same editor the dynamic rules use. */
export function IfStepEditor({ binding, variableCatalog, onChange }: IfStepEditorProps) {
  const { t } = useT();
  const when = whenOf(binding);
  const node = binding.settings.condition as ConditionNode | undefined;
  const conditions = node ? fromConditionNode(node) : [newCondition()];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8, flex: 1, minWidth: 0 }}>
      <select value={when} onChange={(e) => onChange({ ...binding.settings, when: e.target.value })} aria-label={t("action.logic.if")}>
        {(Object.keys(WHEN_KEYS) as IfWhen[]).map((w) => <option key={w} value={w}>{t(WHEN_KEYS[w])}</option>)}
      </select>
      {when === "condition" && (conditions ? (
        <ConditionEditor
          value={{ combinator: node ? combinatorOf(node) : "and", conditions }}
          variableCatalog={variableCatalog}
          onChange={(v) => onChange({ ...binding.settings, condition: toConditionNode({ ...v, result: "" }) })}
        />
      ) : (
        <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", padding: 8 }}>{t("action.logic.unsupported")}</div>
      ))}
    </div>
  );
}
