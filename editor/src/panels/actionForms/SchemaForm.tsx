import { useEffect, useRef, useState } from "react";
import { RefreshCw } from "lucide-react";
import type { OptionsResult, SettingField, SettingOption, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { VariablePicker } from "../VariablePicker";
import { Seg } from "../fields/controls";

interface SchemaFormProps {
  fields: SettingField[];
  values: Record<string, unknown>;
  onChange: (values: Record<string, unknown>) => void;
  /** Resolves a field's `optionsSource` against the current form values — either
   * api.getActionOptions(type, ...) or api.getPluginSettingsOptions(id, ...) bound by the caller. */
  fetchOptions: (sourceId: string, currentValues: Record<string, unknown>) => Promise<OptionsResult>;
  variableCatalog?: VariableInfo[];
}

/**
 * Generic renderer for a `SettingField[]` schema — the same component draws both action settings forms
 * (see forms.tsx's formFor) and a plugin's own settings window (PluginSettingsWindow.tsx). Fields the
 * plugin didn't provide (a hand-written form exists instead) never reach here.
 */
export function SchemaForm({ fields, values, onChange, fetchOptions, variableCatalog = [] }: SchemaFormProps) {
  const set = (key: string, value: unknown) => onChange({ ...values, [key]: value });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      {fields.filter((f) => isVisible(f, values)).map((field) => (
        <SchemaFieldRow
          key={field.key}
          field={field}
          value={values[field.key]}
          values={values}
          onChange={(v) => set(field.key, v)}
          fetchOptions={fetchOptions}
          variableCatalog={variableCatalog}
        />
      ))}
    </div>
  );
}

function isVisible(field: SettingField, values: Record<string, unknown>): boolean {
  if (!field.visibleWhen) return true;
  const [depKey, depValue] = field.visibleWhen.split("=");
  if (!depKey) return true;
  return String(values[depKey] ?? "") === (depValue ?? "");
}

function useFieldOptions(field: SettingField, values: Record<string, unknown>, fetchOptions: SchemaFormProps["fetchOptions"]) {
  const [state, setState] = useState<{ loading: boolean; options: SettingOption[]; error: string | null }>({
    loading: false,
    options: field.options ?? [],
    error: null,
  });
  const [bump, setBump] = useState(0);
  const depsKey = JSON.stringify((field.dependsOn ?? []).map((k) => values[k]));

  useEffect(() => {
    if (!field.optionsSource) return;
    let cancelled = false;
    setState((s) => ({ ...s, loading: true }));
    fetchOptions(field.optionsSource, values)
      .then((res) => {
        if (cancelled) return;
        setState({ loading: false, options: res.options, error: res.error ?? null });
      })
      .catch((err) => {
        if (cancelled) return;
        setState({ loading: false, options: [], error: err instanceof Error ? err.message : String(err) });
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [field.optionsSource, depsKey, bump]);

  return { ...state, refresh: () => setBump((n) => n + 1) };
}

function SchemaFieldRow({
  field,
  value,
  values,
  onChange,
  fetchOptions,
  variableCatalog,
}: {
  field: SettingField;
  value: unknown;
  values: Record<string, unknown>;
  onChange: (v: unknown) => void;
  fetchOptions: SchemaFormProps["fetchOptions"];
  variableCatalog: VariableInfo[];
}) {
  const { t } = useT();
  const textRef = useRef<HTMLInputElement | null>(null);
  // Called unconditionally (rules-of-hooks) — a no-op internally for kinds that never set optionsSource.
  const opts = useFieldOptions(field, values, fetchOptions);

  if (field.kind === "Bool") {
    return (
      <label className="field" style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
        <input type="checkbox" checked={Boolean(value ?? field.default ?? false)} onChange={(e) => onChange(e.target.checked)} />
        {field.label}
      </label>
    );
  }

  if (field.kind === "Number" || field.kind === "Slider") {
    const num = typeof value === "number" ? value : (typeof field.default === "number" ? field.default : field.min ?? 0);
    return (
      <label className="field">
        {field.label}
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <input
            type={field.kind === "Slider" ? "range" : "number"}
            min={field.min ?? undefined}
            max={field.max ?? undefined}
            step={field.step ?? undefined}
            value={num}
            onChange={(e) => onChange(Number(e.target.value))}
            style={{ flex: field.kind === "Slider" ? 1 : undefined }}
          />
          {field.kind === "Slider" && <span style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", width: 40, textAlign: "right" }}>{num}</span>}
        </div>
        {field.description && <FieldHint text={field.description} />}
      </label>
    );
  }

  if (field.kind === "Select" || field.kind === "Segmented") {
    const current = typeof value === "string" ? value : "";
    const knownValues = new Set(opts.options.map((o) => o.value));
    const stale = current && !knownValues.has(current);

    if (field.kind === "Segmented" && !field.optionsSource) {
      return (
        <label className="field">
          {field.label}
          <Seg
            value={current}
            options={opts.options.map((o) => ({ value: o.value, label: o.label }))}
            onChange={onChange}
          />
        </label>
      );
    }

    return (
      <label className="field">
        <div style={{ display: "flex", alignItems: "center" }}>
          <span style={{ flex: 1 }}>{field.label}</span>
          {field.optionsSource && (
            <button type="button" className="ghost" title={t("schemaForm.refresh")} onClick={opts.refresh} style={{ display: "flex", padding: 4 }} disabled={opts.loading}>
              <RefreshCw size={12} className={opts.loading ? "spin" : undefined} />
            </button>
          )}
        </div>
        <select value={current} onChange={(e) => onChange(e.target.value)}>
          <option value="">{t("form.pickPlaceholder")}</option>
          {stale && <option value={current}>{`${current} ${t("schemaForm.notFound")}`}</option>}
          {opts.options.map((o) => (
            <option key={o.value} value={o.value}>{o.group ? `${o.group} › ${o.label}` : o.label}</option>
          ))}
        </select>
        {opts.error && <span style={{ color: "var(--ms-danger)", fontSize: 11 }}>{opts.error}</span>}
        {field.description && !opts.error && <FieldHint text={field.description} />}
      </label>
    );
  }

  // Text / Password
  const strValue = typeof value === "string" ? value : (typeof field.default === "string" ? field.default : "");
  const insertVariable = (token: string) => {
    const el = textRef.current;
    if (!el) { onChange(strValue + token); return; }
    const start = el.selectionStart ?? strValue.length;
    const end = el.selectionEnd ?? strValue.length;
    const next = strValue.slice(0, start) + token + strValue.slice(end);
    onChange(next);
    requestAnimationFrame(() => {
      el.focus();
      el.setSelectionRange(start + token.length, start + token.length);
    });
  };

  return (
    <label className="field">
      <div style={{ display: "flex", alignItems: "center" }}>
        <span style={{ flex: 1 }}>{field.label}</span>
        {field.allowVariables && variableCatalog.length > 0 && (
          <VariablePicker
            catalog={variableCatalog}
            onInsert={insertVariable}
            renderTrigger={(open) => (
              <button type="button" className="ghost" onClick={open} style={{ padding: "2px 6px", fontSize: 11 }}>
                {t("variable.add")}
              </button>
            )}
          />
        )}
      </div>
      <input
        ref={textRef}
        type={field.kind === "Password" ? "password" : "text"}
        value={strValue}
        placeholder={field.placeholder ?? undefined}
        onChange={(e) => onChange(e.target.value)}
      />
      {field.description && <FieldHint text={field.description} />}
    </label>
  );
}

function FieldHint({ text }: { text: string }) {
  return <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", lineHeight: 1.4 }}>{text}</span>;
}
