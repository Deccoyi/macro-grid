import { useCallback, useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Play, Trash2 } from "lucide-react";
import type { ConditionNode } from "@macro/renderer";
import { api } from "../api/client";
import type { ActionInfo, AutomationRuleDef, AutomationRuleStatus, AutomationResponse, AutomationTriggerKind, PairedDeviceInfo, ProfileSummary, VariableInfo } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { ActionList } from "../panels/ActionList";
import { ConditionEditor } from "../panels/dynamic/ConditionEditor";
import { combinatorOf, fromConditionNode, newCondition, toConditionNode } from "../panels/dynamic/conditionEditing";
import { Field, NumberInput, Seg, SelectInput, Switch, TextInput } from "../panels/fields/controls";
import { newRule, ruleProblem, stepNotes, toggleDay, triggerSummary, withChange } from "./automation";
import { DayChip, EmptyState, PageHeader, TimeInput } from "./settingsPrimitives";

type Limits = AutomationResponse["limits"];

const FALLBACK_LIMITS: Limits = { maxRules: 50, maxNameLength: 60, maxSteps: 20, maxCooldownSeconds: 86400, maxComparisons: 20 };

/** The "Automation" page of the Preferences window: rules that run an action list by themselves, when a value turns true, at a time of day or when a device connects.
 * Every change is sent as the whole list, which the server accepts all of it or none of it; a rule that is not finished yet waits on the page. */
export function AutomationPage() {
  const { t } = useT();
  const [rules, setRules] = useState<AutomationRuleDef[] | null>(null);
  const [paused, setPaused] = useState(false);
  const [limits, setLimits] = useState<Limits>(FALLBACK_LIMITS);
  const [status, setStatus] = useState<Record<string, AutomationRuleStatus>>({});
  const [devices, setDevices] = useState<PairedDeviceInfo[]>([]);
  const [actions, setActions] = useState<ActionInfo[]>([]);
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [catalog, setCatalog] = useState<VariableInfo[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<string | null>(null);

  const load = useCallback(() => {
    api.getAutomation().then((r) => { setRules(r.rules); setPaused(r.paused); setLimits(r.limits); setStatus(r.status); }).catch((e) => setError(messageOf(e)));
  }, []);

  useEffect(() => {
    load();
    api.listDevices().then(setDevices).catch(() => {});
    api.listActions().then(setActions).catch(() => {});
    api.listProfiles().then(setProfiles).catch(() => {});
    api.variableCatalog().then(setCatalog).catch(() => {});
  }, [load]);

  const refreshStatus = () => api.getAutomation().then((r) => setStatus(r.status)).catch(() => {});

  /** Shows the change at once; sends it unless a rule is not finished yet. */
  const save = async (nextRules: AutomationRuleDef[], nextPaused: boolean) => {
    setRules(nextRules);
    setPaused(nextPaused);
    if (nextRules.some((r) => ruleProblem(r) !== null)) return;
    setError(null);
    try {
      await api.setAutomation(nextRules, nextPaused);
    } catch (e) {
      setError(messageOf(e));
    }
  };

  const remove = async (rule: AutomationRuleDef) => {
    if (!(await confirmAsync(t("automation.delete.confirm", rule.name), { danger: true }))) return;
    await save((rules ?? []).filter((r) => r.id !== rule.id), paused);
  };

  const runNow = async (rule: AutomationRuleDef) => {
    setError(null);
    try {
      await api.runAutomationRule(rule.id);
      window.setTimeout(() => void refreshStatus(), 600);
    } catch (e) {
      setError(messageOf(e));
    }
  };

  const add = (kind: AutomationTriggerKind) => {
    if (!rules) return;
    const rule = newRule(kind, rules, t("automation.newName", String(rules.length + 1)));
    setOpen(rule.id);
    void save([...rules, rule], paused);
  };

  const full = (rules?.length ?? 0) >= limits.maxRules;

  return (
    <>
      <PageHeader title={t("preferences.category.automation")} lead={t("automation.hint")} alert={error} />

      <div className="st-auto-bar">
        <label className="st-pause">
          <Switch checked={paused} disabled={!rules} onChange={(v) => rules && void save(rules, v)} label={t("automation.pause")} />
          {t("automation.pause")}
        </label>
        <span className={full ? "st-count full" : "st-count"}>{t("automation.count", String(rules?.length ?? 0), String(limits.maxRules))}</span>
      </div>

      {rules?.length === 0 && <EmptyState title={t("automation.empty.title")} hint={t("automation.empty.hint")} />}
      {rules?.map((rule) => {
        const st = status[rule.id];
        const tone = st?.running ? "" : st?.lastResult === "failed" ? " danger" : st?.lastResult === "refused" ? " warning" : "";
        return (
          <div key={rule.id} className="st-rule">
            <div className="st-rule-head">
              <button type="button" className="st-ib sm" aria-expanded={open === rule.id} aria-label={t("automation.edit")} title={t("automation.edit")} onClick={() => setOpen(open === rule.id ? null : rule.id)}>
                {open === rule.id ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
              </button>
              <Switch checked={rule.enabled} onChange={(enabled) => void save(withChange(rules, rule.id, { enabled }), paused)} label={t("automation.enabled")} />
              <div style={{ minWidth: 0 }}>
                <div className="st-rule-name">{rule.name}</div>
                <div className={`st-rule-status${tone}`}>
                  {st?.running && <span className="dot" aria-hidden="true" />}
                  {triggerSummary(rule, devices, t)} · {lastText(st, t)}
                </div>
              </div>
              <button type="button" className="st-ib" aria-label={t("automation.run")} title={t("automation.run")} disabled={ruleProblem(rule) !== null} onClick={() => void runNow(rule)}><Play size={14} /></button>
              <button type="button" className="st-ib danger" aria-label={t("automation.delete")} title={t("automation.delete")} onClick={() => void remove(rule)}><Trash2 size={14} /></button>
            </div>
            {open === rule.id && (
              <RuleEditor
                rule={rule} limits={limits} devices={devices} actions={actions} profiles={profiles} catalog={catalog}
                onChange={(change) => void save(withChange(rules, rule.id, change), paused)}
              />
            )}
          </div>
        );
      })}

      <div className="st-add-rule">
        <span>{t("automation.add")}</span>
        <button type="button" className="st-btn" disabled={full || !rules} onClick={() => add("variable")}>{t("automation.trigger.variable")}</button>
        <button type="button" className="st-btn" disabled={full || !rules} onClick={() => add("time")}>{t("automation.trigger.time")}</button>
        <button type="button" className="st-btn" disabled={full || !rules} onClick={() => add("deviceConnect")}>{t("automation.trigger.deviceConnect")}</button>
      </div>
      {full && <div className="st-hint" style={{ marginTop: 8 }}>{t("automation.full", String(limits.maxRules))}</div>}
    </>
  );
}

interface RuleEditorProps {
  rule: AutomationRuleDef;
  limits: Limits;
  devices: PairedDeviceInfo[];
  actions: ActionInfo[];
  profiles: ProfileSummary[];
  catalog: VariableInfo[];
  onChange: (change: Partial<AutomationRuleDef>) => void;
}

const DAYS = [1, 2, 3, 4, 5, 6, 0];

function RuleEditor({ rule, limits, devices, actions, profiles, catalog, onChange }: RuleEditorProps) {
  const { t } = useT();
  const trigger = rule.trigger;
  const problem = ruleProblem(rule);
  const notes = stepNotes(rule);
  const setTrigger = (change: Partial<AutomationRuleDef["trigger"]>) => onChange({ trigger: { ...trigger, ...change } });
  const node = trigger.condition as ConditionNode | null | undefined;
  const conditions = node ? fromConditionNode(node) : [newCondition()];
  const userCatalog = catalog.filter((v) => !v.name.toLowerCase().startsWith("self."));

  return (
    <div className="st-rule-body">
      <Field label={t("automation.name")}>
        <TextInput value={rule.name} maxLength={limits.maxNameLength} onChange={(name) => onChange({ name })} />
      </Field>

      <Seg
        value={trigger.kind}
        options={(["variable", "time", "deviceConnect"] as const).map((k) => ({ value: k, label: t(`automation.trigger.${k}` as DictKey) }))}
        onChange={(kind) => {
          if (kind === trigger.kind) return;
          const fresh = newRule(kind, [], rule.name).trigger;
          onChange({ trigger: fresh });
        }}
      />

      {trigger.kind === "variable" && (conditions ? (
        <ConditionEditor
          value={{ combinator: node ? combinatorOf(node) : "and", conditions }}
          variableCatalog={userCatalog}
          onChange={(v) => setTrigger({ condition: toConditionNode({ ...v, result: "" }) })}
        />
      ) : (
        <div className="st-hint">{t("action.logic.unsupported")}</div>
      ))}
      {trigger.kind === "variable" && <div className="st-hint">{t("automation.variable.hint")}</div>}

      {trigger.kind === "time" && (
        <>
          <Field label={t("automation.time")}>
            <TimeInput value={trigger.time ?? ""} onChange={(time) => setTrigger({ time })} />
          </Field>
          <div className="st-days" role="group" aria-label={t("automation.days")}>
            {DAYS.map((d) => (
              <DayChip key={d} on={trigger.days.includes(d)} label={t(`automation.day.${d}` as DictKey)} onToggle={() => setTrigger({ days: toggleDay(trigger.days, d) })} />
            ))}
          </div>
          <div className="st-hint">{t("automation.time.hint")}</div>
        </>
      )}

      {trigger.kind === "deviceConnect" && (
        <Field label={t("automation.device")}>
          <SelectInput value={trigger.deviceId ?? ""} onChange={(v) => setTrigger({ deviceId: v === "" ? null : v })}>
            <option value="">{t("automation.device.any")}</option>
            {devices.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </SelectInput>
        </Field>
      )}

      <Field label={t("automation.cooldown")}>
        <div style={{ width: 140 }}>
          <NumberInput
            value={rule.cooldownSeconds} min={0} max={limits.maxCooldownSeconds} unit={t("automation.cooldown.unit")}
            onChange={(v) => onChange({ cooldownSeconds: Math.min(limits.maxCooldownSeconds, Math.max(0, Math.floor(v || 0))) })}
          />
        </div>
      </Field>

      <ActionList
        bindings={rule.actions}
        actions={actions}
        pages={[]}
        profiles={profiles}
        variableCatalog={catalog}
        onChange={(next) => onChange({ actions: next })}
      />
      <div className="st-hint">{t("automation.steps.limit", String(limits.maxSteps))}</div>
      {notes.map((n) => <div key={n} className="st-note">{t(`automation.note.${n}` as DictKey)}</div>)}
      {problem && <div role="status" className="st-hint error">{t(`automation.problem.${problem}` as DictKey)}</div>}
    </div>
  );
}

function lastText(status: AutomationRuleStatus | undefined, t: (key: DictKey, ...args: string[]) => string): string {
  if (!status || status.lastResult === "none" || !status.lastStart) return t("automation.last.none");
  const time = new Date(status.lastStart).toLocaleTimeString();
  if (status.running) return t("automation.last.running");
  if (status.lastResult === "ok") return t("automation.last.ok", time);
  if (status.lastResult === "refused") return t("automation.last.refused", time);
  return t("automation.last.failed", time, status.lastMessage ?? "");
}

function messageOf(e: unknown): string {
  return e instanceof Error ? e.message : String(e);
}
