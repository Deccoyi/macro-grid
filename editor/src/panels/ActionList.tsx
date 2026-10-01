import { useEffect, useRef, useState } from "react";
import { CircleAlert, ChevronUp, ChevronDown, ChevronRight, X } from "lucide-react";
import type { ActionBinding, ConditionNode, Page } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { useCatalogText } from "../i18n/catalogText";
import { useT } from "../i18n/I18nContext";
import { ActionPicker } from "./ActionPicker";
import { formFor } from "./actionForms/forms";
import { FLOW_ELSE, FLOW_END, FLOW_IF, addElse, addIf, layout, move as moveRow, remove as removeRow, type FlowRow } from "./actionFlow";
import { IfStepEditor, whenOf } from "./IfStepEditor";
import { summarizeCondition } from "./dynamic/conditionSummary";

export interface ActionListProps {
  bindings: ActionBinding[];
  actions: ActionInfo[];
  pages: Page[];
  profiles: ProfileSummary[];
  variableCatalog: VariableInfo[];
  onChange: (next: ActionBinding[]) => void;
  /** A step to scroll to and outline once (the Error List's "Go to widget"); the list opens the blocks that hide it, then calls `onFocusDone`. */
  focusIndex?: number | null;
  onFocusDone?: () => void;
}

/** The "This button" variables work only in the button's own text and rules, so an action's fields do not offer them. */
function withoutSelf(catalog: VariableInfo[]): VariableInfo[] {
  return catalog.filter((v) => !v.name.toLowerCase().startsWith("self."));
}

/** An ordered list of action steps with If blocks: add, replace, move, remove and collapse. It works on plain bindings, so a widget's event and an automation rule share it. */
export function ActionList({ bindings, actions, pages, profiles, variableCatalog, onChange, focusIndex = null, onFocusDone }: ActionListProps) {
  const { t } = useT();
  const catalogText = useCatalogText();

  // The step from the Error List: show that event's list (the caller), open the blocks that hide the step, scroll to it and outline it once.
  const [outlined, setOutlined] = useState<number | null>(null);
  const rowRefs = useRef<(HTMLDivElement | null)[]>([]);

  // Which If blocks are drawn collapsed. Only a view state: it is dropped whenever the list changes shape.
  const [collapsed, setCollapsed] = useState<Set<number>>(new Set());
  const rows = layout(bindings);
  const hidden = (index: number) => rows.some((r) => r.kind === "if" && collapsed.has(r.index) && index > r.index && index <= (r.endIndex ?? bindings.length - 1));
  const reshape = (next: ActionBinding[]) => { setCollapsed(new Set()); onChange(next); };

  useEffect(() => {
    if (focusIndex === null) return;
    setCollapsed((prev) => {
      const next = new Set([...prev].filter((c) => !(focusIndex > c && focusIndex <= (rows[c]?.endIndex ?? bindings.length - 1))));
      return next.size === prev.size ? prev : next;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusIndex]);

  useEffect(() => {
    if (focusIndex === null || !rowRefs.current[focusIndex]) return;
    rowRefs.current[focusIndex]?.scrollIntoView({ block: "nearest" });
    setOutlined(focusIndex);
    onFocusDone?.();
  }, [focusIndex, collapsed, onFocusDone]);

  useEffect(() => {
    if (outlined === null) return;
    const timer = window.setTimeout(() => setOutlined(null), 2000);
    return () => window.clearTimeout(timer);
  }, [outlined]);

  const updateBinding = (index: number, next: Partial<ActionBinding>) => {
    const copy = bindings.map((b, i) => (i === index ? { ...b, ...next } : b));
    onChange(copy);
  };

  const addBinding = (type: string) => (type === FLOW_IF ? reshape(addIf(bindings)) : reshape([...bindings, { type, settings: {} }]));

  const removeBinding = (index: number) => reshape(removeRow(bindings, index));

  const move = (index: number, dir: -1 | 1) => {
    const next = moveRow(bindings, index, dir);
    if (next !== bindings) reshape(next);
  };

  const toggleCollapsed = (index: number) =>
    setCollapsed((prev) => { const next = new Set(prev); if (!next.delete(index)) next.add(index); return next; });

  // Otherwise and End are never picked by hand (an If brings them); the picker of an existing row does not offer If either.
  const addable = actions.filter((a) => a.type !== FLOW_ELSE && a.type !== FLOW_END);
  const replaceable = addable.filter((a) => a.type !== FLOW_IF);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      {bindings.length === 0 && <div style={{ color: "var(--ms-text-secondary)", fontSize: 12 }}>{t("action.none")}</div>}

      {rows.map((row) => {
        const { index } = row;
        const binding = bindings[index]!;
        if (hidden(index)) return null;
        const actionInfo = actions.find((a) => a.type === binding.type);
        const Form = formFor(binding.type, actionInfo);
        const frame = {
          border: `1px solid ${outlined === index ? "var(--ms-accent)" : "var(--ms-border)"}`,
          outline: outlined === index ? "2px solid var(--ms-accent)" : undefined,
          borderRadius: 4,
          marginLeft: row.depth * 16,
        };
        const setRef = (el: HTMLDivElement | null) => { rowRefs.current[index] = el; };

        if (row.kind === "end") {
          return (
            <div key={index} ref={setRef} style={{ ...frame, border: "none", borderTop: "1px dashed var(--ms-border)", borderRadius: 0, padding: "2px 0 0", fontSize: 11, fontWeight: 700, letterSpacing: ".04em", textTransform: "uppercase", color: "var(--ms-text-disabled)", display: "flex", alignItems: "center", gap: 6 }}>
              {t("action.logic.end")}
              {row.stray && <button className="ghost" onClick={() => removeBinding(index)} title={t("action.remove")} style={{ marginLeft: "auto" }}><X size={12} /></button>}
            </div>
          );
        }
        if (row.kind === "else") {
          return (
            <div key={index} ref={setRef} style={{ ...frame, padding: "4px 8px", display: "flex", alignItems: "center", gap: 6, background: "var(--ms-bg-inset)" }}>
              <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: ".04em", textTransform: "uppercase", color: "var(--ms-text-secondary)" }}>{t("action.logic.else")}</span>
              <div style={{ flex: 1 }} />
              <button className="ghost" onClick={() => removeBinding(index)} title={t("action.remove")}><X size={13} /></button>
            </div>
          );
        }
        if (row.kind === "if") return (
          <IfRow
            key={index}
            row={row}
            binding={binding}
            bindings={bindings}
            collapsed={collapsed.has(index)}
            frame={frame}
            setRef={setRef}
            variableCatalog={withoutSelf(variableCatalog)}
            onToggle={() => toggleCollapsed(index)}
            onChange={(settings) => updateBinding(index, { settings })}
            onAddElse={() => reshape(addElse(bindings, index))}
            onMove={(dir) => move(index, dir)}
            canMove={(dir) => moveRow(bindings, index, dir) !== bindings}
            onRemove={() => removeBinding(index)}
          />
        );

        return (
          <div key={index} ref={setRef} style={{ ...frame, padding: 8, display: "flex", flexDirection: "column", gap: 6 }}>
            <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
              <ActionPicker
                actions={replaceable}
                onPick={(type) => updateBinding(index, { type, settings: {} })}
                renderTrigger={(open) => (
                  <button type="button" className="ghost" onClick={open} style={{ flex: 1, textAlign: "left", justifyContent: "flex-start" }}>
                    {actionInfo ? catalogText.actionName(actionInfo) : (
                      <span title={t("action.unavailable")} style={{ display: "flex", alignItems: "center", gap: 6 }}>
                        <CircleAlert size={13} style={{ color: "var(--ms-danger)", flex: "0 0 auto" }} />
                        {binding.type}
                      </span>
                    )}
                  </button>
                )}
              />
              {bindings.length > 1 && (
                <>
                  <button className="ghost" onClick={() => move(index, -1)} disabled={moveRow(bindings, index, -1) === bindings} title={t("action.moveUp")}><ChevronUp size={14} /></button>
                  <button className="ghost" onClick={() => move(index, 1)} disabled={moveRow(bindings, index, 1) === bindings} title={t("action.moveDown")}><ChevronDown size={14} /></button>
                </>
              )}
              <button className="ghost" onClick={() => removeBinding(index)} title={t("action.remove")}><X size={14} /></button>
            </div>
            <Form binding={binding} pages={pages} profiles={profiles} actionInfo={actionInfo} variableCatalog={withoutSelf(variableCatalog)} onChange={(settings) => updateBinding(index, { settings })} />
          </div>
        );
      })}

      <ActionPicker actions={addable} onPick={addBinding} />
    </div>
  );
}

interface IfRowProps {
  row: FlowRow;
  binding: ActionBinding;
  bindings: ActionBinding[];
  collapsed: boolean;
  frame: React.CSSProperties;
  setRef: (el: HTMLDivElement | null) => void;
  variableCatalog: VariableInfo[];
  onToggle: () => void;
  onChange: (settings: Record<string, unknown>) => void;
  onAddElse: () => void;
  onMove: (dir: -1 | 1) => void;
  canMove: (dir: -1 | 1) => boolean;
  onRemove: () => void;
}

/** An If: a slim row with what it tests, "Add otherwise", and a collapse that shrinks the block to one line (what it tests, how many steps it holds). */
function IfRow({ row, binding, bindings, collapsed, frame, setRef, variableCatalog, onToggle, onChange, onAddElse, onMove, canMove, onRemove }: IfRowProps) {
  const { t, tn } = useT();
  const when = whenOf(binding);
  const stepCount = row.endIndex === undefined ? 0 : bindings.slice(row.index + 1, row.endIndex).filter((b) => b.type !== FLOW_IF && b.type !== FLOW_ELSE && b.type !== FLOW_END).length;
  const summary = when === "condition" ? summarizeCondition(binding.settings.condition as ConditionNode | undefined, t) : t(`action.logic.when.${when}`);
  return (
    <div ref={setRef} style={{ ...frame, padding: 8, display: "flex", flexDirection: "column", gap: 8, background: "var(--ms-bg-canvas)" }}>
      <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
        <button className="ghost" onClick={onToggle} title={t(collapsed ? "action.logic.expand" : "action.logic.collapse")} aria-expanded={!collapsed} style={{ display: "flex", padding: 4 }}>
          {collapsed ? <ChevronRight size={14} /> : <ChevronDown size={14} />}
        </button>
        <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: ".04em", textTransform: "uppercase", color: "var(--ms-text-secondary)" }}>{t("action.logic.if")}</span>
        {collapsed && (
          <span style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontSize: 12, color: "var(--ms-text-secondary)" }}>
            {summary} · {tn("action.logic.steps", stepCount)}
          </span>
        )}
        <div style={{ flex: 1 }} />
        <button className="ghost" onClick={() => onMove(-1)} disabled={!canMove(-1)} title={t("action.moveUp")}><ChevronUp size={14} /></button>
        <button className="ghost" onClick={() => onMove(1)} disabled={!canMove(1)} title={t("action.moveDown")}><ChevronDown size={14} /></button>
        <button className="ghost" onClick={onRemove} title={t("action.logic.removeBlock")}><X size={14} /></button>
      </div>
      {!collapsed && (
        <>
          <IfStepEditor binding={binding} variableCatalog={variableCatalog} onChange={onChange} />
          {row.elseIndex === undefined && (
            <button type="button" className="ghost" onClick={onAddElse} style={{ alignSelf: "flex-start", fontSize: 12 }}>{t("action.logic.addElse")}</button>
          )}
        </>
      )}
    </div>
  );
}
