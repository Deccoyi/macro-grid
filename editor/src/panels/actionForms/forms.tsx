import type { ActionBinding, Page } from "@macro/renderer";
import { api } from "../../api/client";
import type { ProfileSummary } from "../../api/types";
import { HotkeyCapture } from "./HotkeyCapture";

export interface ActionFormProps {
  binding: ActionBinding;
  onChange: (settings: Record<string, unknown>) => void;
  pages: Page[];
  profiles: ProfileSummary[];
}

const str = (v: unknown, fallback = "") => (typeof v === "string" ? v : fallback);
const num = (v: unknown, fallback = 0) => (typeof v === "number" ? v : fallback);

export function HotkeyForm({ binding, onChange }: ActionFormProps) {
  return (
    <label className="field">
      Kısayol
      <HotkeyCapture value={str(binding.settings.keys)} onChange={(keys) => onChange({ keys })} />
    </label>
  );
}

export function TypeTextForm({ binding, onChange }: ActionFormProps) {
  return (
    <label className="field">
      Yazılacak metin
      <textarea rows={2} value={str(binding.settings.text)} onChange={(e) => onChange({ text: e.target.value })} />
    </label>
  );
}

export function PageActionForm({ binding, onChange, pages }: ActionFormProps) {
  const mode = str(binding.settings.mode, "goto");
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label className="field">
        Mod
        <select value={mode} onChange={(e) => onChange({ mode: e.target.value, pageId: binding.settings.pageId })}>
          <option value="goto">Belirli sayfaya git</option>
          <option value="next">Sonraki sayfa</option>
          <option value="prev">Önceki sayfa</option>
          <option value="back">Geri</option>
        </select>
      </label>
      {mode === "goto" && (
        <label className="field">
          Sayfa
          <select value={str(binding.settings.pageId)} onChange={(e) => onChange({ mode, pageId: e.target.value })}>
            <option value="">— seçin —</option>
            {pages.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </label>
      )}
    </div>
  );
}

export function ProfileActionForm({ binding, onChange, profiles }: ActionFormProps) {
  return (
    <label className="field">
      Profil
      <select value={str(binding.settings.profileId)} onChange={(e) => onChange({ profileId: e.target.value })}>
        <option value="">— seçin —</option>
        {profiles.map((p) => (
          <option key={p.id} value={p.id}>{p.name}</option>
        ))}
      </select>
    </label>
  );
}

export function OpenApplicationForm({ binding, onChange }: ActionFormProps) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label className="field">
        Uygulama
        <div style={{ display: "flex", gap: 6 }}>
          <input
            type="text"
            readOnly
            value={str(binding.settings.target)}
            placeholder="Gözat'a tıklayın"
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
            Gözat…
          </button>
        </div>
      </label>
      <label className="field">
        Argümanlar (opsiyonel)
        <input type="text" value={str(binding.settings.arguments)} onChange={(e) => onChange({ target: binding.settings.target, arguments: e.target.value })} />
      </label>
    </div>
  );
}

export function OpenUrlActionForm({ binding, onChange }: ActionFormProps) {
  const url = str(binding.settings.url);
  const looksValid = url === "" || /^https?:\/\/.+/i.test(url);
  return (
    <label className="field">
      URL (varsayılan tarayıcıda açılır)
      <input
        type="url"
        value={url}
        onChange={(e) => onChange({ url: e.target.value })}
        placeholder="https://twitch.tv/..."
        style={!looksValid ? { borderColor: "var(--ms-danger)" } : undefined}
      />
      {!looksValid && <span style={{ color: "var(--ms-danger)", fontSize: 11 }}>http:// veya https:// ile başlamalı.</span>}
    </label>
  );
}

export function DelayActionForm({ binding, onChange }: ActionFormProps) {
  return (
    <label className="field">
      Gecikme (ms, en fazla 60000)
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

/** Raw JSON fallback for action types the editor doesn't have a dedicated form for yet (future plugins). */
export function GenericJsonForm({ binding, onChange }: ActionFormProps) {
  const text = JSON.stringify(binding.settings, null, 2);
  return (
    <label className="field">
      Ayarlar (JSON)
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

export const ACTION_FORMS: Record<string, (props: ActionFormProps) => JSX.Element> = {
  "core.hotkey": HotkeyForm,
  "core.typeText": TypeTextForm,
  "core.page": PageActionForm,
  "core.profile": ProfileActionForm,
  "core.open": OpenApplicationForm,
  "core.openUrl": OpenUrlActionForm,
  "core.delay": DelayActionForm,
};

export function formFor(type: string): (props: ActionFormProps) => JSX.Element {
  return ACTION_FORMS[type] ?? GenericJsonForm;
}
