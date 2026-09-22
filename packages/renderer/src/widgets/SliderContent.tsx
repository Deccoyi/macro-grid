import type { ChangeEvent, PointerEvent as ReactPointerEvent } from "react";
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

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => onChange?.(Number(e.target.value));
  const handleCommit = (e: ReactPointerEvent<HTMLInputElement>) => onCommit?.(Number((e.target as HTMLInputElement).value));

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
        // Stops the widget's own press/long-press gesture from firing while the user is only dragging the thumb.
        onPointerDown={(e) => e.stopPropagation()}
        onChange={handleChange}
        onPointerUp={handleCommit}
      />
    </div>
  );
}
