import { useEffect, useState } from "react";
import { api } from "../api/client";
import type { SettingField } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { PluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { SchemaForm } from "./actionForms/SchemaForm";

/** Properties tool window content for whatever is selected in the Plugins tool window (see
 * docs/design/plugins-tool-window.md). A plugin row with its own IPluginSettingsPage, or a leaf tree item
 * with IPluginTreeItemSettings, gets the same schema-driven form the standalone plugin settings window
 * uses (see windows/PluginSettingsWindow.tsx) — reused here rather than rebuilt. A plugin row without
 * settings, or a non-leaf tree node, shows only its name. */
export function PluginTreeItemProperties({ selection }: { selection: PluginTreeSelection }) {
  const { t } = useT();
  const [fields, setFields] = useState<SettingField[] | null>(null);
  const [values, setValues] = useState<Record<string, unknown>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const hasSettings = selection.hasSettings;
  const title = selection.kind === "plugin" ? selection.pluginName : selection.label;

  useEffect(() => {
    setLoading(true);
    setSaved(false);
    setError(null);
    if (!hasSettings) {
      setFields(null);
      setLoading(false);
      return;
    }
    const schema = selection.kind === "plugin"
      ? api.getPluginSettingsSchema(selection.pluginId)
      : api.getPluginTreeItemSettingsSchema(selection.pluginId, selection.itemId);
    const current = selection.kind === "plugin"
      ? api.getPluginSettings(selection.pluginId)
      : api.getPluginTreeItemSettings(selection.pluginId, selection.itemId);
    Promise.all([schema, current])
      .then(([f, v]) => { setFields(f); setValues(v); })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selection.kind, selection.pluginId, selection.kind === "item" ? selection.itemId : null, hasSettings]);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      if (selection.kind === "plugin") await api.savePluginSettings(selection.pluginId, values);
      else await api.savePluginTreeItemSettings(selection.pluginId, selection.itemId, values);
      setSaved(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column", overflowY: "auto", padding: 12 }}>
      <div style={{ fontSize: 12.5, fontWeight: 600, color: "var(--ms-text-primary)", marginBottom: 10 }}>{title}</div>

      {!hasSettings && (
        <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pluginsTree.noSettings")}</div>
      )}

      {hasSettings && loading && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pairing.loading")}</div>}

      {hasSettings && !loading && fields && fields.length > 0 && (
        <>
          <SchemaForm
            fields={fields}
            values={values}
            onChange={(next) => { setValues(next); setSaved(false); }}
            fetchOptions={(sourceId, current) =>
              selection.kind === "plugin"
                ? api.getPluginSettingsOptions(selection.pluginId, sourceId, current)
                : api.getPluginTreeItemSettingsOptions(selection.pluginId, sourceId, current)}
            runCommand={selection.kind === "plugin"
              ? (command, current) => api.runPluginSettingsCommand(selection.pluginId, command, current)
              : undefined}
          />
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 14 }}>
            <button type="button" onClick={save} disabled={saving}>{t("pluginSettings.save")}</button>
            {saved && <span style={{ fontSize: 11.5, color: "var(--ms-success, #4ade80)" }}>{t("pluginSettings.saved")}</span>}
            {error && <span style={{ fontSize: 11.5, color: "var(--ms-danger)" }}>{error}</span>}
          </div>
        </>
      )}

      {hasSettings && !loading && (!fields || fields.length === 0) && !error && (
        <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pluginSettings.none")}</div>
      )}

      {error && !fields && (
        <div style={{ fontSize: 11.5, color: "var(--ms-danger)", marginTop: 8 }}>{error}</div>
      )}
    </div>
  );
}
