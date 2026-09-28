import { useDiagnostics } from "../diagnostics/DiagnosticsContext";
import { useT } from "../i18n/I18nContext";
import { toolWindowById, type ToolWindowDefinition } from "./toolWindows";

/** The collapsed edge tab strip for unpinned tool windows — docs/plans/docking-workspace-plan.md ("Pin
 * and auto-hide"): 22 px, no space taken when empty, vertical text on the side rails. */
export function AutoHideRail({
  edge,
  ids,
  openId,
  onToggle,
}: {
  edge: "left" | "right" | "bottom";
  ids: string[];
  openId: string | null;
  onToggle: (id: string) => void;
}) {
  if (ids.length === 0) return null;
  const vertical = edge === "left" || edge === "right";

  return (
    <div
      style={{
        flex: "0 0 auto",
        width: vertical ? 22 : undefined,
        height: vertical ? undefined : 22,
        display: "flex",
        flexDirection: vertical ? "column" : "row",
        background: "var(--ms-bg-surface)",
        borderLeft: edge === "right" ? "1px solid var(--ms-border)" : undefined,
        borderRight: edge === "left" ? "1px solid var(--ms-border)" : undefined,
        borderTop: edge === "bottom" ? "1px solid var(--ms-border)" : undefined,
      }}
    >
      {ids.map((id) => {
        const def = toolWindowById(id);
        if (!def) return null;
        const active = openId === id;
        return (
          <button
            key={id}
            onClick={() => onToggle(id)}
            title={def.id}
            style={{
              flex: "0 0 auto",
              width: vertical ? 22 : 96,
              height: vertical ? 96 : 22,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              background: active ? "var(--ms-bg-surface-raised)" : "transparent",
              color: active ? "var(--ms-accent)" : "var(--ms-text-secondary)",
              border: "none",
              borderBottom: vertical ? "1px solid var(--ms-border)" : undefined,
              borderRight: vertical ? undefined : "1px solid var(--ms-border)",
              cursor: "pointer",
              fontSize: 11,
              writingMode: vertical ? "vertical-rl" : "horizontal-tb",
              transform: vertical ? "rotate(180deg)" : undefined,
              letterSpacing: ".01em",
            }}
          >
            <RailLabel def={def} vertical={vertical} />
          </button>
        );
      })}
    </div>
  );
}

function RailLabel({ def, vertical }: { def: ToolWindowDefinition; vertical: boolean }) {
  const { t } = useT();
  // Only errorList carries counts, and it's always on the bottom (horizontal) edge — pills next to the
  // rotated vertical-rl text of a side-rail tab would be unreadable, so this only ever renders for that
  // one tab in practice, but the vertical guard keeps it correct if a future tool window changes edges.
  if (def.id !== "errorList" || vertical) return <>{t(def.titleKey)}</>;
  return <ErrorListRailLabel def={def} />;
}

function ErrorListRailLabel({ def }: { def: ToolWindowDefinition }) {
  const { t } = useT();
  const { diagnostics } = useDiagnostics();
  let errorCount = 0;
  let warningCount = 0;
  for (const d of diagnostics) {
    if (d.severity === "error") errorCount++;
    else if (d.severity === "warning") warningCount++;
  }
  return (
    <span style={{ display: "flex", alignItems: "center", gap: 5 }}>
      {t(def.titleKey)}
      {errorCount > 0 && <Pill color="var(--ms-danger)">{errorCount}</Pill>}
      {warningCount > 0 && <Pill color="var(--ms-text-secondary)">{warningCount}</Pill>}
    </span>
  );
}

function Pill({ color, children }: { color: string; children: React.ReactNode }) {
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        minWidth: 15,
        height: 15,
        padding: "0 4px",
        borderRadius: 8,
        background: color,
        color: "var(--ms-bg-canvas)",
        fontSize: 10,
        fontWeight: 600,
        lineHeight: 1,
      }}
    >
      {children}
    </span>
  );
}
