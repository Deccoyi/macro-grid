import type { Profile } from "@macro/renderer";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import type { Diagnostic } from "../diagnostics/types";

/** The body of the Details dialog of one Error List line: the whole message, what kind of line it is, where it is, and what to do about it. */
export function DiagnosticDetails({ d, text, where, profile }: { d: Diagnostic; text: string; where: { page: string; location: string }; profile: Profile | null }) {
  const { t, lang } = useT();
  const help = t(`errorHelp.${d.code}` as DictKey);
  const exists = !d.target?.widgetId || !!profile?.pages.some((p) => p.widgets.some((w) => w.id === d.target?.widgetId));
  const time = (iso?: string) => (iso ? new Date(iso).toLocaleString(lang) : null);
  const rows: [string, string | null][] = [
    [t("errorList.details.severity"), t(`errorList.severity.${d.severity}` as DictKey)],
    [t("errorList.col.code"), d.code],
    [t("errorList.col.source"), d.sourceName ?? t("errorList.source.editor")],
    [t("errorList.details.count"), (d.count ?? 1) > 1 ? String(d.count) : null],
    [t("errorList.details.first"), time(d.firstAt)],
    [t("errorList.details.last"), (d.count ?? 1) > 1 ? time(d.lastAt) : null],
    [t("errorList.col.screen"), d.target ? where.page : null],
    [t("errorList.col.location"), d.target ? where.location : null],
  ];
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10, fontSize: 12.5, maxWidth: 520 }}>
      <div style={{ whiteSpace: "pre-wrap", wordBreak: "break-word", userSelect: "text" }}>{text}</div>
      <div style={{ display: "grid", gridTemplateColumns: "auto 1fr", gap: "3px 12px", color: "var(--ms-text-secondary)" }}>
        {rows.filter(([, v]) => v).map(([k, v]) => (
          <div key={k} style={{ display: "contents" }}>
            <span>{k}</span>
            <span style={{ color: "var(--ms-text-primary)", userSelect: "text" }}>{v}</span>
          </div>
        ))}
      </div>
      {help && <div style={{ color: "var(--ms-text-secondary)", userSelect: "text" }}>{help}</div>}
      {d.target?.widgetId && !exists && <div style={{ color: "var(--ms-danger)" }}>{t("errorList.details.gone")}</div>}
    </div>
  );
}
