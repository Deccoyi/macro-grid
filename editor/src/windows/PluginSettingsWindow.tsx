import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { SettingField, StatusEntry } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { SchemaForm } from "../panels/actionForms/SchemaForm";

/**
 * The whole page of a plugin's own settings tool window (see ToolWindow.cs's `plugin-settings-{id}` kind
 * and ServerApp.cs's `POST /api/windows/plugin-settings/{id}`) — a schema-driven form drawn from the
 * plugin's registered IPluginSettingsPage, plus its live status item in the header if it has one
 * (clicking the status chip in StatusBar.tsx is what opens this window in the first place).
 */
export function PluginSettingsWindow({ id }: { id: string }) {
  const { t } = useT();
  const [fields, setFields] = useState<SettingField[] | null>(null);
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [status, setStatus] = useState<StatusEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([api.getPluginSettingsSchema(id), api.getPluginSettings(id)])
      .then(([schema, current]) => {
        setFields(schema);
        setValues(current);
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false));
    api.getStatus().then(setStatus).catch(() => {});
  }, [id]);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.savePluginSettings(id, values);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  const ownStatus = status.filter((s) => s.pluginId === id);

  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column", background: "var(--ms-bg-canvas)", color: "var(--ms-text-primary)" }}>
      {ownStatus.length > 0 && (
        <div style={{ display: "flex", gap: 10, padding: "8px 16px", borderBottom: "1px solid var(--ms-border)", fontSize: 11.5, color: "var(--ms-text-secondary)" }}>
          {ownStatus.map((s) => <span key={s.id}>{s.text}</span>)}
        </div>
      )}

      <div style={{ padding: 16, overflowY: "auto", flex: 1 }}>
        {loading && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pairing.loading")}</div>}

        {!loading && (!fields || fields.length === 0) && (
          <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pluginSettings.none")}</div>
        )}

        {!loading && fields && fields.length > 0 && (
          <div style={{ maxWidth: 420 }}>
            <SchemaForm
              fields={fields}
              values={values}
              onChange={(next) => { setValues(next); setSaved(false); }}
              fetchOptions={(sourceId, current) => api.getPluginSettingsOptions(id, sourceId, current)}
            />

            <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 14 }}>
              <button type="button" onClick={save} disabled={saving}>{t("pluginSettings.save")}</button>
              {saved && <span style={{ fontSize: 11.5, color: "var(--ms-success, #4ade80)" }}>{t("pluginSettings.saved")}</span>}
              {error && <span style={{ fontSize: 11.5, color: "var(--ms-danger)" }}>{error}</span>}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
