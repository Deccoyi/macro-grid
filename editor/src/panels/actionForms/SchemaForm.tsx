import { useEffect, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Eye, EyeOff, RefreshCw, TriangleAlert, Variable, X } from "lucide-react";
import { api } from "../../api/client";
import type { OptionsResult, SettingField, SettingOption, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { VariablePicker } from "../VariablePicker";
import { ColorField, Field, NumberInput, RangeInput, Seg, SelectInput, Switch, TextInput, useFieldId } from "../fields/controls";
import { layoutFields, segmentedAsSelect } from "./schemaFormLayout";

interface SchemaFormProps {
  fields: SettingField[];
  values: Record<string, unknown>;
  onChange: (values: Record<string, unknown>) => void;
  /** Resolves a field's `optionsSource` against the current form values — either
   * api.getActionOptions(type, ...) or api.getPluginSettingsOptions(id, ...) bound by the caller. */
  fetchOptions: (sourceId: string, currentValues: Record<string, unknown>) => Promise<OptionsResult>;
  variableCatalog?: VariableInfo[];
  /** Runs a "Button" field's command against the plugin's ISettingsCommandHandler — only a plugin settings
   * form (PluginSettingsWindow.tsx) provides this; an action settings form has none, so Button renders
   * disabled there (an action handler has no side interface to run a command against). */
  runCommand?: (command: string, currentValues: Record<string, unknown>) => Promise<{ text: string | null }>;
}

/**
 * Generic renderer for a `SettingField[]` schema — the same component draws both action settings forms
 * (see forms.tsx's formFor) and a plugin's own settings window (PluginSettingsWindow.tsx). Fields the
 * plugin didn't provide (a hand-written form exists instead) never reach here. Every kind is drawn through
 * Field (docs/ui/ui-guidelines.md, "Properties panel field system"); consecutive fields that can share a row fill
 * a two-column grid, and a field that depends on another is indented (see schemaFormLayout.ts).
 */
export function SchemaForm({ fields, values, onChange, fetchOptions, variableCatalog = [], runCommand }: SchemaFormProps) {
  const set = (key: string, value: unknown) => onChange({ ...values, [key]: value });

  const row = (field: SettingField) => (
    <SchemaFieldRow
      key={field.key}
      field={field}
      value={values[field.key]}
      values={values}
      onChange={(v) => set(field.key, v)}
      fetchOptions={fetchOptions}
      variableCatalog={variableCatalog}
      runCommand={runCommand}
    />
  );

  return (
    <div className="pf-body">
      {layoutFields(fields.filter((f) => isVisible(f, values))).map((item) => {
        if (item.type === "grid") return <div key={item.fields[0]!.key} className="pf-grid-2">{item.fields.map(row)}</div>;
        if (item.type === "nest") return <div key={item.fields[0]!.key} className="pf-nest">{item.fields.map(row)}</div>;
        return row(item.field);
      })}
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

/** The text input of a Text or Password field; a password has its show/hide button inside, on the right. */
function TextValue({ field, value, onChange, inputRef }: { field: SettingField; value: string; onChange: (v: string) => void; inputRef: React.RefObject<HTMLInputElement | null> }) {
  const { t } = useT();
  const id = useFieldId();
  const [shown, setShown] = useState(false);
  const isPassword = field.kind === "Password";
  return (
    <div className={isPassword ? "pf-pw" : undefined}>
      <input
        ref={inputRef}
        id={id}
        type={isPassword && !shown ? "password" : "text"}
        value={value}
        placeholder={field.placeholder ?? undefined}
        onChange={(e) => onChange(e.target.value)}
      />
      {isPassword && (
        <button
          type="button"
          className="pf-icon-btn inside"
          title={shown ? t("schemaForm.password.hide") : t("schemaForm.password.show")}
          aria-label={shown ? t("schemaForm.password.hide") : t("schemaForm.password.show")}
          aria-pressed={shown}
          onClick={() => setShown((v) => !v)}
        >
          {shown ? <EyeOff size={14} /> : <Eye size={14} />}
        </button>
      )}
    </div>
  );
}

function SchemaFieldRow({
  field,
  value,
  values,
  onChange,
  fetchOptions,
  variableCatalog,
  runCommand,
}: {
  field: SettingField;
  value: unknown;
  values: Record<string, unknown>;
  onChange: (v: unknown) => void;
  fetchOptions: SchemaFormProps["fetchOptions"];
  variableCatalog: VariableInfo[];
  runCommand: SchemaFormProps["runCommand"];
}) {
  const { t } = useT();
  const textRef = useRef<HTMLInputElement | null>(null);
  // Called unconditionally (rules-of-hooks) — a no-op internally for kinds that never set optionsSource.
  const opts = useFieldOptions(field, values, fetchOptions);
  const hint = field.description ?? undefined;

  if (field.kind === "Notice") {
    return (
      <div role="note" className="pf-note">
        <TriangleAlert size={14} />
        <span>{field.description}</span>
      </div>
    );
  }

  if (field.kind === "Button") {
    return <ButtonField field={field} values={values} runCommand={runCommand} />;
  }

  if (field.kind === "File") {
    const path = typeof value === "string" ? value : "";
    return (
      <Field label={field.label} hint={hint}>
        <div className="pf-row">
          <div className="grow"><TextInput value={path} readOnly placeholder={field.placeholder ?? undefined} /></div>
          <button
            type="button"
            onClick={async () => {
              const picked = await api.browseForFile(field.label, field.fileFilter ?? "All files (*.*)|*.*");
              if (picked !== null) onChange(picked);
            }}
          >
            {t("schemaForm.browse")}
          </button>
        </div>
      </Field>
    );
  }

  if (field.kind === "List") {
    return (
      <ListField
        field={field}
        value={value}
        onChange={onChange}
        fetchOptions={fetchOptions}
        variableCatalog={variableCatalog}
        runCommand={runCommand}
      />
    );
  }

  if (field.kind === "Variable") {
    const name = typeof value === "string" ? value : "";
    return (
      <Field label={field.label} hint={hint}>
        <div className="pf-row">
          <div className="grow">
            <VariablePicker
              catalog={variableCatalog}
              mode="bare"
              onInsert={(picked) => onChange(picked)}
              renderTrigger={(open) => (
                <button type="button" className="pf-ctl pf-row pf-fill" onClick={open}>
                  <Variable size={14} />
                  <span className="pf-ellipsis">{name || t("schemaForm.variable.pick")}</span>
                </button>
              )}
            />
          </div>
          {name && (
            <button type="button" className="ghost pf-icon-btn" title={t("schemaForm.variable.clear")} aria-label={t("schemaForm.variable.clear")} onClick={() => onChange("")}>
              <X size={14} />
            </button>
          )}
        </div>
      </Field>
    );
  }

  if (field.kind === "Color") {
    const color = typeof value === "string" ? value : typeof field.default === "string" ? field.default : "";
    return (
      <Field single label={field.label} hint={hint}>
        <ColorField value={color} onChange={onChange} />
      </Field>
    );
  }

  if (field.kind === "Bool") {
    return (
      <Field inline label={field.label} hint={hint}>
        <Switch checked={Boolean(value ?? field.default ?? false)} onChange={onChange} />
      </Field>
    );
  }

  if (field.kind === "Slider") {
    const min = field.min ?? 0;
    const max = field.max ?? 100;
    const num = typeof value === "number" ? value : (typeof field.default === "number" ? field.default : min);
    return (
      <Field label={field.label} hint={hint}>
        <RangeInput value={num} min={min} max={max} step={field.step ?? undefined} onChange={onChange} />
      </Field>
    );
  }

  if (field.kind === "Number") {
    const num = typeof value === "number" ? value : (typeof field.default === "number" ? field.default : field.min ?? 0);
    return (
      <Field single label={field.label} hint={hint}>
        <NumberInput value={num} min={field.min ?? undefined} max={field.max ?? undefined} step={field.step ?? undefined} onChange={onChange} />
      </Field>
    );
  }

  if (field.kind === "Select" || field.kind === "Segmented") {
    const current = typeof value === "string" ? value : "";
    const knownValues = new Set(opts.options.map((o) => o.value));
    const stale = current && !knownValues.has(current);

    if (field.kind === "Segmented" && !segmentedAsSelect(field)) {
      return (
        <Field single label={field.label} hint={hint}>
          <Seg
            value={current}
            options={opts.options.map((o) => ({ value: o.value, label: o.label }))}
            onChange={onChange}
          />
        </Field>
      );
    }

    return (
      <Field
        single
        label={field.label}
        hint={hint}
        error={opts.error ?? undefined}
        action={field.optionsSource && (
          <button type="button" className="ghost" title={t("schemaForm.refresh")} aria-label={t("schemaForm.refresh")} onClick={opts.refresh} disabled={opts.loading}>
            <RefreshCw size={12} className={opts.loading ? "spin" : undefined} />
          </button>
        )}
      >
        <SelectInput value={current} onChange={onChange}>
          <option value="">{t("form.pickPlaceholder")}</option>
          {stale && <option value={current}>{`${current} ${t("schemaForm.notFound")}`}</option>}
          {opts.options.map((o) => (
            <option key={o.value} value={o.value}>{o.group ? `${o.group} › ${o.label}` : o.label}</option>
          ))}
        </SelectInput>
      </Field>
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
    <Field
      label={field.label}
      hint={hint}
      action={field.allowVariables && variableCatalog.length > 0 && (
        <VariablePicker
          catalog={variableCatalog}
          onInsert={insertVariable}
          renderTrigger={(open) => (
            <button type="button" className="ghost" onClick={open}>
              <Variable size={12} />
              {t("variable.add")}
            </button>
          )}
        />
      )}
    >
      <TextValue field={field} value={strValue} onChange={onChange} inputRef={textRef} />
    </Field>
  );
}

/**
 * A `SettingField[]`'s "List" field: each row collapses to one header line (a chevron, its title, a
 * remove button) so a long list — SoundBoard's sounds, for example — does not take over the whole panel.
 * The title is the row's own "name" field value when it has one and it is not blank, otherwise "Item N".
 * All rows start collapsed (a freshly opened form should be compact); a row just added with "+ Add" starts
 * expanded, since the person is about to fill it in. Collapse state is per row index, kept only in this
 * component (not part of the saved values) and re-keyed on add/remove so it stays attached to the right row.
 */
function ListField({
  field,
  value,
  onChange,
  fetchOptions,
  variableCatalog,
  runCommand,
}: {
  field: SettingField;
  value: unknown;
  onChange: (v: Record<string, unknown>[]) => void;
  fetchOptions: SchemaFormProps["fetchOptions"];
  variableCatalog: VariableInfo[];
  runCommand: SchemaFormProps["runCommand"];
}) {
  const { t } = useT();
  const rows = Array.isArray(value) ? (value as Record<string, unknown>[]) : [];
  const itemFields = field.itemFields ?? [];
  const nameFieldKey = itemFields.find((f) => f.key === "name")?.key;
  const [collapsed, setCollapsed] = useState<Set<number>>(() => new Set(rows.map((_, i) => i)));

  const setRows = (next: Record<string, unknown>[]) => onChange(next);
  const toggle = (index: number) => setCollapsed((s) => {
    const next = new Set(s);
    if (next.has(index)) next.delete(index); else next.add(index);
    return next;
  });

  const addRow = () => setRows([...rows, {}]); // the new row's index is not added to `collapsed`, so it opens expanded.
  const removeRow = (index: number) => {
    setRows(rows.filter((_, i) => i !== index));
    setCollapsed((s) => {
      const next = new Set<number>();
      for (const i of s) {
        if (i < index) next.add(i);
        else if (i > index) next.add(i - 1);
      }
      return next;
    });
  };

  const titleFor = (row: Record<string, unknown>, index: number): string => {
    const raw = nameFieldKey ? row[nameFieldKey] : undefined;
    const text = typeof raw === "string" ? raw.trim() : "";
    return text || t("schemaForm.untitledRow", String(index + 1));
  };

  return (
    <Field
      label={field.label}
      hint={field.description ?? undefined}
      action={rows.length > 1 && (
        <>
          <button type="button" className="ghost" onClick={() => setCollapsed(new Set())}>
            {t("schemaForm.expandAll")}
          </button>
          <button type="button" className="ghost" onClick={() => setCollapsed(new Set(rows.map((_, i) => i)))}>
            {t("schemaForm.collapseAll")}
          </button>
        </>
      )}
    >
      {rows.length > 0 && (
        <div className="pf-list">
          {rows.map((row, index) => {
            const isOpen = !collapsed.has(index);
            return (
              <div key={(row.id as string | undefined) || index}>
                <div className="pf-list-head">
                  <button
                    type="button"
                    className="ghost pf-icon-btn small"
                    onClick={() => toggle(index)}
                    aria-expanded={isOpen}
                    aria-label={isOpen ? t("schemaForm.collapseRow") : t("schemaForm.expandRow")}
                  >
                    {isOpen ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
                  </button>
                  <span className="grow pf-clickable" onClick={() => toggle(index)}>{titleFor(row, index)}</span>
                  <button
                    type="button"
                    className="ghost pf-icon-btn small"
                    title={t("schemaForm.removeRow")}
                    aria-label={t("schemaForm.removeRow")}
                    onClick={() => removeRow(index)}
                  >
                    <X size={14} />
                  </button>
                </div>
                {isOpen && (
                  <div className="pf-list-body">
                    <SchemaForm
                      fields={itemFields}
                      values={row}
                      onChange={(nextRow) => setRows(rows.map((r, i) => (i === index ? { ...row, ...nextRow } : r)))}
                      fetchOptions={fetchOptions}
                      variableCatalog={variableCatalog}
                      runCommand={runCommand}
                    />
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
      <button type="button" className="ghost pf-btn small" onClick={addRow}>
        {t("schemaForm.addRow")}
      </button>
    </Field>
  );
}

function ButtonField({
  field,
  values,
  runCommand,
}: {
  field: SettingField;
  values: Record<string, unknown>;
  runCommand: SchemaFormProps["runCommand"];
}) {
  const { t } = useT();
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const run = async () => {
    if (!field.command || !runCommand) return;
    setRunning(true);
    setResult(null);
    setError(null);
    try {
      const { text } = await runCommand(field.command, values);
      setResult(text);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="pf-field">
      <button type="button" className="pf-btn" disabled={running || !runCommand} onClick={run}>
        {running ? t("schemaForm.running") : field.label}
      </button>
      {error ? <span role="alert" className="pf-hint error">{error}</span> : result && <span className="pf-hint">{result}</span>}
    </div>
  );
}
