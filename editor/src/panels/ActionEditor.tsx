import { useEffect, useRef, useState, type ElementType } from "react";
import { CircleAlert, ChevronUp, ChevronDown, ChevronRight, Circle, CircleDot, SlidersHorizontal, Timer, ToggleLeft, ToggleRight, X } from "lucide-react";
import type { ActionBinding, ConditionNode, Page, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { useCatalogText } from "../i18n/catalogText";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import { ActionPicker } from "./ActionPicker";
import { formFor } from "./actionForms/forms";
import { FLOW_ELSE, FLOW_END, FLOW_IF, addElse, addIf, layout, move as moveRow, remove as removeRow, type FlowRow } from "./actionFlow";
import { IfStepEditor, whenOf } from "./IfStepEditor";
import { summarizeCondition } from "./dynamic/conditionSummary";

interface ActionEditorProps {
  widget: Widget;
  actions: ActionInfo[];
  pages: Page[];
  profiles: ProfileSummary[];
  variableCatalog: VariableInfo[];
  onChange: (event: WidgetEventName, bindings: ActionBinding[]) => void;
}

/** Two filled dots — lucide has no "double tap" glyph, so this stays a small hand-drawn icon in the
 * same stroke-icon family (see the other event icons, all lucide). */
function DoubleTapIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor">
      <circle cx="8" cy="12" r="3" />
      <circle cx="16" cy="12" r="3" />
    </svg>
  );
}

const BUTTON_EVENTS: { event: WidgetEventName; key: DictKey; icon: ElementType }[] = [
  { event: "press", key: "action.event.press", icon: CircleDot },
  { event: "release", key: "action.event.release", icon: Circle },
  { event: "longPress", key: "action.event.longPress", icon: Timer },
  { event: "doubleTap", key: "action.event.doubleTap", icon: DoubleTapIcon },
];

const TOGGLE_EVENTS: { event: WidgetEventName; key: DictKey; icon: ElementType }[] = [
  { event: "toggleOn", key: "action.event.toggleOn", icon: ToggleRight },
  { event: "toggleOff", key: "action.event.toggleOff", icon: ToggleLeft },
];

const VALUE_EVENTS: { event: WidgetEventName; key: DictKey; icon: ElementType }[] = [
  { event: "valueChange", key: "action.event.valueChange", icon: SlidersHorizontal },
];

/** The "This button" variables work only in the button's own text and rules, so an action's fields do not offer them. */
function withoutSelf(catalog: VariableInfo[]): VariableInfo[] {
  return catalog.filter((v) => !v.name.toLowerCase().startsWith("self."));
}

export function ActionEditor({ widget, actions, pages, profiles, variableCatalog, onChange }: ActionEditorProps) {
  const { t } = useT();
  const catalogText = useCatalogText();
  const events =
    widget.type === "toggle" ? TOGGLE_EVENTS
    : widget.type === "slider" || widget.type === "knob" ? VALUE_EVENTS
    : BUTTON_EVENTS;
  const [activeEvent, setActiveEvent] = useState<WidgetEventName>(events[0]!.event);
  const bindings = widget.actions[activeEvent] ?? [];

  // "Go to widget" from the Error List: show that event, scroll the action into view and outline it once.
  const { focusAction, setFocusAction } = useWorkspaceUi();
  const [outlined, setOutlined] = useState<number | null>(null);
  const rowRefs = useRef<(HTMLDivElement | null)[]>([]);
  useEffect(() => {
    if (!focusAction || focusAction.widgetId !== widget.id) return;
    if (events.some((e) => e.event === focusAction.event) && activeEvent !== focusAction.event) {
      setActiveEvent(focusAction.event as WidgetEventName);
      return; // the rows of that event render next; this effect runs again with the right one
    }
    rowRefs.current[focusAction.index]?.scrollIntoView({ block: "nearest" });
    setOutlined(focusAction.index);
    setFocusAction(null);
    const timer = window.setTimeout(() => setOutlined(null), 2000);
    return () => window.clearTimeout(timer);
  }, [focusAction, widget.id, activeEvent, events, setFocusAction]);

  // Which If blocks are drawn collapsed. Only a view state: it is dropped whenever the list changes shape.
  const [collapsed, setCollapsed] = useState<Set<number>>(new Set());
  const rows = layout(bindings);
  const hidden = (index: number) => rows.some((r) => r.kind === "if" && collapsed.has(r.index) && index > r.index && index <= (r.endIndex ?? bindings.length - 1));
  const reshape = (next: ActionBinding[]) => { setCollapsed(new Set()); onChange(activeEvent, next); };

  // A row focused from the Error List opens the blocks that hide it.
  useEffect(() => {
    if (!focusAction || focusAction.widgetId !== widget.id) return;
    setCollapsed((prev) => {
      const next = new Set([...prev].filter((c) => !(focusAction.index > c && focusAction.index <= (rows[c]?.endIndex ?? bindings.length - 1))));
      return next.size === prev.size ? prev : next;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focusAction]);

  const updateBinding = (index: number, next: Partial<ActionBinding>) => {
    const copy = bindings.map((b, i) => (i === index ? { ...b, ...next } : b));
    onChange(activeEvent, copy);
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

  const hasLongOrDouble = (widget.actions.longPress?.length ?? 0) > 0 || (widget.actions.doubleTap?.length ?? 0) > 0;
  const hasPressOrRelease = (widget.actions.press?.length ?? 0) > 0 || (widget.actions.release?.length ?? 0) > 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      {/* Always 4 columns (the button's event count), regardless of how many events this widget type
         has — so a slider's single "Value changed" cell or a toggle's two cells are exactly the same size
         as a button's, instead of stretching to fill the row. */}
      <div style={{ display: "grid", gridTemplateColumns: `repeat(${BUTTON_EVENTS.length}, minmax(0, 1fr))`, gap: 6 }}>
        {events.map((e) => {
          const Icon = e.icon;
          const bound = (widget.actions[e.event]?.length ?? 0) > 0;
          return (
            <button
              key={e.event}
              type="button"
              className={activeEvent === e.event ? "action-event-cell on" : "action-event-cell"}
              title={t(e.key)}
              onClick={() => setActiveEvent(e.event)}
            >
              <Icon size={16} />
              <span className="cap">{t(e.key)}</span>
              {bound && <span className="dot" />}
            </button>
          );
        })}
      </div>

      {widget.type !== "toggle" && hasLongOrDouble && hasPressOrRelease && (
        <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 4, padding: "6px 8px" }}>
          {t("action.warning.longDouble")}
        </div>
      )}

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
  const { t } = useT();
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
            {summary} · {t("action.logic.steps", String(stepCount))}
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
