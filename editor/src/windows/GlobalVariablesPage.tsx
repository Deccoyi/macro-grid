import { useEffect, useState } from "react";
import { Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { UserVariableDef, UserVariablesResponse, UserVariableType } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "../panels/fields/controls";
import { newVariable, nameProblem, parseStartValue, startValueText, usageSummary, USER_PREFIX, withChange } from "./globalVariables";

type Limits = UserVariablesResponse["limits"];

const FALLBACK_LIMITS: Limits = { maxCount: 200, maxNameLength: 40, maxDescriptionLength: 200, maxTextLength: 1024 };

/** The "Global Variable List" page of the Preferences window: variables the person defines and uses as {user.name} in texts, rules and actions.
 * Every change is sent to the server as the whole list, which accepts all of it or none of it. */
export function GlobalVariablesPage() {
  const { t } = useT();
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
      setList(next);
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      return false;
    }
  };

  const remove = async (v: UserVariableDef) => {
    const uses = await api.userVariableUsage(v.name).catch(() => []);
    const { lines, more } = usageSummary(uses);
    const message = uses.length === 0
      ? t("globalVariables.delete.confirm", v.name)
      : `${t("globalVariables.delete.used", v.name, String(uses.length))}\n${lines.join("\n")}${more > 0 ? `\n${t("globalVariables.delete.more", String(more))}` : ""}`;
    if (await confirmAsync(message, { danger: true })) await save((list ?? []).filter((x) => x.name !== v.name));
  };

  const problem = nameProblem(name, (list ?? []).map((v) => v.name), limits.maxNameLength);
  const full = (list?.length ?? 0) >= limits.maxCount;
  const add = async () => {
    if (problem || !list) return;
    if (await save([...list, newVariable(name, type)])) setName("");
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12, maxWidth: 640 }}>
      <SectionLabel>{t("preferences.category.globalVariables")}</SectionLabel>
      <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("globalVariables.hint")}</p>
      {error && <div role="alert" style={{ fontSize: 12, color: "var(--ms-danger)" }}>{error}</div>}

      {list?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("globalVariables.empty")}</div>}
      {list?.map((v) => (
        <VariableRow key={v.name} variable={v} limits={limits} onChange={(change) => save(withChange(list, v.name, change))} onRemove={() => remove(v)} />
      ))}

      <div style={{ display: "flex", gap: 6, alignItems: "center", flexWrap: "wrap", borderTop: "1px solid var(--ms-border)", paddingTop: 10 }}>
        <input
          type="text" value={name} maxLength={limits.maxNameLength + 10} placeholder={t("globalVariables.name")} aria-label={t("globalVariables.name")}
          onChange={(e) => setName(e.target.value)} onKeyDown={(e) => { if (e.key === "Enter") void add(); }} style={{ width: 180 }}
        />
        <select value={type} onChange={(e) => setType(e.target.value as UserVariableType)} aria-label={t("globalVariables.type")}>
          <option value="number">{t("globalVariables.type.number")}</option>
          <option value="text">{t("globalVariables.type.text")}</option>
          <option value="boolean">{t("globalVariables.type.boolean")}</option>
        </select>
        <button type="button" className="ghost" disabled={!!problem || full || !list} onClick={() => void add()}>{t("globalVariables.add")}</button>
      </div>
      {name && problem && <div style={{ fontSize: 11.5, color: "var(--ms-danger)" }}>{t(`globalVariables.name.${problem}`, String(limits.maxNameLength))}</div>}
      {full && <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("globalVariables.full", String(limits.maxCount))}</div>}
    </div>
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
  const [description, setDescription] = useState(v.description);
  const [startBad, setStartBad] = useState(false);

  const commitStart = async (text: string) => {
    const parsed = parseStartValue(v.type, text);
    setStartBad(parsed === undefined);
    if (parsed === undefined || parsed === v.initial) return;
    if (!(await onChange({ initial: parsed }))) setStart(startValueText(v));
  };

  return (
    <div style={{ display: "grid", gridTemplateColumns: "minmax(120px, 1fr) 90px 150px auto auto", gap: 6, alignItems: "center", padding: "6px 0", borderTop: "1px solid var(--ms-border)" }}>
      <div style={{ fontSize: 12.5, fontFamily: "var(--ms-font-mono, monospace)", overflow: "hidden", textOverflow: "ellipsis" }} title={`{${USER_PREFIX}${v.name}}`}>
        {USER_PREFIX}{v.name}
      </div>
      <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t(`globalVariables.type.${v.type}`)}</div>
      {v.type === "boolean" ? (
        <select
          value={v.initial === true ? "true" : v.initial === false ? "false" : ""} aria-label={t("globalVariables.start")}
          onChange={(e) => void onChange({ initial: e.target.value === "" ? null : e.target.value === "true" })}
        >
          <option value="">{t("globalVariables.start.none")}</option>
          <option value="true">{t("globalVariables.true")}</option>
          <option value="false">{t("globalVariables.false")}</option>
        </select>
      ) : (
        <input
          type="text" value={start} aria-label={t("globalVariables.start")} placeholder={t("globalVariables.start.none")} maxLength={v.type === "text" ? limits.maxTextLength : 40}
          style={startBad ? { borderColor: "var(--ms-danger)" } : undefined} title={startBad ? t("globalVariables.start.bad") : undefined}
          onChange={(e) => setStart(e.target.value)} onBlur={() => void commitStart(start)} onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); }}
        />
      )}
      <label style={{ display: "flex", alignItems: "center", gap: 4, fontSize: 11.5 }} title={t("globalVariables.keep.hint")}>
        <input type="checkbox" checked={v.keep} onChange={(e) => void onChange({ keep: e.target.checked })} />
        {t("globalVariables.keep")}
      </label>
      <button type="button" className="ghost" title={t("globalVariables.delete")} onClick={onRemove} style={{ padding: 5, color: "var(--ms-danger)" }}>
        <Trash2 size={13} />
      </button>
      <input
        type="text" value={description} maxLength={limits.maxDescriptionLength} placeholder={t("globalVariables.description")} aria-label={t("globalVariables.description")}
        style={{ gridColumn: "1 / -1", fontSize: 11.5 }}
        onChange={(e) => setDescription(e.target.value)}
        onBlur={() => { if (description !== v.description) void onChange({ description }); }}
        onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); }}
      />
    </div>
  );
}
