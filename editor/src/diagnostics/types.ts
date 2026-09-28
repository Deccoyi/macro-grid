import type { DictKey } from "../i18n/tr";

export type DiagnosticSeverity = "error" | "warning" | "info";

export interface DiagnosticTarget {
  profileId: string;
  pageId?: string;
  widgetId?: string;
  /** Free text for display only, e.g. "actions.press[0]". */
  field?: string;
}

export interface Diagnostic {
  /** Unique inside its source. */
  id: string;
  /** Producer id, e.g. "profile-validation". */
  source: string;
  severity: DiagnosticSeverity;
  /** e.g. "E102"; each producer documents its own codes. */
  code: string;
  messageKey: DictKey;
  messageArgs?: string[];
  target?: DiagnosticTarget;
}
