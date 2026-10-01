import { useEffect, useRef, useState, type ElementType } from "react";
import { CircleAlert, ChevronUp, ChevronDown, Circle, CircleDot, SlidersHorizontal, Timer, ToggleLeft, ToggleRight, X } from "lucide-react";
import type { ActionBinding, Page, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { useCatalogText } from "../i18n/catalogText";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import { ActionPicker } from "./ActionPicker";
import { formFor } from "./actionForms/forms";

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

  const updateBinding = (index: number, next: Partial<ActionBinding>) => {
    const copy = bindings.map((b, i) => (i === index ? { ...b, ...next } : b));
    onChange(activeEvent, copy);
  };

  const addBinding = (type: string) => onChange(activeEvent, [...bindings, { type, settings: {} }]);

  const removeBinding = (index: number) => onChange(activeEvent, bindings.filter((_, i) => i !== index));

  const move = (index: number, dir: -1 | 1) => {
    const target = index + dir;
    if (target < 0 || target >= bindings.length) return;
    const copy = [...bindings];
    [copy[index], copy[target]] = [copy[target]!, copy[index]!];
    onChange(activeEvent, copy);
  };

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

      {bindings.map((binding, index) => {
        const actionInfo = actions.find((a) => a.type === binding.type);
        const Form = formFor(binding.type, actionInfo);
        return (
          <div
            key={index}
            ref={(el) => { rowRefs.current[index] = el; }}
            style={{ border: `1px solid ${outlined === index ? "var(--ms-accent)" : "var(--ms-border)"}`, outline: outlined === index ? "2px solid var(--ms-accent)" : undefined, borderRadius: 4, padding: 8, display: "flex", flexDirection: "column", gap: 6 }}
          >
            <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
              <ActionPicker
                actions={actions}
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
                  <button className="ghost" onClick={() => move(index, -1)} disabled={index === 0} title={t("action.moveUp")}><ChevronUp size={14} /></button>
                  <button className="ghost" onClick={() => move(index, 1)} disabled={index === bindings.length - 1} title={t("action.moveDown")}><ChevronDown size={14} /></button>
                </>
              )}
              <button className="ghost" onClick={() => removeBinding(index)} title={t("action.remove")}><X size={14} /></button>
            </div>
            <Form binding={binding} pages={pages} profiles={profiles} actionInfo={actionInfo} variableCatalog={withoutSelf(variableCatalog)} onChange={(settings) => updateBinding(index, { settings })} />
          </div>
        );
      })}

      <ActionPicker actions={actions} onPick={addBinding} />
    </div>
  );
}
