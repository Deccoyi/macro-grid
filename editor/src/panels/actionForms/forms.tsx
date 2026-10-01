import { useEffect, useState, type JSX } from "react";
import { webUrlHost, type ActionBinding, type Page } from "@macro/renderer";
import { api } from "../../api/client";
import type { ActionInfo, OptionsResult, ProfileSummary, SettingField, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
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
    <label className="field">
      {t("form.hotkey.label")}
      <HotkeyCapture value={str(binding.settings.keys)} onChange={(keys) => onChange({ keys })} />
    </label>
  );
}

function TypeTextForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  return (
    <label className="field">
      {t("form.typeText.label")}
      <textarea rows={2} value={str(binding.settings.text)} onChange={(e) => onChange({ text: e.target.value })} />
    </label>
  );
}

function PageActionForm({ binding, onChange, pages }: ActionFormProps) {
  const { t } = useT();
  const mode = str(binding.settings.mode, "goto");
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label className="field">
        {t("form.page.mode")}
        <select value={mode} onChange={(e) => onChange({ mode: e.target.value, pageId: binding.settings.pageId })}>
          <option value="goto">{t("form.page.mode.goto")}</option>
          <option value="next">{t("form.page.mode.next")}</option>
          <option value="prev">{t("form.page.mode.prev")}</option>
          <option value="back">{t("form.page.mode.back")}</option>
        </select>
      </label>
      {mode === "goto" && (
        <label className="field">
          {t("form.page.page")}
          <select value={str(binding.settings.pageId)} onChange={(e) => onChange({ mode, pageId: e.target.value })}>
            <option value="">{t("form.pickPlaceholder")}</option>
            {pages.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </label>
      )}
    </div>
  );
}

function ProfileActionForm({ binding, onChange, profiles }: ActionFormProps) {
  const { t } = useT();
  return (
    <label className="field">
      {t("form.profile.label")}
      <select value={str(binding.settings.profileId)} onChange={(e) => onChange({ profileId: e.target.value })}>
        <option value="">{t("form.pickPlaceholder")}</option>
        {profiles.map((p) => (
          <option key={p.id} value={p.id}>{p.name}</option>
        ))}
      </select>
    </label>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label className="field">
        {t("form.web.widget")}
        <select value={widgetId} onChange={(e) => set({ widgetId: e.target.value })} style={missing ? { borderColor: "var(--ms-danger)" } : undefined}>
          <option value="">{t("form.pickPlaceholder")}</option>
          {missing && <option value={widgetId}>{t("form.web.missing")}</option>}
          {widgets.map((w) => (
            <option key={w.id} value={w.id}>{w.label}</option>
          ))}
        </select>
      </label>
      <label className="field">
        {t("form.web.mode")}
        <select value={mode} onChange={(e) => set({ mode: e.target.value })}>
          <option value="set">{t("form.web.mode.set")}</option>
          <option value="reset">{t("form.web.mode.reset")}</option>
          <option value="reload">{t("form.web.mode.reload")}</option>
        </select>
      </label>
      {mode === "set" && (
        <>
          <label className="field">
            {t("form.web.url")}
            <input type="text" value={url} onChange={(e) => set({ url: e.target.value })} placeholder={t("fields.web.urlPlaceholder")} />
          </label>
          <WebWarning url={url} />
        </>
      )}
      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>{t("form.web.thisDevice")}</p>
    </div>
  );
}

function OpenApplicationForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label className="field">
        {t("form.openApp.label")}
        <div style={{ display: "flex", gap: 6 }}>
          <input
            type="text"
            readOnly
            value={str(binding.settings.target)}
            placeholder={t("form.openApp.browsePlaceholder")}
            onClick={async () => {
              const path = await api.browseForExecutable();
              if (path) onChange({ target: path, arguments: binding.settings.arguments });
            }}
          />
          <button
            type="button"
            className="ghost"
            onClick={async () => {
              const path = await api.browseForExecutable();
              if (path) onChange({ target: path, arguments: binding.settings.arguments });
            }}
          >
            {t("form.openApp.browse")}
          </button>
        </div>
      </label>
      <label className="field">
        {t("form.openApp.arguments")}
        <input type="text" value={str(binding.settings.arguments)} onChange={(e) => onChange({ target: binding.settings.target, arguments: e.target.value })} />
      </label>
    </div>
  );
}

function OpenUrlActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const url = str(binding.settings.url);
  const looksValid = url === "" || /^https?:\/\/.+/i.test(url);
  return (
    <label className="field">
      {t("form.openUrl.label")}
      <input
        type="url"
        value={url}
        onChange={(e) => onChange({ url: e.target.value })}
        placeholder="https://example.com/..."
        style={!looksValid ? { borderColor: "var(--ms-danger)" } : undefined}
      />
      {!looksValid && <span style={{ color: "var(--ms-danger)", fontSize: 11 }}>{t("form.openUrl.invalid")}</span>}
    </label>
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
    <label className="field">
      {t("form.delay.label")}
      <input
        type="number"
        min={0}
        max={60}
        step={0.1}
        value={text}
        onChange={(e) => {
          setText(e.target.value);
          const seconds = Number(e.target.value);
          if (e.target.value !== "" && Number.isFinite(seconds)) onChange({ ms: Math.round(seconds * 1000) });
        }}
      />
    </label>
  );
}

function StopForm() {
  const { t } = useT();
  return <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("action.logic.stopNote")}</p>;
}

/** No settings to configure — the action reads the widget's live dragged value instead (core.setVolume). */
function NoSettingsForm() {
  const { t } = useT();
  return <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: 0 }}>{t("form.noSettings")}</p>;
}

function SetMuteActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const muted = binding.settings.muted !== false;
  return (
    <label className="field">
      {t("form.setMute.label")}
      <select value={muted ? "mute" : "unmute"} onChange={(e) => onChange({ muted: e.target.value === "mute" })}>
        <option value="mute">{t("form.setMute.mute")}</option>
        <option value="unmute">{t("form.setMute.unmute")}</option>
      </select>
    </label>
  );
}

/** The ways a Set variable action can change a variable of each type (the server refuses the others). */
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

function SetVariableForm({ binding, onChange, variableCatalog }: ActionFormProps) {
  const { t } = useT();
  const own = (variableCatalog ?? []).filter((v) => v.name.startsWith("user."));
  const name = str(binding.settings.variable);
  const current = own.find((v) => v.name === name);
  const modes = setVariableModes(current?.type);
  const mode = modes.includes(str(binding.settings.mode, "set")) ? str(binding.settings.mode, "set") : "set";
  const valueField = { key: "value", label: t("form.setVariable.value"), kind: "Text", allowVariables: true } as SettingField;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <label className="field">
        {t("form.setVariable.variable")}
        <select value={name} onChange={(e) => onChange({ variable: e.target.value, mode: "set" })}>
          <option value="">{t("form.setVariable.choose")}</option>
          {name && !current && <option value={name}>{name} {t("form.setVariable.missing")}</option>}
          {own.map((v) => <option key={v.name} value={v.name}>{v.name}</option>)}
        </select>
      </label>
      {own.length === 0 && <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("form.setVariable.none")}</div>}
      <label className="field">
        {t("form.setVariable.mode")}
        <select value={mode} onChange={(e) => onChange({ mode: e.target.value })}>
          {modes.map((m) => <option key={m} value={m}>{t(SET_VARIABLE_MODE_KEYS[m as keyof typeof SET_VARIABLE_MODE_KEYS])}</option>)}
        </select>
      </label>
      {mode === "set" && (
        <SchemaForm
          fields={[valueField]}
          values={binding.settings as Record<string, unknown>}
          onChange={(values) => onChange({ value: values.value })}
          fetchOptions={() => Promise.resolve({ options: [] } as unknown as OptionsResult)}
          variableCatalog={variableCatalog}
        />
      )}
      {mode === "add" && (
        <label className="field">
          {t("form.setVariable.amount")}
          <input type="number" step="any" value={num(binding.settings.amount, 1)} onChange={(e) => onChange({ amount: Number(e.target.value) })} />
        </label>
      )}
    </div>
  );
}

/** Raw JSON fallback for action types the editor doesn't have a dedicated form for yet (future plugins). */
function GenericJsonForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  const text = JSON.stringify(binding.settings, null, 2);
  return (
    <label className="field">
      {t("form.json.label")}
      <textarea
        rows={4}
        style={{ fontFamily: "ui-monospace, monospace", fontSize: 12 }}
        defaultValue={text}
        onBlur={(e) => {
          try {
            onChange(JSON.parse(e.target.value || "{}"));
          } catch {
            // Leave the last valid settings in place rather than corrupting them on invalid JSON.
          }
        }}
      />
    </label>
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
