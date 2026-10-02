import { useEffect, useState, type JSX } from "react";
import { Variable, X } from "lucide-react";
import { webUrlHost, type ActionBinding, type Page } from "@macro/renderer";
import { api } from "../../api/client";
import type { ActionInfo, OptionsResult, ProfileSummary, SettingField, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { Field, NumberInput, SelectInput, TextAreaInput, TextInput, useFieldId } from "../fields/controls";
import { VariablePicker } from "../VariablePicker";
import { WebWarning } from "../fields/WebFields";
import { HotkeyCapture } from "./HotkeyCapture";
import { SchemaForm } from "./SchemaForm";

interface ActionFormProps {
  binding: ActionBinding;
  onChange: (settings: Record<string, unknown>) => void;
  pages: Page[];
  profiles: ProfileSummary[];
  actionInfo?: ActionInfo;
  variableCatalog?: VariableInfo[];
}

const str = (v: unknown, fallback = "") => (typeof v === "string" ? v : fallback);
const num = (v: unknown, fallback = 0) => (typeof v === "number" ? v : fallback);

function HotkeyForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  return (
    <Field label={t("form.hotkey.label")}>
      <HotkeyCapture value={str(binding.settings.keys)} onChange={(keys) => onChange({ keys })} />
    </Field>
  );
}

function TypeTextForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  return (
    <Field label={t("form.typeText.label")}>
      <TextAreaInput value={str(binding.settings.text)} onChange={(text) => onChange({ text })} />
    </Field>
  );
}

function PageActionForm({ binding, onChange, pages }: ActionFormProps) {
  const { t } = useT();
  const mode = str(binding.settings.mode, "goto");
  return (
    <>
      <Field label={t("form.page.mode")}>
        <SelectInput value={mode} onChange={(v) => onChange({ mode: v, pageId: binding.settings.pageId })}>
          <option value="goto">{t("form.page.mode.goto")}</option>
          <option value="next">{t("form.page.mode.next")}</option>
          <option value="prev">{t("form.page.mode.prev")}</option>
          <option value="back">{t("form.page.mode.back")}</option>
        </SelectInput>
      </Field>
      {mode === "goto" && (
        <Field label={t("form.page.page")}>
          <SelectInput value={str(binding.settings.pageId)} onChange={(v) => onChange({ mode, pageId: v })}>
            <option value="">{t("form.pickPlaceholder")}</option>
            {pages.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </SelectInput>
        </Field>
      )}
    </>
  );
}

function ProfileActionForm({ binding, onChange, profiles }: ActionFormProps) {
  const { t } = useT();
  return (
    <Field label={t("form.profile.label")}>
      <SelectInput value={str(binding.settings.profileId)} onChange={(v) => onChange({ profileId: v })}>
        <option value="">{t("form.pickPlaceholder")}</option>
        {profiles.map((p) => (
          <option key={p.id} value={p.id}>{p.name}</option>
        ))}
      </SelectInput>
    </Field>
  );
}

/** core.web: pick one of the profile's web widgets and what to do with it. The action stores the widget's id, so renaming it never breaks the button. */
function WebActionForm({ binding, onChange, pages }: ActionFormProps) {
  const { t } = useT();
  const widgetId = str(binding.settings.widgetId);
  const mode = str(binding.settings.mode, "set");
  const url = str(binding.settings.url);
  const widgets = pages.flatMap((p) =>
    p.widgets
      .filter((w) => w.type === "web")
      .map((w) => {
        const own = typeof w.props?.url === "string" ? webUrlHost(w.props.url) : "";
        return { id: w.id, label: `${w.name || own || t("widget.type.web")} — ${p.name}` };
      }),
  );
  const missing = widgetId !== "" && !widgets.some((w) => w.id === widgetId);
  const set = (patch: Record<string, unknown>) => onChange({ widgetId, mode, url, ...patch });
  return (
    <>
      <Field label={t("form.web.widget")} error={missing ? t("form.web.missing") : undefined}>
        <SelectInput value={widgetId} onChange={(v) => set({ widgetId: v })}>
          <option value="">{t("form.pickPlaceholder")}</option>
          {missing && <option value={widgetId}>{t("form.web.missing")}</option>}
          {widgets.map((w) => (
            <option key={w.id} value={w.id}>{w.label}</option>
          ))}
        </SelectInput>
      </Field>
      <Field label={t("form.web.mode")}>
        <SelectInput value={mode} onChange={(v) => set({ mode: v })}>
          <option value="set">{t("form.web.mode.set")}</option>
          <option value="reset">{t("form.web.mode.reset")}</option>
          <option value="reload">{t("form.web.mode.reload")}</option>
        </SelectInput>
      </Field>
      {mode === "set" && (
        <>
          <Field label={t("form.web.url")}>
            <TextInput value={url} onChange={(v) => set({ url: v })} placeholder={t("fields.web.urlPlaceholder")} />
          </Field>
          <WebWarning url={url} />
        </>
      )}
      <span className="pf-hint">{t("form.web.thisDevice")}</span>
    </>
  );
}

function OpenApplicationForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const browse = async () => {
    const path = await api.browseForExecutable();
    if (path) onChange({ target: path, arguments: binding.settings.arguments });
  };
  return (
    <>
      <Field label={t("form.openApp.label")}>
        <div className="pf-row">
          <div className="grow">
            <TextInput
              readOnly
              value={str(binding.settings.target)}
              placeholder={t("form.openApp.browsePlaceholder")}
              onClick={browse}
            />
          </div>
          <button type="button" className="ghost pf-ctl" onClick={browse}>
            {t("form.openApp.browse")}
          </button>
        </div>
      </Field>
      <Field label={t("form.openApp.arguments")}>
        <TextInput value={str(binding.settings.arguments)} onChange={(v) => onChange({ target: binding.settings.target, arguments: v })} />
      </Field>
    </>
  );
}

function OpenUrlActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const url = str(binding.settings.url);
  const looksValid = url === "" || /^https?:\/\/.+/i.test(url);
  return (
    <Field label={t("form.openUrl.label")} error={looksValid ? undefined : t("form.openUrl.invalid")}>
      <TextInput value={url} onChange={(v) => onChange({ url: v })} placeholder="https://example.com/..." />
    </Field>
  );
}

/** The text of a number of seconds, kept as typed, labelled by the nearest Field. */
function SecondsInput({ text, onChange }: { text: string; onChange: (text: string) => void }) {
  const id = useFieldId();
  return (
    <div className="pf-num has-unit">
      <input id={id} type="number" min={0} max={60} step={0.1} value={text} onChange={(e) => onChange(e.target.value)} />
      <span className="unit" aria-hidden="true">s</span>
    </div>
  );
}

/** The wait is shown in seconds; the stored setting stays in milliseconds. The text is kept as typed ("0." must not turn into "0"). */
function DelayActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const ms = num(binding.settings.ms);
  const [text, setText] = useState(() => String(ms / 1000));
  useEffect(() => {
    setText((current) => (Math.round(Number(current) * 1000) === ms ? current : String(ms / 1000)));
  }, [ms]);
  return (
    <Field label={t("form.delay.label")}>
      <SecondsInput
        text={text}
        onChange={(value) => {
          setText(value);
          const seconds = Number(value);
          if (value !== "" && Number.isFinite(seconds)) onChange({ ms: Math.round(seconds * 1000) });
        }}
      />
    </Field>
  );
}

function StopForm() {
  const { t } = useT();
  return <div className="pf-note info">{t("action.logic.stopNote")}</div>;
}

/** No settings to configure — the action reads the widget's live dragged value instead (core.setVolume). */
function NoSettingsForm() {
  const { t } = useT();
  return <div className="pf-note info">{t("form.noSettings")}</div>;
}

function SetMuteActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const muted = binding.settings.muted !== false;
  return (
    <Field label={t("form.setMute.label")}>
      <SelectInput value={muted ? "mute" : "unmute"} onChange={(v) => onChange({ muted: v === "mute" })}>
        <option value="mute">{t("form.setMute.mute")}</option>
        <option value="unmute">{t("form.setMute.unmute")}</option>
      </SelectInput>
    </Field>
  );
}

/** Only the person's own variables can be written; system and plugin variables are read-only, so these forms never offer them. */
function writableVariables(catalog: VariableInfo[] | undefined, type?: string): VariableInfo[] {
  return (catalog ?? []).filter((v) => v.name.startsWith("user.") && (type === undefined || (v.type ?? "text") === type));
}

/** The variable a small variable action changes, picked the same way as everywhere else (the variable picker). Keeps the other settings. */
function VariableTargetField({ binding, onChange, variableCatalog, type, hintKey }: ActionFormProps & { type?: string; hintKey?: "form.variable.onlyNumber" | "form.variable.onlyBoolean" }) {
  const { t } = useT();
  const options = writableVariables(variableCatalog, type);
  const name = str(binding.settings.variable);
  const known = (variableCatalog ?? []).some((v) => v.name === name);
  const hint = options.length === 0 ? t(hintKey ?? "form.variable.none") : undefined;
  return (
    <Field label={t("form.variable.target")} hint={hint} error={name && !known ? t("form.variable.missing") : undefined}>
      <div className="pf-row">
        <div className="grow">
          <VariablePicker
            catalog={options}
            mode="bare"
            onInsert={(picked) => onChange({ ...binding.settings, variable: picked })}
            renderTrigger={(open) => (
              <button type="button" className="pf-ctl pf-row pf-fill" onClick={open} disabled={options.length === 0 && !name}>
                <Variable size={14} />
                <span className="pf-ellipsis">{name || t("form.variable.pick")}</span>
              </button>
            )}
          />
        </div>
        {name && (
          <button type="button" className="ghost pf-icon-btn" title={t("schemaForm.variable.clear")} aria-label={t("schemaForm.variable.clear")} onClick={() => onChange({ ...binding.settings, variable: "" })}>
            <X size={14} />
          </button>
        )}
      </div>
    </Field>
  );
}

/** The value field of a set: True / False for a True/False variable, a text box (with variables) otherwise. */
function VariableValueField({ binding, onChange, variableCatalog }: ActionFormProps) {
  const { t } = useT();
  const type = (variableCatalog ?? []).find((v) => v.name === str(binding.settings.variable))?.type ?? "text";
  const set = (value: unknown) => onChange({ ...binding.settings, value });
  if (type === "boolean") {
    const current = str(binding.settings.value).toLowerCase() === "false" ? "false" : "true";
    return (
      <Field label={t("form.variable.value")}>
        <SelectInput value={current} onChange={set}>
          <option value="true">{t("form.variable.true")}</option>
          <option value="false">{t("form.variable.false")}</option>
        </SelectInput>
      </Field>
    );
  }
  const valueField = { key: "value", label: t("form.variable.value"), kind: "Text", allowVariables: true, description: type === "number" ? t("form.variable.valueNumberHint") : undefined } as SettingField;
  return (
    <SchemaForm
      fields={[valueField]}
      values={binding.settings as Record<string, unknown>}
      onChange={(values) => set(values.value)}
      fetchOptions={() => Promise.resolve({ options: [] } as unknown as OptionsResult)}
      variableCatalog={variableCatalog}
    />
  );
}

function VariableSetValueForm(props: ActionFormProps) {
  return (
    <>
      <VariableTargetField {...props} />
      <VariableValueField {...props} />
    </>
  );
}

function VariableAddForm(props: ActionFormProps) {
  const { t } = useT();
  const raw = props.binding.settings.amount;
  return (
    <>
      <VariableTargetField {...props} type="number" hintKey="form.variable.onlyNumber" />
      <Field label={t("form.variable.amount")}>
        <NumberInput step="any" value={typeof raw === "number" ? raw : 1} onChange={(v) => props.onChange({ ...props.binding.settings, amount: v })} />
      </Field>
    </>
  );
}

function VariableToggleForm(props: ActionFormProps) {
  return <VariableTargetField {...props} type="boolean" hintKey="form.variable.onlyBoolean" />;
}

function VariableResetForm(props: ActionFormProps) {
  const { t } = useT();
  return (
    <>
      <VariableTargetField {...props} />
      <span className="pf-hint">{t("form.variable.resetNote")}</span>
    </>
  );
}

/** The ways the older all-in-one Set variable action can change a variable of each type (the server refuses the others). */
export function setVariableModes(type: string | undefined): string[] {
  if (type === "number") return ["set", "add", "reset"];
  if (type === "boolean") return ["set", "toggle", "reset"];
  return ["set", "reset"];
}

const SET_VARIABLE_MODE_KEYS = {
  set: "form.setVariable.mode.set",
  add: "form.setVariable.mode.add",
  toggle: "form.setVariable.mode.toggle",
  reset: "form.setVariable.mode.reset",
} as const;

/** Only for buttons saved before the four small variable actions existed; the picker no longer offers the old action. */
function SetVariableForm(props: ActionFormProps) {
  const { t } = useT();
  const { binding, onChange, variableCatalog } = props;
  const current = writableVariables(variableCatalog).find((v) => v.name === str(binding.settings.variable));
  const modes = setVariableModes(current?.type);
  const mode = modes.includes(str(binding.settings.mode, "set")) ? str(binding.settings.mode, "set") : "set";
  return (
    <>
      <VariableTargetField {...props} onChange={(settings) => onChange({ ...settings, mode: "set" })} />
      <Field label={t("form.setVariable.mode")}>
        <SelectInput value={mode} onChange={(v) => onChange({ ...binding.settings, mode: v })}>
          {modes.map((m) => <option key={m} value={m}>{t(SET_VARIABLE_MODE_KEYS[m as keyof typeof SET_VARIABLE_MODE_KEYS])}</option>)}
        </SelectInput>
      </Field>
      {mode === "set" && <VariableValueField {...props} />}
      {mode === "add" && (
        <Field label={t("form.variable.amount")}>
          <NumberInput step="any" value={num(binding.settings.amount, 1)} onChange={(v) => onChange({ ...binding.settings, amount: v })} />
        </Field>
      )}
    </>
  );
}

/** Raw JSON fallback for action types the editor doesn't have a dedicated form for yet (future plugins). */
function GenericJsonForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const text = JSON.stringify(binding.settings, null, 2);
  return (
    <Field label={t("form.json.label")}>
      <TextAreaInput
        rows={4}
        mono
        defaultValue={text}
        onBlur={(value) => {
          try {
            onChange(JSON.parse(value || "{}"));
          } catch {
            // Leave the last valid settings in place rather than corrupting them on invalid JSON.
          }
        }}
      />
    </Field>
  );
}

const ACTION_FORMS: Record<string, (props: ActionFormProps) => JSX.Element> = {
  "core.hotkey": HotkeyForm,
  "core.typeText": TypeTextForm,
  "core.page": PageActionForm,
  "core.profile": ProfileActionForm,
  "core.web": WebActionForm,
  "core.open": OpenApplicationForm,
  "core.openUrl": OpenUrlActionForm,
  "core.delay": DelayActionForm,
  "core.stop": StopForm,
  "core.setVolume": NoSettingsForm,
  "core.toggleMute": NoSettingsForm,
  "core.setMute": SetMuteActionForm,
  "core.setVariable": SetVariableForm,
  "core.variable.set": VariableSetValueForm,
  "core.variable.add": VariableAddForm,
  "core.variable.toggle": VariableToggleForm,
  "core.variable.reset": VariableResetForm,
};

/** A plugin action with a declared schema (`ActionInfo.fields`, from IActionDescriptor) and no
 * hand-written form renders through the generic SchemaForm instead of the raw-JSON fallback. */
function SchemaActionForm({ binding, onChange, actionInfo, variableCatalog }: ActionFormProps) {
  const fields = actionInfo?.fields;
  if (!fields || fields.length === 0) return <GenericJsonForm binding={binding} onChange={onChange} pages={[]} profiles={[]} />;
  return (
    <SchemaForm
      fields={fields}
      values={binding.settings as Record<string, unknown>}
      onChange={onChange}
      fetchOptions={(sourceId, values) => api.getActionOptions(binding.type, sourceId, values)}
      variableCatalog={variableCatalog}
    />
  );
}

export function formFor(type: string, actionInfo?: ActionInfo): (props: ActionFormProps) => JSX.Element {
  return ACTION_FORMS[type] ?? (actionInfo?.fields?.length ? SchemaActionForm : GenericJsonForm);
}
