import { Eraser, Puzzle, ShieldAlert, TriangleAlert } from "lucide-react";
import { effectiveOptions } from "@macro/renderer";
import type { OptionsResult, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { turnOn } from "../../grid/widgetCrashGuard";
import { editorWidgetData } from "../../grid/widgetData";
import { useOffPlugins } from "../../state/useOffPlugins";
import { usePluginWidgets } from "../../state/usePluginWidgets";
import { SchemaForm } from "../actionForms/SchemaForm";
import type { FieldGroupProps } from "./AppearanceFields";
import { SectionLabel } from "./controls";

const NO_OPTIONS: OptionsResult = { options: [] };

/** The options a person can switch per widget, with their texts. */
const OPTION_TEXTS = {
  keepLoaded: { label: "pluginWidget.option.keepLoaded", hint: "pluginWidget.option.keepLoaded.hint" },
  storage: { label: "pluginWidget.option.storage", hint: "pluginWidget.option.storage.hint" },
} as const;

/** A short notice above the fields: an icon and one line, flat, like a message in a code editor's panel. */
function Note({ tone, children }: { tone: "warning" | "info"; children: React.ReactNode }) {
  const Icon = tone === "warning" ? TriangleAlert : ShieldAlert;
  return (
    <div role="note" style={{ display: "flex", gap: 6, alignItems: "flex-start", fontSize: 11.5, lineHeight: 1.4, color: "var(--ms-warning, #facc15)" }}>
      <Icon size={13} strokeWidth={2} style={{ flexShrink: 0, marginTop: 1 }} />
      <div style={{ display: "flex", flexDirection: "column", gap: 6, alignItems: "flex-start" }}>{children}</div>
    </div>
  );
}

/**
 * For a `plugin-widget`, in the same order as every other widget: what it is (the widget's name, the plugin under it), the notices that matter
 * now, the options the plugin declared (checkboxes, off or on for this widget), the widget's own settings drawn from the manifest's schema (a `Variable`
 * field is the person's binding, a `Color` field the color picker) and, for a widget that keeps data, a button that clears it.
 */
export function PluginWidgetFields({ widget, onChange, variableCatalog }: FieldGroupProps & { variableCatalog: VariableInfo[] }) {
  const { t } = useT();
  const available = usePluginWidgets();
  const off = useOffPlugins();
  const pluginId = typeof widget.props?.plugin === "string" ? widget.props.plugin : "";
  const widgetId = typeof widget.props?.widget === "string" ? widget.props.widget : "";
  const info = available.find((w) => w.plugin === pluginId && w.widget === widgetId);
  const settings = (widget.props?.settings as Record<string, unknown> | undefined) ?? {};
  const enabledOptions = effectiveOptions(info, widget.props);
  const switchable = (info?.options ?? []).filter((name): name is keyof typeof OPTION_TEXTS => name in OPTION_TEXTS);
  const hasSettings = (info?.settings?.length ?? 0) > 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}>
        {info?.icon ? (
          <span aria-hidden style={{ width: 16, height: 16, flexShrink: 0, background: "var(--ms-text-secondary)", WebkitMask: `url("${info.icon}") center / contain no-repeat`, mask: `url("${info.icon}") center / contain no-repeat` }} />
        ) : (
          <Puzzle size={16} strokeWidth={1.75} color="var(--ms-text-secondary)" style={{ flexShrink: 0 }} />
        )}
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0, lineHeight: 1.3 }}>
          <span style={{ fontSize: 12.5, fontWeight: 600, color: "var(--ms-text-primary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{info ? info.name : widgetId}</span>
          <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{info ? info.pluginName : pluginId}</span>
        </div>
      </div>

      {!info && <Note tone="warning">{t("pluginWidget.unavailable")}</Note>}
      {off.has(pluginId) && (
        <Note tone="warning">
          <span>{t("pluginWidget.crashedOff")}</span>
          <button type="button" className="ghost" onClick={() => turnOn(pluginId)}>{t("pluginWidget.turnOn")}</button>
        </Note>
      )}
      {info && !info.verified && <Note tone="info">{t("palette.unverifiedHint")}</Note>}

      {switchable.length > 0 && (
        <>
          <SectionLabel>{t("pluginWidget.section.options")}</SectionLabel>
          {switchable.map((name) => (
            <label key={name} style={{ display: "flex", flexDirection: "column", gap: 2 }}>
              <span style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-primary)" }}>
                <input
                  type="checkbox"
                  checked={enabledOptions.includes(name)}
                  onChange={(e) =>
                    onChange((w) => {
                      w.props = { ...w.props, options: { ...((w.props?.options as Record<string, boolean> | undefined) ?? {}), [name]: e.target.checked } };
                    })
                  }
                />
                {t(OPTION_TEXTS[name].label)}
              </span>
              <span style={{ marginLeft: 22, fontSize: 11, lineHeight: 1.4, color: "var(--ms-text-secondary)" }}>{t(OPTION_TEXTS[name].hint)}</span>
            </label>
          ))}
        </>
      )}

      {hasSettings && (
        <>
          <SectionLabel>{t("pluginWidget.settings")}</SectionLabel>
          <SchemaForm
            fields={info!.settings!}
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

      {info?.options?.includes("storage") && (
        <>
          <SectionLabel>{t("pluginWidget.section.data")}</SectionLabel>
          <button type="button" className="ghost" onClick={() => editorWidgetData.clear(widget.id)} style={{ alignSelf: "flex-start", display: "flex", alignItems: "center", gap: 6 }}>
            <Eraser size={13} strokeWidth={1.75} />
            {t("pluginWidget.clearData")}
          </button>
        </>
      )}
    </div>
  );
}
