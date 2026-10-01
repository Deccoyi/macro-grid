import type { ActionBinding, ConditionNode, Profile, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, PluginWidgetInfo, SettingField } from "../api/types";
import { FLOW_ELSE, FLOW_END, FLOW_IF, MAX_DEPTH, isWellFormed } from "../panels/actionFlow";
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
const SET_VARIABLE_ACTION = "core.setVariable";
const SELF_PREFIX = "self.";
const LOGIC_TYPES = new Set([FLOW_IF, FLOW_ELSE, FLOW_END]);

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

export function conditionVariables(node: ConditionNode, into: string[]) {
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
  // A "user." name that is not in the Global Variable List gets its own, more helpful message than a variable some plugin does not provide.
  const missingVariable = (v: string, id: string, page: string, widgetName: string, target: Diagnostic["target"]) => {
    const own = v.toLowerCase().startsWith("user.");
    const code = own ? "W231" : "W230";
    add({ id: `${code}:${id}:${v}`, severity: "warning", code, messageKey: own ? "diag.check.W231" : "diag.check.W230", messageArgs: [page, widgetName, v], target });
  };

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

      checkVariables(widget, page.name, name, base, known, missingVariable);
      const sliderVariable = widget.props?.valueVariable;
      if (typeof sliderVariable === "string" && sliderVariable.toLowerCase().startsWith(SELF_PREFIX)) {
        add({ id: `W232:${widget.id}:slider:${sliderVariable}`, severity: "warning", code: "W232", messageKey: "diag.check.W232", messageArgs: [page.name, name, sliderVariable], target: base });
      }

      for (const [event, bindings] of Object.entries(widget.actions) as [WidgetEventName, ActionBinding[] | undefined][]) {
        const firstLogic = (bindings ?? []).findIndex((b) => LOGIC_TYPES.has(b.type));
        if (firstLogic >= 0 && !isWellFormed(bindings ?? [])) {
          add({ id: `W233:${widget.id}:${event}`, severity: "warning", code: "W233", messageKey: "diag.check.W233", messageArgs: [page.name, name, eventLabel(event), String(MAX_DEPTH)], target: { ...base, event, actionIndex: firstLogic } });
        }
        (bindings ?? []).forEach((binding, index) => {
          const target = { ...base, event, actionIndex: index };
          const args = [page.name, name, eventLabel(event), String(index + 1)];
          const info = actionByType.get(binding.type.toLowerCase());
          if (!info) {
            add({ id: `E210:${widget.id}:${event}:${index}`, severity: "error", code: "E210", messageKey: "diag.check.E210", messageArgs: [...args, binding.type], target });
            return;
          }

          const s = binding.settings ?? {};
          if (info.type === FLOW_IF && s.when !== "previousFailed" && s.when !== "previousOk") {
            const names: string[] = [];
            if (s.condition) conditionVariables(s.condition as ConditionNode, names);
            if (names.length === 0) add({ id: `W234:${widget.id}:${event}:${index}`, severity: "warning", code: "W234", messageKey: "diag.check.W234", messageArgs: args, target });
            for (const v of new Set(names)) {
              if (v.toLowerCase().startsWith(SELF_PREFIX)) {
                add({ id: `W232:${widget.id}:${event}:${index}:${v}`, severity: "warning", code: "W232", messageKey: "diag.check.W232", messageArgs: [page.name, name, v], target });
              } else if (!known(v)) missingVariable(v, `${widget.id}:${event}:${index}`, page.name, name, target);
            }
          }
          const missing = (code: string, what: string) =>
            add({ id: `E220:${widget.id}:${event}:${index}:${code}`, severity: "error", code: "E220", messageKey: "diag.check.E220", messageArgs: [...args, what], target });
          if (info.type === PAGE_ACTION && (s.mode ?? "goto") === "goto" && typeof s.pageId === "string" && s.pageId && !pageIds.has(s.pageId)) missing("page", "page");
          if (info.type === PROFILE_ACTION && typeof s.profileId === "string" && s.profileId && !catalogs.profileIds.has(s.profileId)) missing("profile", "profile");
          if (info.type === WEB_ACTION && typeof s.widgetId === "string" && s.widgetId && !webWidgetIds.has(s.widgetId)) missing("web", "web widget");
          if (info.type === SET_VARIABLE_ACTION && typeof s.variable === "string" && s.variable && !known(s.variable)) missing("variable", "variable");

          for (const field of info.fields ?? []) {
            const problem = invalidSetting(field, s[field.key]);
            if (problem) {
              add({ id: `W221:${widget.id}:${event}:${index}:${field.key}`, severity: "warning", code: "W221", messageKey: "diag.check.W221", messageArgs: [...args, field.label, problem], target });
            }
            if (field.allowVariables && typeof s[field.key] === "string") {
              for (const v of templateVariables(s[field.key] as string)) {
                if (v.toLowerCase().startsWith(SELF_PREFIX)) {
                  add({ id: `W232:${widget.id}:${event}:${index}:${v}`, severity: "warning", code: "W232", messageKey: "diag.check.W232", messageArgs: [page.name, name, v], target });
                } else if (!known(v)) missingVariable(v, `${widget.id}:${event}:${index}`, page.name, name, target);
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
  known: (name: string) => boolean, missingVariable: (v: string, id: string, page: string, widgetName: string, target: Diagnostic["target"]) => void,
) {
  const used = new Set<string>(templateVariables(widget.text));
  for (const binding of Object.values(widget.dynamic ?? {})) {
    const names: string[] = [];
    for (const c of binding.cases) conditionVariables(c.condition, names);
    names.forEach((n) => used.add(n));
  }
  for (const v of used) {
    if (!known(v)) missingVariable(v, widget.id, pageName, name, base);
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
