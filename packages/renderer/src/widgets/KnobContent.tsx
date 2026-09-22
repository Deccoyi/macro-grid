import { useRef, type PointerEvent as ReactPointerEvent } from "react";
import { clamp, numberProp } from "./numberProp";

export interface KnobContentProps {
  text?: string;
  value?: number;
  props?: Record<string, unknown>;
  onChange?: (value: number) => void;
  onCommit?: (value: number) => void;
}

/** Pixels of vertical drag needed to sweep the full min..max range — a relative ("endless") knob, like a hardware one. */
const DRAG_RANGE_PX = 200;

export function KnobContent({ text, value, props, onChange, onCommit }: KnobContentProps) {
  const min = numberProp(props, "min", 0);
  const max = numberProp(props, "max", 100);
  const current = clamp(value ?? numberProp(props, "value", min), min, max);
  const drag = useRef<{ startY: number; startValue: number } | null>(null);

  const fraction = max > min ? (current - min) / (max - min) : 0;
  const angleDeg = -135 + fraction * 270; // 270° sweep starting bottom-left, matches most hardware knobs

  const onPointerDown = (e: ReactPointerEvent<SVGSVGElement>) => {
    e.stopPropagation();
    e.currentTarget.setPointerCapture(e.pointerId);
    drag.current = { startY: e.clientY, startValue: current };
  };

  const onPointerMove = (e: ReactPointerEvent<SVGSVGElement>) => {
    if (!drag.current) return;
    const deltaY = drag.current.startY - e.clientY;
    const next = clamp(drag.current.startValue + (deltaY / DRAG_RANGE_PX) * (max - min), min, max);
    onChange?.(next);
  };

  const endDrag = () => {
    if (!drag.current) return;
    drag.current = null;
    onCommit?.(current);
  };

  return (
    <div className="ms-content ms-knob">
      <svg
        viewBox="0 0 100 100"
        className="ms-knob-dial"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      >
        <circle cx="50" cy="50" r="42" className="ms-knob-track" />
        <line x1="50" y1="50" x2="50" y2="14" className="ms-knob-needle" transform={`rotate(${angleDeg} 50 50)`} />
      </svg>
      {text && <span className="ms-text">{text}</span>}
    </div>
  );
}
