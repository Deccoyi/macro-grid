import type { DictKey } from "../i18n/tr";
import { widgetOptionOf } from "./widgetPermission";
import { networkTargetScope, type HttpTargetScope } from "./httpTarget";

/** What the approval screens show for one permission string: a kind (picks the icon) and the sentence. One place, so the install consent and the plugin list never disagree. */
export interface PermissionLabel {
  kind: "variables" | "actions" | "input" | "storage" | "notify" | "widget" | "http" | "unknown";
  /** The sentence for the plugin list. */
  text: string;
  /** The longer sentence the install consent shows, when it differs. */
  consentText: string;
  /** Set for a network permission. */
  scope?: HttpTargetScope;
}

type Translate = (key: DictKey, ...args: string[]) => string;

export function permissionLabel(permission: string, t: Translate): PermissionLabel {
  if (permission === "variables" || permission === "actions" || permission === "input" || permission === "storage" || permission === "notify") {
    const text = t(`plugins.permission.${permission}` as DictKey);
    return { kind: permission, text, consentText: text };
  }
  const widgetOption = widgetOptionOf(permission);
  if (widgetOption) {
    const text = t(`plugins.permission.widget.${widgetOption}` as DictKey);
    return { kind: "widget", text, consentText: text };
  }
  if (permission.startsWith("http:")) {
    const target = permission.slice(5);
    const scope = networkTargetScope(target);
    return { kind: "http", scope, text: t(`plugins.permission.http.${scope}` as DictKey, target), consentText: t("consent.http", target) };
  }
  const text = t("plugins.permission.unknown", permission);
  return { kind: "unknown", text, consentText: text };
}
