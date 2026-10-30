import { useEffect, useState, type ElementType } from "react";
import { Circle, CircleDot, SlidersHorizontal, Timer, ToggleLeft, ToggleRight, TriangleAlert } from "lucide-react";
import type { ActionBinding, Page, Widget, WidgetEventName } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import { ActionList } from "./ActionList";

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

export function ActionEditor({ widget, actions, pages, profiles, variableCatalog, onChange }: ActionEditorProps) {
  const { t } = useT();
  const events =
    widget.type === "toggle" ? TOGGLE_EVENTS
    : widget.type === "slider" || widget.type === "knob" ? VALUE_EVENTS
    : BUTTON_EVENTS;
  const [activeEvent, setActiveEvent] = useState<WidgetEventName>(events[0]!.event);
  const bindings = widget.actions[activeEvent] ?? [];

  // "Go to widget" from the Error List: show that event, then hand the step to the list, which scrolls to it and outlines it once.
  const { focusAction, setFocusAction } = useWorkspaceUi();
  const [focusIndex, setFocusIndex] = useState<number | null>(null);
  useEffect(() => {
    if (!focusAction || focusAction.widgetId !== widget.id) return;
    if (events.some((e) => e.event === focusAction.event) && activeEvent !== focusAction.event) {
      setActiveEvent(focusAction.event as WidgetEventName);
      return; // the rows of that event render next; this effect runs again with the right one
    }
    setFocusIndex(focusAction.index);
    setFocusAction(null);
  }, [focusAction, widget.id, activeEvent, events, setFocusAction]);

  const hasLongOrDouble = (widget.actions.longPress?.length ?? 0) > 0 || (widget.actions.doubleTap?.length ?? 0) > 0;
  const hasPressOrRelease = (widget.actions.press?.length ?? 0) > 0 || (widget.actions.release?.length ?? 0) > 0;

  return (
    <>
      {/* Always 4 columns (the button's event count), regardless of how many events this widget type
         has — so a slider's single "Value changed" cell or a toggle's two cells are exactly the same size
         as a button's, instead of stretching to fill the row. More events wrap to further rows. */}
      <div className="pf-events">
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
        <div role="note" className="pf-note">
          <TriangleAlert size={14} />
          <span>{t("action.warning.longDouble")}</span>
        </div>
      )}

      <ActionList
        key={activeEvent}
        bindings={bindings}
        actions={actions}
        pages={pages}
        profiles={profiles}
        variableCatalog={variableCatalog}
        onChange={(next) => onChange(activeEvent, next)}
        focusIndex={focusIndex}
        onFocusDone={() => setFocusIndex(null)}
      />
    </>
  );
}
