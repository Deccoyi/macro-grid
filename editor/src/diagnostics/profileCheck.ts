import type { ActionBinding, ConditionNode, Profile, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, PluginWidgetInfo, SettingField } from "../api/types";
import type { Diagnostic } from "./types";

/** The source id of the lines the profile check makes; every run replaces the earlier ones. */
export const PROFILE_CHECK_SOURCE = "profile-check";

export interface CheckCatalogs {
  /** The actions the server offers right now. */
  actions: ActionInfo[];
  /** Names of the variables the server describes, and of those that have a value right now. */
  variableNames: ReadonlySet<string>;
  liveVariableNames: ReadonlySet<string>;
  pluginWidgets: PluginWidgetInfo[];
  /** Ids of every profile (for "Switch profile"). */
  profileIds: ReadonlySet<string>;
}

const PAGE_ACTION = "core.page";
const PROFILE_ACTION = "core.profile";
const WEB_ACTION = "core.web";

/** A `{name}` / `{name|format|placeholder}` token in a text; `{{` and `}}` are literal braces. */
export function templateVariables(text: string | undefined): string[] {
  if (!text || !text.includes("{")) return [];
  const names: string[] = [];
  for (const m of text.matchAll(/\{\{|\}\}|\{([^{}|]+)(?:\|[^{}|]*)?(?:\|[^}]*)?\}/g)) {
    const name = m[1]?.trim();
    if (name) names.push(name);
  }
  return names;
}

function conditionVariables(node: ConditionNode, into: string[]) {
  if (node.kind === "compare") {
    if (node.variable) into.push(node.variable);
    return;
  }
  for (const child of node.children ?? []) conditionVariables(child, into);
}

/**
 * Looks for what is wrong inside a profile without running it: an action whose plugin is gone, a plugin widget that is not installed, a jump to a
 * page, profile or web widget that does not exist, a setting that is not valid, a variable nobody provides. Pure: the caller passes what the server
 * offers right now. `eventLabel` turns an event name into its translated text. One line per finding, with a stable id.
 */
export function checkProfile(profile: Profile, catalogs: CheckCatalogs, eventLabel: (event: string) => string): Diagnostic[] {
  const list: Diagnostic[] = [];
  const pageIds = new Set(profile.pages.map((p) => p.id));
  const webWidgetIds = new Set(profile.pages.flatMap((p) => p.widgets).filter((w) => w.type === "web").map((w) => w.id));
  const actionByType = new Map(catalogs.actions.map((a) => [a.type.toLowerCase(), a]));
  const known = (name: string) => catalogs.variableNames.has(name) || catalogs.liveVariableNames.has(name);

  const add = (d: Omit<Diagnostic, "source" | "origin">) => list.push({ ...d, source: PROFILE_CHECK_SOURCE });

  for (const page of profile.pages) {
    for (const widget of page.widgets) {
      const name = widget.name ?? widget.id;
      const base = { profileId: profile.id, pageId: page.id, widgetId: widget.id };

      if (widget.type === "plugin-widget") {
        const plugin = typeof widget.props?.plugin === "string" ? widget.props.plugin : "";
        const kind = typeof widget.props?.widget === "string" ? widget.props.widget : "";
        if (!catalogs.pluginWidgets.some((w) => w.plugin === plugin && w.widget === kind)) {
          add({ id: `E211:${widget.id}`, severity: "error", code: "E211", messageKey: "diag.check.E211", messageArgs: [page.name, name, plugin || "?"], target: base });
        }
      }

      checkVariables(widget, page.name, name, base, known, add);

      for (const [event, bindings] of Object.entries(widget.actions) as [WidgetEventName, ActionBinding[] | undefined][]) {
        (bindings ?? []).forEach((binding, index) => {
          const target = { ...base, event, actionIndex: index };
          const args = [page.name, name, eventLabel(event), String(index + 1)];
          const info = actionByType.get(binding.type.toLowerCase());
          if (!info) {
            add({ id: `E210:${widget.id}:${event}:${index}`, severity: "error", code: "E210", messageKey: "diag.check.E210", messageArgs: [...args, binding.type], target });
            return;
          }

          const s = binding.settings ?? {};
          const missing = (code: string, what: string) =>
            add({ id: `E220:${widget.id}:${event}:${index}:${code}`, severity: "error", code: "E220", messageKey: "diag.check.E220", messageArgs: [...args, what], target });
          if (info.type === PAGE_ACTION && (s.mode ?? "goto") === "goto" && typeof s.pageId === "string" && s.pageId && !pageIds.has(s.pageId)) missing("page", "page");
          if (info.type === PROFILE_ACTION && typeof s.profileId === "string" && s.profileId && !catalogs.profileIds.has(s.profileId)) missing("profile", "profile");
          if (info.type === WEB_ACTION && typeof s.widgetId === "string" && s.widgetId && !webWidgetIds.has(s.widgetId)) missing("web", "web widget");

          for (const field of info.fields ?? []) {
            const problem = invalidSetting(field, s[field.key]);
            if (problem) {
              add({ id: `W221:${widget.id}:${event}:${index}:${field.key}`, severity: "warning", code: "W221", messageKey: "diag.check.W221", messageArgs: [...args, field.label, problem], target });
            }
            if (field.allowVariables && typeof s[field.key] === "string") {
              for (const v of templateVariables(s[field.key] as string)) {
                if (!known(v)) add({ id: `W230:${widget.id}:${event}:${index}:${v}`, severity: "warning", code: "W230", messageKey: "diag.check.W230", messageArgs: [page.name, name, v], target });
              }
            }
          }
        });
      }
    }
  }
  return list;
}

function checkVariables(
  widget: Widget, pageName: string, name: string, base: { profileId: string; pageId: string; widgetId: string },
  known: (name: string) => boolean, add: (d: Omit<Diagnostic, "source" | "origin">) => void,
) {
  const used = new Set<string>(templateVariables(widget.text));
  for (const binding of Object.values(widget.dynamic ?? {})) {
    const names: string[] = [];
    for (const c of binding.cases) conditionVariables(c.condition, names);
    names.forEach((n) => used.add(n));
  }
  for (const v of used) {
    if (!known(v)) add({ id: `W230:${widget.id}:${v}`, severity: "warning", code: "W230", messageKey: "diag.check.W230", messageArgs: [pageName, name, v], target: base });
  }
}

/** Why a value does not fit its field, or null. Fields whose options come from the plugin at run time are not checked. */
function invalidSetting(field: SettingField, value: unknown): string | null {
  if (value === undefined || value === null || value === "") return null;
  if ((field.kind === "Select" || field.kind === "Segmented") && !field.optionsSource && field.options && field.options.length > 0) {
    return field.options.some((o) => o.value === String(value)) ? null : String(value);
  }
  if ((field.kind === "Number" || field.kind === "Slider") && typeof value === "number") {
    if (field.min != null && value < field.min) return `${value} < ${field.min}`;
    if (field.max != null && value > field.max) return `${value} > ${field.max}`;
  }
  return null;
}
