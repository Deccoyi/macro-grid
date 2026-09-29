import { useMemo, useState } from "react";
import { CircleAlert, Eraser, Info, Search, TriangleAlert } from "lucide-react";
import { useDiagnostics } from "../diagnostics/DiagnosticsContext";
import type { Diagnostic, DiagnosticSeverity } from "../diagnostics/types";
import { useT } from "../i18n/I18nContext";
const COLUMNS = "90px 70px 1fr 150px 130px 100px";
type Filter = "all" | DiagnosticSeverity;

/** The Error List tool window — docs/design/docking-workspace.md ("Error List"). A table, filter row
 * and severity counts over DiagnosticsContext data. The server's problems (a plugin's refused key press, a plugin that did not load) come
 * in with a source and a count; the editor's own checks have no producer yet, so nothing is invented here.
 * Double-click-to-navigate is not built yet: it needs the document area's multi-tab open-page list
 * (phase 5), which doesn't exist, so there is nowhere to navigate TO yet. */
export function ErrorListPanel() {
  const { t } = useT();
  const { diagnostics, clear } = useDiagnostics();
  const [filter, setFilter] = useState<Filter>("all");
  const [query, setQuery] = useState("");

  const counts = useMemo(() => {
    const c = { error: 0, warning: 0, info: 0 };
    for (const d of diagnostics) c[d.severity]++;
    return c;
  }, [diagnostics]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return diagnostics.filter((d) => {
      if (filter !== "all" && d.severity !== filter) return false;
      if (!q) return true;
      const text = `${d.code} ${d.message ?? d.messageKey ?? ""} ${d.sourceName ?? d.source} ${d.target?.pageId ?? ""} ${d.target?.field ?? ""}`.toLowerCase();
      return text.includes(q);
    });
  }, [diagnostics, filter, query]);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }}>
      <div style={{ height: 30, flex: "0 0 auto", display: "flex", alignItems: "center", gap: 10, padding: "0 10px", borderBottom: "1px solid var(--ms-border)" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 6, height: 22, padding: "0 8px", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 3, width: 200 }}>
          <Search size={12} color="var(--ms-text-disabled)" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("errorList.filterPlaceholder")}
            style={{ border: "none", background: "transparent", color: "var(--ms-text-primary)", fontSize: 11.5, width: "100%", outline: "none" }}
          />
        </div>
        <div style={{ display: "flex", height: 22, border: "1px solid var(--ms-border)", borderRadius: 3, overflow: "hidden", fontSize: 11.5 }}>
          <SegButton active={filter === "all"} onClick={() => setFilter("all")}>{t("errorList.all")} {diagnostics.length}</SegButton>
          <SegButton active={filter === "error"} onClick={() => setFilter("error")}><CircleAlert size={10} strokeWidth={2.5} color="var(--ms-danger)" /> {counts.error}</SegButton>
          <SegButton active={filter === "warning"} onClick={() => setFilter("warning")}><TriangleAlert size={10} strokeWidth={2.5} /> {counts.warning}</SegButton>
          <SegButton active={filter === "info"} onClick={() => setFilter("info")} last><Info size={10} strokeWidth={2.5} /> {counts.info}</SegButton>
        </div>
        <button
          onClick={() => clear()}
          style={{ marginLeft: "auto", display: "flex", alignItems: "center", gap: 5, color: "var(--ms-text-secondary)", fontSize: 11.5, background: "transparent", border: "none", cursor: "pointer" }}
        >
          <Eraser size={12} strokeWidth={1.75} /> {t("errorList.clear")}
        </button>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: COLUMNS, height: 24, flex: "0 0 auto", alignItems: "center", padding: "0 10px", color: "var(--ms-text-disabled)", fontSize: 11, borderBottom: "1px solid var(--ms-border)" }}>
        <span>{t("errorList.col.severity")}</span>
        <span>{t("errorList.col.code")}</span>
        <span>{t("errorList.col.description")}</span>
        <span>{t("errorList.col.source")}</span>
        <span>{t("errorList.col.screen")}</span>
        <span>{t("errorList.col.location")}</span>
      </div>

      <div style={{ flex: "1 1 auto", overflow: "auto" }}>
        {filtered.length === 0 ? (
          <div style={{ padding: "16px 10px", color: "var(--ms-text-secondary)", fontSize: 12 }}>{t("errorList.empty")}</div>
        ) : (
          filtered.map((d) => <Row key={d.id} d={d} />)
        )}
      </div>
    </div>
  );
}

function Row({ d }: { d: Diagnostic }) {
  const { t } = useT();
  const Icon = d.severity === "error" ? CircleAlert : d.severity === "warning" ? TriangleAlert : Info;
  const color = d.severity === "error" ? "var(--ms-danger)" : "var(--ms-text-secondary)";
  const text = d.message ?? (d.messageKey ? t(d.messageKey, ...(d.messageArgs ?? [])) : "");
  const label = d.severity === "error" ? t("errorList.severity.error") : d.severity === "warning" ? t("errorList.severity.warning") : t("errorList.severity.info");
  return (
    <div style={{ display: "grid", gridTemplateColumns: COLUMNS, height: 24, alignItems: "center", padding: "0 10px", fontSize: 12, borderBottom: "1px solid var(--ms-bg-canvas)" }}>
      <span style={{ display: "flex", alignItems: "center", gap: 6, color }}><Icon size={12} strokeWidth={2.25} />{label}</span>
      <span style={{ color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>{d.code}</span>
      <span style={{ color: "var(--ms-text-primary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={text}>
        {text}
        {(d.count ?? 1) > 1 && <span style={{ marginLeft: 8, color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>×{d.count}</span>}
      </span>
      <span style={{ color: "var(--ms-text-secondary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{d.sourceName ?? t("errorList.source.editor")}</span>
      <span style={{ color: "var(--ms-text-secondary)" }}>{d.target?.pageId ?? "—"}</span>
      <span style={{ color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>{d.target?.field ?? "—"}</span>
    </div>
  );
}

function SegButton({ active, last, onClick, children }: { active: boolean; last?: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      style={{
        padding: "0 10px",
        display: "flex",
        alignItems: "center",
        gap: 5,
        background: active ? "var(--ms-bg-surface-raised)" : "transparent",
        color: active ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
        borderRight: last ? "none" : "1px solid var(--ms-border)",
        border: "none",
        borderLeft: "none",
        borderTop: "none",
        borderBottom: "none",
        cursor: "pointer",
        fontSize: 11.5,
      }}
    >
      {children}
    </button>
  );
}
