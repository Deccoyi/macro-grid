import { useEffect, useRef, useState } from "react";
import { ChevronDown, ChevronRight, RefreshCw, X } from "lucide-react";
import { api } from "../../api/client";
import type { OptionsResult, SettingField, SettingOption, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { VariablePicker } from "../VariablePicker";
import { HotkeyCapture } from "./HotkeyCapture";
import { ColorField, Seg } from "../fields/controls";

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
 * plugin didn't provide (a hand-written form exists instead) never reach here.
 */
export function SchemaForm({ fields, values, onChange, fetchOptions, variableCatalog = [], runCommand }: SchemaFormProps) {
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
          runCommand={runCommand}
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

  if (field.kind === "Notice") {
    return (
      <div style={{ fontSize: 11.5, color: "var(--ms-warning, #facc15)", lineHeight: 1.4 }}>{field.description}</div>
    );
  }

  if (field.kind === "Button") {
    return <ButtonField field={field} values={values} runCommand={runCommand} />;
  }

  if (field.kind === "File") {
    const path = typeof value === "string" ? value : "";
    return (
      <label className="field">
        {field.label}
        <div style={{ display: "flex", gap: 6 }}>
          <input type="text" value={path} readOnly style={{ flex: 1 }} placeholder={field.placeholder ?? undefined} />
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
        {field.description && <FieldHint text={field.description} />}
      </label>
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
      <div className="field">
        <div style={{ display: "flex", alignItems: "center" }}>
          <span style={{ flex: 1 }}>{field.label}</span>
          {name && (
            <button type="button" className="ghost" style={{ padding: "2px 6px", fontSize: 11 }} onClick={() => onChange("")}>
              {t("schemaForm.variable.clear")}
            </button>
          )}
        </div>
        <VariablePicker
          catalog={variableCatalog}
          mode="bare"
          onInsert={(picked) => onChange(picked)}
          renderTrigger={(open) => (
            <button type="button" onClick={open} style={{ textAlign: "left", width: "100%" }}>
              {name || t("schemaForm.variable.pick")}
            </button>
          )}
        />
        {field.description && <FieldHint text={field.description} />}
      </div>
    );
  }

  if (field.kind === "Color") {
    const color = typeof value === "string" ? value : typeof field.default === "string" ? field.default : "";
    return (
      <div className="field">
        <span>{field.label}</span>
        <ColorField value={color} onChange={onChange} />
        {field.description && <FieldHint text={field.description} />}
      </div>
    );
  }

  if (field.kind === "Hotkey") {
    const combo = typeof value === "string" ? value : typeof field.default === "string" ? field.default : "";
    return (
      <div className="field">
        <span>{field.label}</span>
        <HotkeyCapture value={combo} onChange={onChange} />
        {field.description && <FieldHint text={field.description} />}
      </div>
    );
  }

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
    <div className="field">
      <div style={{ display: "flex", alignItems: "center" }}>
        <span style={{ flex: 1 }}>{field.label}</span>
        {rows.length > 1 && (
          <>
            <button type="button" className="ghost" style={{ fontSize: 11 }} onClick={() => setCollapsed(new Set())}>
              {t("schemaForm.expandAll")}
            </button>
            <button type="button" className="ghost" style={{ fontSize: 11 }} onClick={() => setCollapsed(new Set(rows.map((_, i) => i)))}>
              {t("schemaForm.collapseAll")}
            </button>
          </>
        )}
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {rows.map((row, index) => {
          const isOpen = !collapsed.has(index);
          return (
            <div key={(row.id as string | undefined) || index} style={{ border: "1px solid var(--ms-border)", borderRadius: 6 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 4, padding: "6px 6px 6px 4px" }}>
                <button
                  type="button"
                  className="ghost"
                  style={{ display: "flex", padding: 4 }}
                  onClick={() => toggle(index)}
                  aria-label={isOpen ? t("schemaForm.collapseRow") : t("schemaForm.expandRow")}
                >
                  {isOpen ? <ChevronDown size={13} /> : <ChevronRight size={13} />}
                </button>
                <span
                  style={{ flex: 1, fontSize: 12, cursor: "pointer", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
                  onClick={() => toggle(index)}
                >
                  {titleFor(row, index)}
                </span>
                <button
                  type="button"
                  className="ghost"
                  style={{ display: "flex", padding: 4 }}
                  title={t("schemaForm.removeRow")}
                  aria-label={t("schemaForm.removeRow")}
                  onClick={() => removeRow(index)}
                >
                  <X size={13} />
                </button>
              </div>
              {isOpen && (
                <div style={{ padding: "0 8px 8px 8px" }}>
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
        <button type="button" className="ghost" style={{ alignSelf: "flex-start" }} onClick={addRow}>
          {t("schemaForm.addRow")}
        </button>
      </div>
      {field.description && <FieldHint text={field.description} />}
    </div>
  );
}

function FieldHint({ text }: { text: string }) {
  return <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", lineHeight: 1.4 }}>{text}</span>;
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

  const run = async () => {
    if (!field.command || !runCommand) return;
    setRunning(true);
    setResult(null);
    try {
      const { text } = await runCommand(field.command, values);
      setResult(text);
    } catch (err) {
      setResult(err instanceof Error ? err.message : String(err));
    } finally {
      setRunning(false);
    }
  };

  return (
    <div className="field">
      <button type="button" disabled={running || !runCommand} onClick={run}>
        {running ? t("schemaForm.running") : field.label}
      </button>
      {result && <FieldHint text={result} />}
    </div>
  );
}
