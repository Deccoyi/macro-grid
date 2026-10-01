import { useMemo, useRef, useState } from "react";
import type { Profile } from "@macro/renderer";
import { CircleAlert, Copy, Eraser, Info, Save, Search, TriangleAlert } from "lucide-react";
import { api } from "../api/client";
import { toExportLines } from "../diagnostics/exportLines";
import { useDiagnostics } from "../diagnostics/DiagnosticsContext";
import type { Diagnostic, DiagnosticSeverity } from "../diagnostics/types";
import { useT } from "../i18n/I18nContext";
import { useEditorStateContext } from "../state/EditorStateContext";
import { clampWidth, DIAGNOSTIC_COLUMNS, gridTemplate, loadColumnWidths, saveColumnWidths, tableMinWidth, type DiagnosticColumn } from "./diagnosticColumns";
type Filter = "all" | DiagnosticSeverity;

/** The Error List tool window — docs/design/docking-workspace.md ("Error List"). A table, filter row
 * and severity counts over DiagnosticsContext data. The server's problems (a plugin's refused key press, a plugin that did not load) come
 * in with a source and a count; the editor's own checks have no producer yet, so nothing is invented here.
 * Double-click-to-navigate is not built yet: it needs the document area's multi-tab open-page list
 * (phase 5), which doesn't exist, so there is nowhere to navigate TO yet. */
export function ErrorListPanel() {
  const { t } = useT();
  const { diagnostics, clear } = useDiagnostics();
  const { profile } = useEditorStateContext();
  const [filter, setFilter] = useState<Filter>("all");
  const [query, setQuery] = useState("");
  const [widths, setWidths] = useState(loadColumnWidths);
  const template = gridTemplate(widths);
  const [exportState, setExportState] = useState<"copied" | "saved" | "failed" | null>(null);
  const drag = useRef<{ column: DiagnosticColumn; startX: number; startWidth: number } | null>(null);

  const startDrag = (column: DiagnosticColumn) => (e: React.PointerEvent<HTMLSpanElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = { column, startX: e.clientX, startWidth: widths[column] };
  };
  const moveDrag = (e: React.PointerEvent<HTMLSpanElement>) => {
    const d = drag.current;
    if (!d) return;
    const width = clampWidth(d.startWidth + e.clientX - d.startX);
    setWidths((w) => ({ ...w, [d.column]: width }));
  };
  const endDrag = () => {
    if (!drag.current) return;
    drag.current = null;
    saveColumnWidths(widths);
  };

  const textOf = (d: Diagnostic) => d.message ?? (d.messageKey ? t(d.messageKey, ...(d.messageArgs ?? [])) : "");
  const runExport = async (to: "text" | "file") => {
    try {
      const result = await api.exportProblems(to, toExportLines(diagnostics, textOf, t("errorList.source.editor")));
      if (to === "text") {
        await navigator.clipboard.writeText(result.text ?? "");
        setExportState("copied");
      } else {
        setExportState(result.path ? "saved" : null);
      }
    } catch {
      setExportState("failed");
    }
    window.setTimeout(() => setExportState(null), 2500);
  };

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
      const text = `${d.code} ${d.message ?? d.messageKey ?? ""} ${d.sourceName ?? d.source} ${where(d, profile).page} ${where(d, profile).location}`.toLowerCase();
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
        <span style={{ marginLeft: "auto", fontSize: 11.5, color: exportState === "failed" ? "var(--ms-danger)" : "var(--ms-text-secondary)" }} role="status">
          {exportState === "copied" ? t("errorList.copied") : exportState === "saved" ? t("errorList.saved") : exportState === "failed" ? t("errorList.exportFailed") : ""}
        </span>
        <button onClick={() => void runExport("text")} title={t("errorList.exportNote")} style={toolButton}>
          <Copy size={12} strokeWidth={1.75} /> {t("errorList.copy")}
        </button>
        <button onClick={() => void runExport("file")} title={t("errorList.exportNote")} style={toolButton}>
          <Save size={12} strokeWidth={1.75} /> {t("errorList.save")}
        </button>
        <button
          onClick={() => clear()}
          style={{ display: "flex", alignItems: "center", gap: 5, color: "var(--ms-text-secondary)", fontSize: 11.5, background: "transparent", border: "none", cursor: "pointer" }}
        >
          <Eraser size={12} strokeWidth={1.75} /> {t("errorList.clear")}
        </button>
      </div>

      <div style={{ flex: "1 1 auto", overflow: "auto" }}>
        <div style={{ minWidth: tableMinWidth(widths) }}>
          <div style={{ position: "sticky", top: 0, zIndex: 1, display: "grid", gridTemplateColumns: template, height: 24, alignItems: "center", padding: "0 10px", color: "var(--ms-text-disabled)", background: "var(--ms-bg-surface)", fontSize: 11, borderBottom: "1px solid var(--ms-border)" }}>
            {DIAGNOSTIC_COLUMNS.map((column) => (
              <span key={column} style={{ position: "relative", height: "100%", display: "flex", alignItems: "center", overflow: "hidden", whiteSpace: "nowrap" }}>
                {t(`errorList.col.${column}`)}
                <span
                  role="separator"
                  aria-orientation="vertical"
                  aria-label={t("errorList.resizeColumn")}
                  onPointerDown={startDrag(column)}
                  onPointerMove={moveDrag}
                  onPointerUp={endDrag}
                  onPointerCancel={endDrag}
                  style={{ position: "absolute", top: 0, right: 0, width: 7, height: "100%", cursor: "col-resize", borderRight: "1px solid var(--ms-border)", touchAction: "none" }}
                />
              </span>
            ))}
          </div>
          {filtered.length === 0 ? (
            <div style={{ padding: "16px 10px", color: "var(--ms-text-secondary)", fontSize: 12 }}>{t("errorList.empty")}</div>
          ) : (
            filtered.map((d) => <Row key={d.id} d={d} template={template} profile={profile} />)
          )}
        </div>
      </div>
    </div>
  );
}

/** The page and the widget (with its event) a line is about. The names come from the open profile; for a line about another profile the ids are shown. */
function where(d: Diagnostic, profile: Profile | null): { page: string; location: string } {
  const target = d.target;
  if (!target) return { page: "—", location: "—" };
  const own = profile && profile.id === target.profileId ? profile : null;
  const page = target.pageId ? (own?.pages.find((p) => p.id === target.pageId)?.name ?? target.pageId) : "—";
  const widget = target.widgetId
    ? (own?.pages.flatMap((p) => p.widgets).find((w) => w.id === target.widgetId)?.name ?? target.widgetId)
    : (target.field ?? "");
  const detail = [target.event, target.actionIndex !== undefined ? `#${target.actionIndex + 1}` : ""].filter(Boolean).join(" ");
  return { page, location: [widget, detail].filter(Boolean).join(" · ") || "—" };
}

const toolButton: React.CSSProperties = { display: "flex", alignItems: "center", gap: 5, color: "var(--ms-text-secondary)", fontSize: 11.5, background: "transparent", border: "none", cursor: "pointer" };

function Row({ d, template, profile }: { d: Diagnostic; template: string; profile: Profile | null }) {
  const { t } = useT();
  const { page, location } = where(d, profile);
  const Icon = d.severity === "error" ? CircleAlert : d.severity === "warning" ? TriangleAlert : Info;
  const color = d.severity === "error" ? "var(--ms-danger)" : "var(--ms-text-secondary)";
  const text = d.message ?? (d.messageKey ? t(d.messageKey, ...(d.messageArgs ?? [])) : "");
  const label = d.severity === "error" ? t("errorList.severity.error") : d.severity === "warning" ? t("errorList.severity.warning") : t("errorList.severity.info");
  return (
    <div style={{ display: "grid", gridTemplateColumns: template, height: 24, alignItems: "center", padding: "0 10px", fontSize: 12, borderBottom: "1px solid var(--ms-bg-canvas)" }}>
      <span style={{ display: "flex", alignItems: "center", gap: 6, color }}><Icon size={12} strokeWidth={2.25} />{label}</span>
      <span style={{ color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>{d.code}</span>
      <span style={{ color: "var(--ms-text-primary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={text}>
        {text}
        {(d.count ?? 1) > 1 && <span style={{ marginLeft: 8, color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>×{d.count}</span>}
      </span>
      <span style={{ color: "var(--ms-text-secondary)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{d.sourceName ?? t("errorList.source.editor")}</span>
      <span style={{ color: "var(--ms-text-secondary)" }}>{page}</span>
      <span style={{ color: "var(--ms-text-secondary)", fontFamily: "Consolas, monospace" }}>{location}</span>
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
