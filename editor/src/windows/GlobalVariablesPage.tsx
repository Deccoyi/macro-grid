import { useEffect, useState } from "react";
import { Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { UserVariableDef, UserVariablesResponse, UserVariableType } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { SelectInput, Switch, TextInput } from "../panels/fields/controls";
import { USER_VARIABLES_CHANGED } from "../state/userVariablesEvent";
import { newVariable, nameProblem, parseStartValue, startValueText, usageSummary, USER_PREFIX, withChange } from "./globalVariables";
import { EmptyState, InlineText, PageHeader } from "./settingsPrimitives";

type Limits = UserVariablesResponse["limits"];

const FALLBACK_LIMITS: Limits = { maxCount: 200, maxNameLength: 40, maxDescriptionLength: 200, maxTextLength: 1024 };

/** The "Global Variable List" page of the Preferences window: variables the person defines and uses as {user.name} in texts, rules and actions.
 * Every change is sent to the server as the whole list, which accepts all of it or none of it. */
export function GlobalVariablesPage() {
  const { t, tn } = useT();
  const [list, setList] = useState<UserVariableDef[] | null>(null);
  const [limits, setLimits] = useState<Limits>(FALLBACK_LIMITS);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [type, setType] = useState<UserVariableType>("number");

  useEffect(() => {
    api.getUserVariables().then((r) => { setList(r.variables); setLimits(r.limits); }).catch((e) => setError(String(e instanceof Error ? e.message : e)));
  }, []);

  const save = async (next: UserVariableDef[]): Promise<boolean> => {
    setError(null);
    try {
      await api.setUserVariables(next);
      window.dispatchEvent(new Event(USER_VARIABLES_CHANGED));
      setList(next);
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return false;
    }
  };

  const remove = async (v: UserVariableDef) => {
    const uses = await api.userVariableUsage(v.name).catch(() => []);
    const ruleUses = await api.automationUsage(v.name).catch(() => []);
    const { lines, more } = usageSummary(uses);
    const rulesLine = ruleUses.length === 0 ? "" : `\n${t("globalVariables.delete.rules", ruleUses.map((r) => r.name).join(", "))}`;
    const message = uses.length === 0 && ruleUses.length === 0
      ? t("globalVariables.delete.confirm", v.name)
      : `${tn("globalVariables.delete.used", uses.length + ruleUses.length, v.name)}\n${lines.join("\n")}${more > 0 ? `\n${t("globalVariables.delete.more", String(more))}` : ""}${rulesLine}`;
    if (await confirmAsync(message, { danger: true })) await save((list ?? []).filter((x) => x.name !== v.name));
  };

  const problem = nameProblem(name, (list ?? []).map((v) => v.name), limits.maxNameLength);
  const full = (list?.length ?? 0) >= limits.maxCount;
  const add = async () => {
    if (problem || !list) return;
    if (await save([...list, newVariable(name, type)])) setName("");
  };

  return (
    <>
      <PageHeader title={t("preferences.category.globalVariables")} lead={t("globalVariables.hint")} alert={error} />

      <div className="st-bar">
        <span className={full ? "st-count full" : "st-count"}>{t("globalVariables.count", String(list?.length ?? 0), String(limits.maxCount))}</span>
      </div>

      {list?.length === 0 && <EmptyState title={t("globalVariables.empty.title")} hint={t("globalVariables.empty.hint")} />}
      {list && list.length > 0 && (
        <div role="table" aria-label={t("preferences.category.globalVariables")} style={{ marginTop: 8 }}>
          <div className="st-vt-head" role="row">
            <span>{t("globalVariables.name")}</span>
            <span>{t("globalVariables.type")}</span>
            <span>{t("globalVariables.start")}</span>
            <span>{t("globalVariables.keep")}</span>
            <span />
          </div>
          {list.map((v) => (
            <VariableRow key={v.name} variable={v} limits={limits} onChange={(change) => save(withChange(list, v.name, change))} onRemove={() => remove(v)} />
          ))}
        </div>
      )}

      <div className="st-add">
        <div onKeyDown={(e) => { if (e.key === "Enter") void add(); }}>
          <TextInput value={name} onChange={setName} maxLength={limits.maxNameLength + 10} placeholder={t("globalVariables.name")} label={t("globalVariables.name")} />
        </div>
        <SelectInput value={type} onChange={(v) => setType(v as UserVariableType)} label={t("globalVariables.type")}>
          <option value="number">{t("globalVariables.type.number")}</option>
          <option value="text">{t("globalVariables.type.text")}</option>
          <option value="boolean">{t("globalVariables.type.boolean")}</option>
        </SelectInput>
        <button type="button" className="st-btn" disabled={!!problem || full || !list} onClick={() => void add()}>{t("globalVariables.add")}</button>
      </div>
      {name && problem && <div role="alert" className="st-hint error" style={{ marginTop: 4 }}>{t(`globalVariables.name.${problem}`, String(limits.maxNameLength))}</div>}
      {full && <div className="st-hint" style={{ marginTop: 4 }}>{t("globalVariables.full", String(limits.maxCount))}</div>}
    </>
  );
}

function VariableRow({ variable: v, limits, onChange, onRemove }: {
  variable: UserVariableDef;
  limits: Limits;
  onChange: (change: Partial<UserVariableDef>) => Promise<boolean>;
  onRemove: () => void;
}) {
  const { t } = useT();
  const [start, setStart] = useState(startValueText(v));
  const [startBad, setStartBad] = useState(false);

  const commitStart = async (text: string) => {
    const parsed = parseStartValue(v.type, text);
    setStartBad(parsed === undefined);
    if (parsed === undefined || parsed === v.initial) return;
    if (!(await onChange({ initial: parsed }))) setStart(startValueText(v));
  };

  return (
    <div className="st-vt-row" role="row">
      <div className="st-var-name" title={`{${USER_PREFIX}${v.name}}`}><i>{USER_PREFIX}</i>{v.name}</div>
      <div className="st-var-type">{t(`globalVariables.type.${v.type}`)}</div>
      {v.type === "boolean" ? (
        <SelectInput
          value={v.initial === true ? "true" : v.initial === false ? "false" : ""} label={t("globalVariables.start")}
          onChange={(value) => void onChange({ initial: value === "" ? null : value === "true" })}
        >
          <option value="">{t("globalVariables.start.none")}</option>
          <option value="true">{t("globalVariables.true")}</option>
          <option value="false">{t("globalVariables.false")}</option>
        </SelectInput>
      ) : (
        <input
          type="text" className={startBad ? "invalid" : undefined} value={start} aria-label={t("globalVariables.start")} aria-invalid={startBad || undefined}
          placeholder={t("globalVariables.start.none")} maxLength={v.type === "text" ? limits.maxTextLength : 40}
          onChange={(e) => setStart(e.target.value)} onBlur={() => void commitStart(start)} onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); }}
        />
      )}
      <div className="st-var-keep" title={t("globalVariables.keep.hint")}>
        <Switch checked={v.keep} onChange={(keep) => void onChange({ keep })} label={t("globalVariables.keep")} />
      </div>
      <button type="button" className="st-ib danger" aria-label={t("globalVariables.delete")} title={t("globalVariables.delete")} onClick={onRemove}>
        <Trash2 size={14} />
      </button>
      <InlineText
        value={v.description} label={t("globalVariables.description")} placeholder={t("globalVariables.description")} maxLength={limits.maxDescriptionLength}
        onCommit={(description) => void onChange({ description })}
      />
      {startBad && <div role="alert" className="st-row-error">{t("globalVariables.start.bad")}</div>}
    </div>
  );
}
