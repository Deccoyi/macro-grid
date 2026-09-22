import { useState } from "react";
import { ChevronUp, ChevronDown, X } from "lucide-react";
import type { ActionBinding, Page, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, ProfileSummary } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { formFor } from "./actionForms/forms";

export interface ActionEditorProps {
  widget: Widget;
  actions: ActionInfo[];
  pages: Page[];
  profiles: ProfileSummary[];
  onChange: (event: WidgetEventName, bindings: ActionBinding[]) => void;
}

const BUTTON_EVENTS: { event: WidgetEventName; key: DictKey }[] = [
  { event: "press", key: "action.event.press" },
  { event: "release", key: "action.event.release" },
  { event: "longPress", key: "action.event.longPress" },
  { event: "doubleTap", key: "action.event.doubleTap" },
];

const TOGGLE_EVENTS: { event: WidgetEventName; key: DictKey }[] = [
  { event: "toggleOn", key: "action.event.toggleOn" },
  { event: "toggleOff", key: "action.event.toggleOff" },
];

export function ActionEditor({ widget, actions, pages, profiles, onChange }: ActionEditorProps) {
  const { t } = useT();
  const events = widget.type === "toggle" ? TOGGLE_EVENTS : BUTTON_EVENTS;
  const [activeEvent, setActiveEvent] = useState<WidgetEventName>(events[0]!.event);
  const bindings = widget.actions[activeEvent] ?? [];

  const updateBinding = (index: number, next: Partial<ActionBinding>) => {
    const copy = bindings.map((b, i) => (i === index ? { ...b, ...next } : b));
    onChange(activeEvent, copy);
  };

  const addBinding = () => {
    const first = actions[0];
    if (!first) return;
    onChange(activeEvent, [...bindings, { type: first.type, settings: {} }]);
  };

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
      <div style={{ display: "flex", flexWrap: "wrap", gap: 4 }}>
        {events.map((e) => (
          <button
            key={e.event}
            className={activeEvent === e.event ? "active" : "ghost"}
            onClick={() => setActiveEvent(e.event)}
            style={{ padding: "4px 8px", fontSize: 12 }}
          >
            {t(e.key)}
            {(widget.actions[e.event]?.length ?? 0) > 0 ? " •" : ""}
          </button>
        ))}
      </div>

      {widget.type !== "toggle" && hasLongOrDouble && hasPressOrRelease && (
        <div style={{ fontSize: 11, color: "var(--ms-text-secondary)", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 4, padding: "6px 8px" }}>
          {t("action.warning.longDouble")}
        </div>
      )}

      {bindings.length === 0 && <div style={{ color: "var(--ms-text-secondary)", fontSize: 12 }}>{t("action.none")}</div>}

      {bindings.map((binding, index) => {
        const Form = formFor(binding.type);
        return (
          <div key={index} style={{ border: "1px solid var(--ms-border)", borderRadius: 4, padding: 8, display: "flex", flexDirection: "column", gap: 6 }}>
            <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
              <select
                value={binding.type}
                onChange={(e) => updateBinding(index, { type: e.target.value, settings: {} })}
                style={{ flex: 1 }}
              >
                {actions.map((a) => (
                  <option key={a.type} value={a.type}>{a.displayName}</option>
                ))}
              </select>
              {bindings.length > 1 && (
                <>
                  <button className="ghost" onClick={() => move(index, -1)} disabled={index === 0} title={t("action.moveUp")}><ChevronUp size={14} /></button>
                  <button className="ghost" onClick={() => move(index, 1)} disabled={index === bindings.length - 1} title={t("action.moveDown")}><ChevronDown size={14} /></button>
                </>
              )}
              <button className="ghost" onClick={() => removeBinding(index)} title={t("action.remove")}><X size={14} /></button>
            </div>
            <Form binding={binding} pages={pages} profiles={profiles} onChange={(settings) => updateBinding(index, { settings })} />
          </div>
        );
      })}

      <button className="ghost" onClick={addBinding} disabled={actions.length === 0} style={{ alignSelf: "flex-start" }}>
        {t("action.add")}
      </button>
    </div>
  );
}
