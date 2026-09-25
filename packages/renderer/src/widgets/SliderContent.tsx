import { useRef, type ChangeEvent, type PointerEvent as ReactPointerEvent } from "react";
import { isMultiTouch, multiTouchEpoch } from "../interaction/multiTouch";
import { numberProp } from "./numberProp";

export interface SliderContentProps {
  text?: string;
  /** Current value; falls back to props.value, then props.min, then 0. */
  value?: number;
  props?: Record<string, unknown>;
  /** Fires continuously while dragging (for a live local preview; not every tick needs to hit the wire). */
  onChange?: (value: number) => void;
  /** Fires once the drag ends — this is what should actually send `widget.value` to the server. */
  onCommit?: (value: number) => void;
}

export function SliderContent({ text, value, props, onChange, onCommit }: SliderContentProps) {
  const min = numberProp(props, "min", 0);
  const max = numberProp(props, "max", 100);
  const step = numberProp(props, "step", 1);
  const current = value ?? numberProp(props, "value", min);

  // A second finger during the drag (the deck's two-finger page swipe) is not a slider drag: the value goes back to
  // where it was and nothing is sent.
  const drag = useRef<{ startValue: number; startEpoch: number; multi: boolean } | null>(null);

  const handleDown = (e: ReactPointerEvent<HTMLInputElement>) => {
    // Stops the widget's own press/long-press gesture from firing while the user is only dragging the thumb.
    e.stopPropagation();
    drag.current = { startValue: current, startEpoch: multiTouchEpoch(), multi: isMultiTouch() };
  };
  const handleChange = (e: ChangeEvent<HTMLInputElement>) => onChange?.(Number(e.target.value));
  const endDrag = (commit: boolean, e: ReactPointerEvent<HTMLInputElement>) => {
    const start = drag.current;
    drag.current = null;
    if (start && (!commit || start.multi || multiTouchEpoch() !== start.startEpoch)) {
      onChange?.(start.startValue);
      return;
    }
    onCommit?.(Number((e.target as HTMLInputElement).value));
  };

  return (
    <div className="ms-content ms-slider">
      {text && <span className="ms-text">{text}</span>}
      <input
        type="range"
        className="ms-range"
        min={min}
        max={max}
        step={step}
        value={current}
        onPointerDown={handleDown}
        onChange={handleChange}
        onPointerUp={(e) => endDrag(true, e)}
        onPointerCancel={(e) => endDrag(false, e)}
      />
    </div>
  );
}
