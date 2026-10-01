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
import { SectionLabel, Seg } from "../panels/fields/controls";
import { newRule, ruleProblem, stepNotes, toggleDay, triggerSummary, withChange } from "./automation";

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
    <div style={{ display: "flex", flexDirection: "column", gap: 12, maxWidth: 720 }}>
      <SectionLabel>{t("preferences.category.automation")}</SectionLabel>
      <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("automation.hint")}</p>
      {error && <div role="alert" style={{ fontSize: 12, color: "var(--ms-danger)" }}>{error}</div>}

      <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12.5 }}>
          <input type="checkbox" checked={paused} disabled={!rules} onChange={(e) => rules && void save(rules, e.target.checked)} />
          {t("automation.pause")}
        </label>
        <span style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("automation.count", String(rules?.length ?? 0), String(limits.maxRules))}</span>
      </div>

      {rules?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("automation.empty")}</div>}
      {rules?.map((rule) => (
        <div key={rule.id} style={{ borderTop: "1px solid var(--ms-border)", paddingTop: 8, display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <button type="button" className="ghost" aria-expanded={open === rule.id} title={t("automation.edit")} onClick={() => setOpen(open === rule.id ? null : rule.id)} style={{ padding: 4, display: "flex" }}>
              {open === rule.id ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            </button>
            <input type="checkbox" checked={rule.enabled} aria-label={t("automation.enabled")} title={t("automation.enabled")} onChange={(e) => void save(withChange(rules, rule.id, { enabled: e.target.checked }), paused)} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 12.5, fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{rule.name}</div>
              <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {triggerSummary(rule, devices, t)} · {lastText(status[rule.id], t)}
              </div>
            </div>
            <button type="button" className="ghost" title={t("automation.run")} disabled={ruleProblem(rule) !== null} onClick={() => void runNow(rule)} style={{ padding: 5 }}><Play size={13} /></button>
            <button type="button" className="ghost" title={t("automation.delete")} onClick={() => void remove(rule)} style={{ padding: 5, color: "var(--ms-danger)" }}><Trash2 size={13} /></button>
          </div>
          {open === rule.id && (
            <RuleEditor
              rule={rule} limits={limits} devices={devices} actions={actions} profiles={profiles} catalog={catalog}
              onChange={(change) => void save(withChange(rules, rule.id, change), paused)}
            />
          )}
        </div>
      ))}

      <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap", borderTop: "1px solid var(--ms-border)", paddingTop: 10 }}>
        <span style={{ fontSize: 12 }}>{t("automation.add")}</span>
        <button type="button" className="ghost" disabled={full || !rules} onClick={() => add("variable")}>{t("automation.trigger.variable")}</button>
        <button type="button" className="ghost" disabled={full || !rules} onClick={() => add("time")}>{t("automation.trigger.time")}</button>
        <button type="button" className="ghost" disabled={full || !rules} onClick={() => add("deviceConnect")}>{t("automation.trigger.deviceConnect")}</button>
      </div>
      {full && <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("automation.full", String(limits.maxRules))}</div>}
    </div>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 10, paddingLeft: 24 }}>
      <label className="field">
        {t("automation.name")}
        <input type="text" value={rule.name} maxLength={limits.maxNameLength} onChange={(e) => onChange({ name: e.target.value })} />
      </label>

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
        <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{t("action.logic.unsupported")}</div>
      ))}
      {trigger.kind === "variable" && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{t("automation.variable.hint")}</div>}

      {trigger.kind === "time" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <label className="field">
            {t("automation.time")}
            <input type="time" value={trigger.time ?? ""} onChange={(e) => setTrigger({ time: e.target.value })} style={{ width: 120 }} />
          </label>
          <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }} role="group" aria-label={t("automation.days")}>
            {DAYS.map((d) => (
              <button
                key={d} type="button" className={trigger.days.includes(d) ? "ghost on" : "ghost"} aria-pressed={trigger.days.includes(d)}
                style={trigger.days.includes(d) ? { background: "var(--ms-accent-bg-muted)", color: "var(--ms-accent)" } : undefined}
                onClick={() => setTrigger({ days: toggleDay(trigger.days, d) })}
              >
                {t(`automation.day.${d}` as DictKey)}
              </button>
            ))}
          </div>
          <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{t("automation.time.hint")}</div>
        </div>
      )}

      {trigger.kind === "deviceConnect" && (
        <label className="field">
          {t("automation.device")}
          <select value={trigger.deviceId ?? ""} onChange={(e) => setTrigger({ deviceId: e.target.value === "" ? null : e.target.value })}>
            <option value="">{t("automation.device.any")}</option>
            {devices.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </label>
      )}

      <label className="field">
        {t("automation.cooldown")}
        <input
          type="number" min={0} max={limits.maxCooldownSeconds} value={rule.cooldownSeconds} style={{ width: 120 }}
          onChange={(e) => onChange({ cooldownSeconds: Math.min(limits.maxCooldownSeconds, Math.max(0, Math.floor(Number(e.target.value) || 0))) })}
        />
      </label>

      <ActionList
        bindings={rule.actions}
        actions={actions}
        pages={[]}
        profiles={profiles}
        variableCatalog={catalog}
        onChange={(next) => onChange({ actions: next })}
      />
      <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{t("automation.steps.limit", String(limits.maxSteps))}</div>
      {notes.map((n) => <div key={n} style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 4, padding: "6px 8px" }}>{t(`automation.note.${n}` as DictKey)}</div>)}
      {problem && <div role="status" style={{ fontSize: 11.5, color: "var(--ms-danger)" }}>{t(`automation.problem.${problem}` as DictKey)}</div>}
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
