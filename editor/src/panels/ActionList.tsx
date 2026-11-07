import { useEffect, useRef, useState } from "react";
import { CircleAlert, ChevronUp, ChevronDown, GripVertical, X } from "lucide-react";
import type { ActionBinding, Page } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { useCatalogText } from "../i18n/catalogText";
import { useT } from "../i18n/I18nContext";
import { ActionPicker } from "./ActionPicker";
import { formFor } from "./actionForms/forms";
import type { WidgetIconControl } from "./actionForms/SchemaForm";
import { FLOW_ELSE, FLOW_END, FLOW_IF, MAX_DEPTH, addElse, carryCollapsed, closeIf, insertAt, laneStacks, layout, move as moveRow, moveTo, remove as removeRow } from "./actionFlow";
import { ElseRow, EndCap, IfRow, railStyle, summarizeIf } from "./IfBlock";

export interface ActionListProps {
  bindings: ActionBinding[];
  actions: ActionInfo[];
  pages: Page[];
  profiles: ProfileSummary[];
  variableCatalog: VariableInfo[];
  onChange: (next: ActionBinding[]) => void;
  /** The widget's button icon, offered to action forms that can fill it. */
  widgetIcon?: WidgetIconControl;
  /** A step to scroll to and outline once (the Error List's "Go to widget"); the list opens the blocks that hide it, then calls `onFocusDone`. */
  focusIndex?: number | null;
  onFocusDone?: () => void;
}

/** The "This button" variables work only in the button's own text and rules, so an action's fields do not offer them. */
const LEGACY_SET_VARIABLE = "core.setVariable";

function withoutSelf(catalog: VariableInfo[]): VariableInfo[] {
  return catalog.filter((v) => !v.name.toLowerCase().startsWith("self."));
}

/** An ordered list of action steps with If blocks: add (anywhere, also inside a block), replace, move (buttons or drag), remove and collapse. It works on plain bindings, so a widget's event and an automation rule share it. */
export function ActionList({ bindings, actions, pages, profiles, variableCatalog, onChange, widgetIcon, focusIndex = null, onFocusDone }: ActionListProps) {
  const { t } = useT();
  const catalogText = useCatalogText();

  // The step from the Error List: show that event's list (the caller), open the blocks that hide the step, scroll to it and outline it once.
  const [outlined, setOutlined] = useState<number | null>(null);
  const rowRefs = useRef<(HTMLDivElement | null)[]>([]);

  // Which If blocks are drawn collapsed. Only a view state: it follows its If when the list is reshaped (moved, steps added or removed).
  const [collapsed, setCollapsed] = useState<Set<number>>(new Set());
  const rows = layout(bindings);
  const stacks = laneStacks(bindings);
  const hidden = (index: number) => rows.some((r) => r.kind === "if" && collapsed.has(r.index) && index > r.index && index <= (r.endIndex ?? bindings.length - 1));
  /** What the If that an End closes tests, shown on the End so a step below it is plainly outside. */
  const endSummary = (end: { index: number }) => { const owner = ownerOf(end); return owner ? summarizeIf(bindings[owner.index]!, t) : ""; };
  const ownerOf = (end: { index: number }) => rows.find((r) => r.kind === "if" && r.endIndex === end.index);
  const endHasNoElse = (end: { index: number }) => ownerOf(end)?.elseIndex === undefined;
  const [notice, setNotice] = useState<string | null>(null);
  const reshape = (next: ActionBinding[]) => { setNotice(null); setCollapsed(carryCollapsed(bindings, next, collapsed)); onChange(next); };

  // Drag and drop: the step (or whole If block) being dragged and the insertion point under the pointer. The up/down buttons stay as the keyboard way to move.
  const [drag, setDrag] = useState<number | null>(null);
  const [over, setOver] = useState<number | null>(null);
  const dragEnd = drag === null ? -1 : rows[drag]?.kind === "if" ? (rows[drag]?.endIndex ?? drag) : drag;
  const canDrop = (pos: number) => drag !== null && moveTo(bindings, drag, pos) !== bindings;
  const startDrag = (index: number) => (e: React.DragEvent) => {
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", String(index));
    const card = (e.currentTarget as HTMLElement).closest(".pf-card");
    if (card) e.dataTransfer.setDragImage(card, 8, 8);
    // Drawing the drop zones in the same tick would cancel the drag in some browsers.
    window.setTimeout(() => setDrag(index), 0);
  };
  const stopDrag = () => { setDrag(null); setOver(null); };
  const grip = (index: number) => (
    <span className="pf-grip" draggable onDragStart={startDrag(index)} onDragEnd={stopDrag} title={t("action.drag")} aria-hidden="true"><GripVertical size={14} /></span>
  );

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

  // An If that would nest too deep is refused by insertAt (same list back); say so instead of doing nothing.
  const insertBinding = (pos: number) => (type: string) => {
    const next = insertAt(bindings, pos, type);
    if (next === bindings) setNotice(t("action.logic.tooDeep", String(MAX_DEPTH)));
    else reshape(next);
  };

  const removeBinding = (index: number) => reshape(removeRow(bindings, index));

  const move = (index: number, dir: -1 | 1) => {
    const next = moveRow(bindings, index, dir);
    if (next !== bindings) reshape(next);
  };

  const toggleCollapsed = (index: number) =>
    setCollapsed((prev) => { const next = new Set(prev); if (!next.delete(index)) next.add(index); return next; });

  // Otherwise and End are never picked by hand (an If brings them); the picker of an existing row does not offer If either.
  // The old all-in-one Set variable action still runs but is replaced by four small ones, so it is no longer offered.
  const addable = actions.filter((a) => a.type !== FLOW_ELSE && a.type !== FLOW_END && a.type !== LEGACY_SET_VARIABLE);
  const replaceable = addable.filter((a) => a.type !== FLOW_IF);

  /** The point before row `pos`: a quiet "add here" plus, or a labelled drop zone while dragging. A block body with nothing in it shows a placeholder instead. */
  const slot = (pos: number) => {
    const stack = stacks[pos] ?? [];
    const lane = stack[stack.length - 1];
    const before = rows[pos - 1];
    const next = rows[pos];
    const emptyBody = before !== undefined && (before.kind === "if" || before.kind === "else") && !before.stray && next !== undefined && (next.kind === "else" || next.kind === "end");
    const droppable = canDrop(pos);
    const label = lane === "if" ? t("action.drop.insideIf") : lane === "else" ? t("action.drop.insideElse") : t("action.drop.here");
    const state = `${droppable ? " drop" : ""}${droppable && over === pos ? " over" : ""}`;
    const dropProps = droppable
      ? {
          onDragOver: (e: React.DragEvent) => { e.preventDefault(); e.dataTransfer.dropEffect = "move"; if (over !== pos) setOver(pos); },
          onDragLeave: () => setOver((o) => (o === pos ? null : o)),
          onDrop: (e: React.DragEvent) => { e.preventDefault(); if (drag !== null) reshape(moveTo(bindings, drag, pos)); stopDrag(); },
        }
      : {};
    const picker = (render: (open: () => void) => React.ReactNode) => <ActionPicker actions={addable} onPick={insertBinding(pos)} renderTrigger={render} />;
    if (emptyBody) {
      return (
        <div key={`slot-${pos}`} style={railStyle(stack)} {...dropProps}>
          <div className={`pf-empty${state}`}>
            <span className="pf-empty-text">{droppable ? label : t(lane === "else" ? "action.logic.emptyElse" : "action.logic.emptyIf")}</span>
            {!droppable && picker((open) => <button type="button" className="pf-add pf-empty-add" onClick={open} disabled={addable.length === 0}>+ {t(lane === "else" ? "action.logic.addInElse" : "action.logic.addInIf")}</button>)}
          </div>
        </div>
      );
    }
    // The end of a lane and the point right under a block get a labelled button, so it is clear whether a step lands inside the block or after it.
    const endOfLane = !droppable && before !== undefined && !before.stray && next !== undefined && (next.kind === "else" || next.kind === "end") && before.kind !== "if" && before.kind !== "else";
    const belowBlock = !droppable && before?.kind === "end" && !before.stray;
    if (endOfLane || belowBlock) {
      return (
        <div key={`slot-${pos}`} style={{ ...railStyle(stack), paddingTop: 4, paddingBottom: 4, display: "flex" }}>
          {picker((open) => <button type="button" className={`pf-add${belowBlock ? " outside" : ""}`} onClick={open} disabled={addable.length === 0}>+ {t(belowBlock ? "action.logic.addBelowIf" : lane === "else" ? "action.logic.addInElse" : "action.logic.addInIf")}</button>)}
        </div>
      );
    }
    return (
      <div key={`slot-${pos}`} className={`pf-slot${stack.length === 0 ? " quiet" : ""}${state}`} style={railStyle(stack)} {...dropProps}>
        <div className="pf-slot-inner">
          {droppable
            ? <span className="pf-slot-label">{label}</span>
            : picker((open) => <button type="button" className="pf-slot-btn" onClick={open} disabled={addable.length === 0} title={t("action.addHere")} aria-label={t("action.addHere")}>+</button>)}
        </div>
      </div>
    );
  };

  return (
    <div className="pf-cards pf-flow">
      {bindings.length === 0 && <p className="pf-hint">{t("action.none")}</p>}

      {rows.map((row) => {
        const { index } = row;
        const binding = bindings[index]!;
        if (hidden(index)) return null;
        const wrap = (node: React.ReactNode) => (
          <div key={index} className={drag !== null && index >= drag && index <= dragEnd ? "pf-flow-row pf-dim" : "pf-flow-row"} style={railStyle(stacks[index] ?? [])}>{node}</div>
        );
        const actionInfo = actions.find((a) => a.type === binding.type);
        const Form = formFor(binding.type, actionInfo);
        const frame = {
          borderColor: outlined === index ? "var(--ms-accent)" : undefined,
          outline: outlined === index ? "2px solid var(--ms-accent)" : undefined,
        };
        const setRef = (el: HTMLDivElement | null) => { rowRefs.current[index] = el; };

        let body: React.ReactNode;
        if (row.kind === "end") {
          body = <EndCap setRef={setRef} frame={frame} summary={endSummary(row)} stray={row.stray} canAddElse={!row.stray && endHasNoElse(row)} onAddElse={() => reshape(addElse(bindings, ownerOf(row)!.index))} onRemove={() => removeBinding(index)} />;
        } else if (row.kind === "else") {
          body = <ElseRow setRef={setRef} frame={frame} onRemove={() => removeBinding(index)} />;
        } else if (row.kind === "if") {
          body = (
            <IfRow
              row={row}
              binding={binding}
              bindings={bindings}
              collapsed={collapsed.has(index)}
              frame={frame}
              setRef={setRef}
              grip={grip(index)}
              variableCatalog={withoutSelf(variableCatalog)}
              onToggle={() => toggleCollapsed(index)}
              onChange={(settings) => updateBinding(index, { settings })}
              onClose={() => reshape(closeIf(bindings, index))}
              onMove={(dir) => move(index, dir)}
              canMove={(dir) => moveRow(bindings, index, dir) !== bindings}
              onRemove={() => removeBinding(index)}
            />
          );
        } else {
          body = (
            <div ref={setRef} className="pf-card" style={frame}>
              <div className="pf-card-head">
                {grip(index)}
                <span className="pf-card-num" aria-hidden="true">{index + 1}</span>
                <ActionPicker
                  actions={replaceable}
                  onPick={(type) => updateBinding(index, { type, settings: {} })}
                  renderTrigger={(open) => (
                    <button type="button" className="ghost grow pf-card-name" onClick={open}>
                      {actionInfo ? catalogText.actionName(actionInfo) : (
                        <span title={t("action.unavailable")} className="pf-row">
                          <CircleAlert size={13} className="pf-danger" />
                          {binding.type}
                        </span>
                      )}
                    </button>
                  )}
                />
                {bindings.length > 1 && (
                  <>
                    <button className="ghost pf-icon-btn small" onClick={() => move(index, -1)} disabled={moveRow(bindings, index, -1) === bindings} title={t("action.moveUp")} aria-label={t("action.moveUp")}><ChevronUp size={14} /></button>
                    <button className="ghost pf-icon-btn small" onClick={() => move(index, 1)} disabled={moveRow(bindings, index, 1) === bindings} title={t("action.moveDown")} aria-label={t("action.moveDown")}><ChevronDown size={14} /></button>
                  </>
                )}
                <button className="ghost pf-icon-btn small" onClick={() => removeBinding(index)} title={t("action.remove")} aria-label={t("action.remove")}><X size={14} /></button>
              </div>
              <div className="pf-card-body">
                <Form binding={binding} pages={pages} profiles={profiles} actionInfo={actionInfo} variableCatalog={withoutSelf(variableCatalog)} widgetIcon={widgetIcon} onChange={(settings) => updateBinding(index, { settings })} />
              </div>
            </div>
          );
        }
        return [slot(index), wrap(body)];
      })}

      {slot(bindings.length)}
      {rows[rows.length - 1]?.kind !== "end" && <ActionPicker
        actions={addable}
        onPick={insertBinding(bindings.length)}
        renderTrigger={(open) => <button type="button" className="ghost pf-btn small" onClick={open} disabled={addable.length === 0}>{t("action.add")}</button>}
      />}
      {notice && <p className="pf-hint error" role="alert" style={{ position: "sticky", bottom: 0, padding: "4px 0", background: "var(--ms-bg-surface, var(--ms-bg-canvas))" }}>{notice}</p>}
    </div>
  );
}
