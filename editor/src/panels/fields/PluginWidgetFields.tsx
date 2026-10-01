import { Eraser, Puzzle, ShieldAlert, TriangleAlert } from "lucide-react";
import { effectiveOptions, type Widget } from "@macro/renderer";
import type { OptionsResult, VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { turnOn } from "../../grid/widgetCrashGuard";
import { editorWidgetData } from "../../grid/widgetData";
import { useOffPlugins } from "../../state/useOffPlugins";
import { usePluginWidgets } from "../../state/usePluginWidgets";
import { SchemaForm } from "../actionForms/SchemaForm";
import type { FieldGroupProps } from "./AppearanceFields";
import { Field, SectionLabel, Switch } from "./controls";

const NO_OPTIONS: OptionsResult = { options: [] };

/** The options a person can switch per widget, with their texts. */
const OPTION_TEXTS = {
  keepLoaded: { label: "pluginWidget.option.keepLoaded", hint: "pluginWidget.option.keepLoaded.hint" },
  storage: { label: "pluginWidget.option.storage", hint: "pluginWidget.option.storage.hint" },
} as const;

/** A short notice: an icon and one line. A warning is yellow, plain information uses the accent. */
function Note({ tone, children }: { tone: "warning" | "info"; children: React.ReactNode }) {
  const Icon = tone === "warning" ? TriangleAlert : ShieldAlert;
  return (
    <div role="note" className={tone === "info" ? "pf-note info" : "pf-note"}>
      <Icon size={14} />
      <div className="pf-body">{children}</div>
    </div>
  );
}

/** What the widget is (its name, the plugin under it) and the notices that matter now: at most two at a time, the most urgent first. Sits in the
 * identity section, right after the widget's own name. */
export function PluginWidgetIdentity({ widget }: { widget: Widget }) {
  const { t } = useT();
  const available = usePluginWidgets();
  const off = useOffPlugins();
  const pluginId = typeof widget.props?.plugin === "string" ? widget.props.plugin : "";
  const widgetId = typeof widget.props?.widget === "string" ? widget.props.widget : "";
  const info = available.find((w) => w.plugin === pluginId && w.widget === widgetId);

  const notices: React.ReactNode[] = [];
  if (!info) notices.push(<Note key="unavailable" tone="warning">{t("pluginWidget.unavailable")}</Note>);
  if (off.has(pluginId)) {
    notices.push(
      <Note key="off" tone="warning">
        <span>{t("pluginWidget.crashedOff")}</span>
        <button type="button" className="ghost" onClick={() => turnOn(pluginId)}>{t("pluginWidget.turnOn")}</button>
      </Note>,
    );
  }
  if (info && !info.verified) notices.push(<Note key="unverified" tone="info">{t("palette.unverifiedHint")}</Note>);

  return (
    <>
      <div className="pf-identity">
        {info?.icon ? (
          <span aria-hidden className="pf-plugin-icon" style={{ WebkitMask: `url("${info.icon}") center / contain no-repeat`, mask: `url("${info.icon}") center / contain no-repeat` }} />
        ) : (
          <Puzzle size={20} strokeWidth={1.75} color="var(--ms-text-secondary)" />
        )}
        <div className="pf-identity-text">
          <span className="pf-identity-name">{info ? info.name : widgetId}</span>
          <span className="pf-hint">{info ? info.pluginName : pluginId}</span>
        </div>
      </div>
      {notices.slice(0, 2)}
    </>
  );
}

/**
 * For a `plugin-widget`, after the identity section: the options the plugin declared (a switch each, off or on for this widget), the widget's own
 * settings drawn from the manifest's schema (a `Variable` field is the person's binding, a `Color` field the color picker) and, for a widget that
 * keeps data, a button that clears it.
 */
export function PluginWidgetFields({ widget, onChange, variableCatalog }: FieldGroupProps & { variableCatalog: VariableInfo[] }) {
  const { t } = useT();
  const available = usePluginWidgets();
  const pluginId = typeof widget.props?.plugin === "string" ? widget.props.plugin : "";
  const widgetId = typeof widget.props?.widget === "string" ? widget.props.widget : "";
  const info = available.find((w) => w.plugin === pluginId && w.widget === widgetId);
  const settings = (widget.props?.settings as Record<string, unknown> | undefined) ?? {};
  const enabledOptions = effectiveOptions(info, widget.props);
  const switchable = (info?.options ?? []).filter((name): name is keyof typeof OPTION_TEXTS => name in OPTION_TEXTS);
  const hasSettings = (info?.settings?.length ?? 0) > 0;

  return (
    <>
      {switchable.length > 0 && (
        <>
          <SectionLabel>{t("pluginWidget.section.options")}</SectionLabel>
          {switchable.map((name) => (
            <Field
              key={name}
              inline
              label={t(OPTION_TEXTS[name].label)}
              hint={t(OPTION_TEXTS[name].hint)}
            >
              <Switch
                checked={enabledOptions.includes(name)}
                onChange={(v) =>
                  onChange((w) => {
                    w.props = { ...w.props, options: { ...((w.props?.options as Record<string, boolean> | undefined) ?? {}), [name]: v } };
                  })
                }
              />
            </Field>
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
          <button type="button" className="ghost pf-btn small" onClick={() => editorWidgetData.clear(widget.id)}>
            <Eraser size={13} strokeWidth={1.75} />
            {t("pluginWidget.clearData")}
          </button>
        </>
      )}
    </>
  );
}
