import type { DictKey } from "../i18n/tr";

export type DiagnosticSeverity = "error" | "warning" | "info";

export interface DiagnosticTarget {
  profileId: string;
  pageId?: string;
  widgetId?: string;
  /** The event of the widget the line is about ("press", "longPress"...) and the 0-based number of the action in it. */
  event?: string;
  actionIndex?: number;
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
  /** Text the editor produces itself (translated). Either this or `message`. */
  messageKey?: DictKey;
  messageArgs?: string[];
  /** Text a producer outside the editor already wrote (the server's problems), shown as it is. */
  message?: string;
  /** Display name of the source (a plugin's name); the editor's own checks leave it out. */
  sourceName?: string;
  /** How many times this same message happened; shown as "x5" when more than one. */
  count?: number;
  target?: DiagnosticTarget;
  /** "server" for a line the server reported; the export leaves those out because the server adds its own. */
  origin?: "server";
}
