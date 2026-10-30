import { useEffect, useId, useState, type ReactNode } from "react";
import { useT } from "../i18n/I18nContext";
import { FieldContext, SelectInput } from "../panels/fields/controls";

/** Shared building blocks of the Settings window pages (docs/ui/settings-window-design.md). Pages never set their own colors or heights. */

export function PageHeader({ title, lead, alert }: { title: string; lead?: string; alert?: string | null }) {
  return (
    <>
      <h1 className="st-title">{title}</h1>
      {lead && <p className="st-lead">{lead}</p>}
      {alert && <div role="alert" className="st-alert">{alert}</div>}
    </>
  );
}

/** A group of setting rows under a 13 px head. */
export function SettingGroup({ title, children }: { title?: string; children: ReactNode }) {
  return (
    <section className="st-group">
      {title && <h2 className="st-group-head">{title}</h2>}
      <div className="st-rows">{children}</div>
    </section>
  );
}

/** One setting: name and optional hint on the left, the control in a fixed right column. A switch is right-aligned. */
export function SettingRow({ name, hint, control, switchControl, labelFor }: { name: string; hint?: ReactNode; control: ReactNode; switchControl?: boolean; labelFor?: string }) {
  return (
    <div className="st-row">
      <div>
        {labelFor ? <label className="st-row-name" htmlFor={labelFor}>{name}</label> : <div className="st-row-name">{name}</div>}
        {hint && <div className="st-row-hint">{hint}</div>}
      </div>
      <div className={switchControl ? "st-row-ctl switch" : "st-row-ctl"}>{control}</div>
    </div>
  );
}

/** A borderless one-line edit that shows its frame on hover and focus; hands the trimmed text over on blur or Enter, Escape puts the old text back. */
export function InlineText({ value, onCommit, placeholder, label, maxLength }: { value: string; onCommit: (v: string) => void; placeholder?: string; label: string; maxLength?: number }) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  return (
    <input
      type="text" className="st-inline" value={draft} placeholder={placeholder} aria-label={label} maxLength={maxLength}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={() => { if (draft !== value) onCommit(draft); }}
      onKeyDown={(e) => {
        if (e.key === "Enter") e.currentTarget.blur();
        if (e.key === "Escape") { setDraft(value); e.currentTarget.blur(); }
      }}
    />
  );
}

/** A weekday chip: on = accent, `aria-pressed`. */
export function DayChip({ on, label, onToggle }: { on: boolean; label: string; onToggle: () => void }) {
  return <button type="button" className="st-day" aria-pressed={on} onClick={onToggle}>{label}</button>;
}

/** Bold line plus a hint, centered in a bordered box. */
export function EmptyState({ title, hint }: { title: string; hint: string }) {
  return <div className="st-empty"><b>{title}</b>{hint}</div>;
}

/** An id for a control that a SettingRow name should label. */
export function useControlId(): string {
  return useId();
}

const TWO_DIGITS = (n: number) => String(n).padStart(2, "0");
const HOURS = Array.from({ length: 24 }, (_, i) => TWO_DIGITS(i));
const MINUTES = Array.from({ length: 60 }, (_, i) => TWO_DIGITS(i));

/** A time of day as "HH:mm" in two selects, so it looks like the other fields (the browser's own time picker cannot be themed). An empty value shows 00:00 until one part is changed. */
export function TimeInput({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const { t } = useT();
  const match = /^(\d{2}):(\d{2})$/.exec(value);
  const hour = match?.[1] ?? "00";
  const minute = match?.[2] ?? "00";
  return (
    <FieldContext.Provider value={null}>
    <div className="st-time">
      <SelectInput value={hour} label={t("automation.time.hour")} onChange={(h) => onChange(`${h}:${minute}`)}>
        {HOURS.map((h) => <option key={h} value={h}>{h}</option>)}
      </SelectInput>
      <span aria-hidden="true">:</span>
      <SelectInput value={minute} label={t("automation.time.minute")} onChange={(m) => onChange(`${hour}:${m}`)}>
        {MINUTES.map((m) => <option key={m} value={m}>{m}</option>)}
      </SelectInput>
    </div>
    </FieldContext.Provider>
  );
}
