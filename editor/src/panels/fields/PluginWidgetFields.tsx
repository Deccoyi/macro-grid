import { ShieldAlert } from "lucide-react";
import type { OptionsResult, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { turnOn } from "../../grid/widgetCrashGuard";
import { useOffPlugins } from "../../state/useOffPlugins";
import { usePluginWidgets } from "../../state/usePluginWidgets";
import { SchemaForm } from "../actionForms/SchemaForm";
import type { FieldGroupProps } from "./AppearanceFields";

const NO_OPTIONS: OptionsResult = { options: [] };

/**
 * For a `plugin-widget`: which plugin's widget it is, the "Unverified" note for a plugin that is not verified, and the widget's own settings drawn from
 * the manifest's schema (a `Variable` field is the person's binding: the only outside variable the widget may read).
 */
export function PluginWidgetFields({ widget, onChange, variableCatalog }: FieldGroupProps & { variableCatalog: VariableInfo[] }) {
  const { t } = useT();
  const available = usePluginWidgets();
  const off = useOffPlugins();
  const pluginId = typeof widget.props?.plugin === "string" ? widget.props.plugin : "";
  const widgetId = typeof widget.props?.widget === "string" ? widget.props.widget : "";
  const info = available.find((w) => w.plugin === pluginId && w.widget === widgetId);
  const settings = (widget.props?.settings as Record<string, unknown> | undefined) ?? {};

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>
        {info ? `${info.pluginName} › ${info.name}` : `${pluginId} › ${widgetId}`}
      </div>
      {!info && <div style={{ fontSize: 11.5, color: "var(--ms-warning, #facc15)", lineHeight: 1.4 }}>{t("pluginWidget.unavailable")}</div>}
      {off.has(pluginId) && (
        <div role="note" style={{ display: "flex", flexDirection: "column", gap: 6, fontSize: 11.5, lineHeight: 1.4, color: "var(--ms-warning, #facc15)" }}>
          <span>{t("pluginWidget.crashedOff")}</span>
          <button type="button" onClick={() => turnOn(pluginId)} style={{ alignSelf: "flex-start" }}>{t("pluginWidget.turnOn")}</button>
        </div>
      )}
      {info && !info.verified && (
        <div role="note" style={{ display: "flex", gap: 6, alignItems: "flex-start", fontSize: 11.5, lineHeight: 1.4, color: "var(--ms-warning, #facc15)" }}>
          <ShieldAlert size={14} strokeWidth={2} style={{ flexShrink: 0, marginTop: 1 }} />
          <span>{t("palette.unverifiedHint")}</span>
        </div>
      )}
      {info?.settings && info.settings.length > 0 && (
        <>
          <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>{t("pluginWidget.settings")}</div>
          <SchemaForm
            fields={info.settings}
            values={settings}
            variableCatalog={variableCatalog}
            fetchOptions={async () => NO_OPTIONS}
            onChange={(values) =>
              onChange((w) => {
                w.props = { ...w.props, settings: values };
              })
            }
          />
        </>
      )}
    </div>
  );
}
