import { ChevronDown, ChevronRight, ChevronUp, X } from "lucide-react";
import type { ActionBinding, ConditionNode } from "@macro/renderer";
import type { VariableInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { FLOW_ELSE, FLOW_END, FLOW_IF, type FlowRow, type Lane } from "./actionFlow";
import { IfStepEditor, whenOf } from "./IfStepEditor";
import { summarizeCondition } from "./dynamic/conditionSummary";

/*
 * The parts of an If block: the header card (what it tests), the Otherwise divider and the End cap. They are plain presentational
 * pieces, so any list of steps that has If blocks draws them the same way. The stored list stays flat (see actionFlow.ts); a block
 * is only drawn as one by a rail down its left side, in the colour of its lane.
 */

/** How far each block lane is indented. A rail of this lane's colour (If = accent, Otherwise = neutral) joins all rows of a block. */
const LANE_STEP = 14;
const railColor = (lane: Lane) => (lane === "if" ? "var(--ms-accent)" : "var(--ms-border-strong)");
export function railStyle(stack: Lane[]): React.CSSProperties {
  if (stack.length === 0) return {};
  return {
    paddingLeft: stack.length * LANE_STEP,
    backgroundImage: stack.map((l) => `linear-gradient(${railColor(l)}, ${railColor(l)})`).join(", "),
    backgroundSize: stack.map(() => "3px 100%").join(", "),
    backgroundPosition: stack.map((_, i) => `${i * LANE_STEP + 4}px 0`).join(", "),
    backgroundRepeat: "no-repeat",
  };
}

/** What an If tests, in a few words (its condition, or how the step before ended). */
export function summarizeIf(binding: ActionBinding, t: ReturnType<typeof useT>["t"]): string {
  const when = whenOf(binding);
  return when === "condition" ? summarizeCondition(binding.settings.condition as ConditionNode | undefined, t) : t(`action.logic.when.${when}`);
}

interface RowFrame {
  frame: React.CSSProperties;
  setRef: (el: HTMLDivElement | null) => void;
}

/** The Otherwise divider: the steps below it run when the If was not met. */
export function ElseRow({ frame, setRef, onRemove }: RowFrame & { onRemove: () => void }) {
  const { t } = useT();
  return (
    <div ref={setRef} className="pf-card" style={{ ...frame, padding: "4px 8px", display: "flex", alignItems: "center", gap: 6, background: "var(--ms-bg-inset)" }}>
      <span className="pf-tag neutral">{t("action.logic.else")}</span>
      <div style={{ flex: 1 }} />
      <button className="ghost" onClick={onRemove} title={t("action.remove")}><X size={13} /></button>
    </div>
  );
}

/**
 * The cap that closes a block. It names the If it closes and says the steps below it run in every case, so a step that sits just
 * under the cap is plainly outside the block. A stray End (no If to close) can be removed.
 */
export function EndCap({ frame, setRef, summary, stray, onRemove }: RowFrame & { summary: string; stray?: boolean; onRemove: () => void }) {
  const { t } = useT();
  return (
    <div ref={setRef} style={{ ...frame, borderTop: "1px solid var(--ms-border-strong)", borderBottom: "1px solid var(--ms-border)", borderBottomLeftRadius: 6, padding: "3px 0 3px 6px", marginBottom: 2, fontSize: 11, display: "flex", alignItems: "center", gap: 6, minWidth: 0 }}>
      <span style={{ fontWeight: 700, letterSpacing: ".04em", textTransform: "uppercase", color: "var(--ms-text-secondary)", flex: "none" }}>{stray ? t("action.logic.end") : t("action.logic.endOf")}</span>
      {summary && <span title={`${summary} · ${t("action.logic.endHint")}`} style={{ flex: "0 1 auto", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", color: "var(--ms-text-disabled)" }}>{summary}</span>}
      {!stray && <span style={{ color: "var(--ms-text-disabled)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis", minWidth: 0 }}>· {t("action.logic.endHint")}</span>}
      {stray && <button className="ghost" onClick={onRemove} title={t("action.remove")} style={{ marginLeft: "auto" }}><X size={12} /></button>}
    </div>
  );
}

interface IfRowProps extends RowFrame {
  row: FlowRow;
  binding: ActionBinding;
  bindings: ActionBinding[];
  collapsed: boolean;
  grip: React.ReactNode;
  variableCatalog: VariableInfo[];
  onToggle: () => void;
  onChange: (settings: Record<string, unknown>) => void;
  onAddElse: () => void;
  onClose: () => void;
  onMove: (dir: -1 | 1) => void;
  canMove: (dir: -1 | 1) => boolean;
  onRemove: () => void;
}

/** An If: a slim row with what it tests, "Add else", and a collapse that shrinks the block to one line (what it tests, how many steps it holds). An If with no End says so and offers to close it. */
export function IfRow({ row, binding, bindings, collapsed, frame, setRef, grip, variableCatalog, onToggle, onChange, onAddElse, onClose, onMove, canMove, onRemove }: IfRowProps) {
  const { t, tn } = useT();
  const unclosed = row.endIndex === undefined;
  const stepCount = unclosed ? 0 : bindings.slice(row.index + 1, row.endIndex).filter((b) => b.type !== FLOW_IF && b.type !== FLOW_ELSE && b.type !== FLOW_END).length;
  const summary = summarizeIf(binding, t);
  return (
    <div ref={setRef} className="pf-card" style={{ ...frame, padding: 8, display: "flex", flexDirection: "column", gap: 8, background: "var(--ms-bg-canvas)" }}>
      <div style={{ display: "flex", gap: 4, alignItems: "center", flexWrap: "wrap" }}>
        <button className="ghost" onClick={onToggle} title={t(collapsed ? "action.logic.expand" : "action.logic.collapse")} aria-expanded={!collapsed} style={{ display: "flex", padding: 4 }}>
          {collapsed ? <ChevronRight size={14} /> : <ChevronDown size={14} />}
        </button>
        {grip}
        <span className="pf-tag">{t("action.logic.if")}</span>
        {/* The summary takes the room the buttons leave; when that is under 120px it drops to its own line instead of being cut to nothing. */}
        {collapsed && (
          <span title={`${summary} · ${tn("action.logic.steps", stepCount)}`} style={{ flex: "1 1 120px", minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontSize: 12, color: "var(--ms-text-secondary)" }}>
            {summary} · {tn("action.logic.steps", stepCount)}
          </span>
        )}
        <div style={{ display: "flex", gap: 0, marginLeft: "auto" }}>
          <button className="ghost" style={{ padding: 4 }} onClick={() => onMove(-1)} disabled={!canMove(-1)} title={t("action.moveUp")}><ChevronUp size={14} /></button>
          <button className="ghost" style={{ padding: 4 }} onClick={() => onMove(1)} disabled={!canMove(1)} title={t("action.moveDown")}><ChevronDown size={14} /></button>
          <button className="ghost" style={{ padding: 4 }} onClick={onRemove} title={t("action.logic.removeBlock")}><X size={14} /></button>
        </div>
      </div>
      {!collapsed && (
        <>
          <IfStepEditor binding={binding} variableCatalog={variableCatalog} onChange={onChange} />
          {row.elseIndex === undefined && (
            <button type="button" className="ghost" onClick={onAddElse} style={{ alignSelf: "flex-start", fontSize: 12 }}>{t("action.logic.addElse")}</button>
          )}
        </>
      )}
      {unclosed && (
        <div role="alert" style={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 6, fontSize: 12, color: "var(--ms-danger)" }}>
          <span style={{ minWidth: 0 }}>{t("action.logic.unclosed")}</span>
          <button type="button" className="ghost" onClick={onClose} style={{ fontSize: 12 }}>{t("action.logic.closeIt")}</button>
        </div>
      )}
    </div>
  );
}
