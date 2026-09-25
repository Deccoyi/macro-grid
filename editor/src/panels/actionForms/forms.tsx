import type { JSX } from "react";
import type { ActionBinding, Page } from "@macro/renderer";
import { api } from "../../api/client";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
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

function DelayActionForm({ binding, onChange }: ActionFormProps) {
  const { t } = useT();
  return (
    <label className="field">
      {t("form.delay.label")}
      <input
        type="number"
        min={0}
        max={60000}
        value={num(binding.settings.ms)}
        onChange={(e) => onChange({ ms: Number(e.target.value) })}
      />
    </label>
  );
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
  "core.open": OpenApplicationForm,
  "core.openUrl": OpenUrlActionForm,
  "core.delay": DelayActionForm,
  "core.setVolume": NoSettingsForm,
  "core.toggleMute": NoSettingsForm,
  "core.setMute": SetMuteActionForm,
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
