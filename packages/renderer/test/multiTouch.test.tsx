import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePressGesture, PRESS_CONFIRM_MS, type PressGestureOptions } from "../src/interaction/usePressGesture";
import { KnobContent } from "../src/widgets/KnobContent";
import { SliderContent } from "../src/widgets/SliderContent";

function TestButton(options: PressGestureOptions) {
  const gesture = usePressGesture(options);
  return (
    <div
      data-testid="btn"
      onPointerDown={gesture.onPointerDown as any}
      onPointerUp={gesture.onPointerUp as any}
      onPointerCancel={gesture.onPointerCancel as any}
    />
  );
}

const touch = (pointerId: number) => ({ pointerId, pointerType: "touch" });

/** Lifts every finger the test put down, so the global finger count starts at zero in the next test. */
const liftedFingers = new Set<number>();
function down(el: Element, pointerId: number) {
  liftedFingers.add(pointerId);
  fireEvent.pointerDown(el, touch(pointerId));
}
function up(el: Element, pointerId: number) {
  liftedFingers.delete(pointerId);
  fireEvent.pointerUp(el, touch(pointerId));
}

// jsdom has no pointer capture; the knob calls it on pointer down.
Object.defineProperty(Element.prototype, "setPointerCapture", { value: () => {}, writable: true, configurable: true });

/** A slider that follows its own changes, like the deck does while dragging. */
function StatefulSlider({ start, onCommit, onChange }: { start: number; onCommit?: (v: number) => void; onChange?: (v: number) => void }) {
  const [value, setValue] = useState(start);
  return (
    <SliderContent
      value={value}
      onChange={(v) => {
        setValue(v);
        onChange?.(v);
      }}
      onCommit={onCommit}
    />
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(performance, "now").mockImplementation(() => Date.now());
});

afterEach(() => {
  for (const id of liftedFingers) fireEvent.pointerUp(document.body, touch(id));
  liftedFingers.clear();
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("a touch press", () => {
  it("fires after the confirmation window and releases on pointer up", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    render(<TestButton onPress={onPress} onRelease={onRelease} />);
    const el = screen.getByTestId("btn");

    down(el, 1);
    expect(onPress).not.toHaveBeenCalled();
    vi.advanceTimersByTime(PRESS_CONFIRM_MS + 1);
    expect(onPress).toHaveBeenCalledTimes(1);

    up(el, 1);
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it("fires press and release together for a tap shorter than the window", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    render(<TestButton onPress={onPress} onRelease={onRelease} />);
    const el = screen.getByTestId("btn");

    down(el, 1);
    up(el, 1);

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it("is dropped when a second finger lands inside the window (a two-finger swipe over a button)", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    const onLongPress = vi.fn();
    render(<TestButton onPress={onPress} onRelease={onRelease} onLongPress={onLongPress} />);
    const el = screen.getByTestId("btn");

    down(el, 1);
    vi.advanceTimersByTime(10);
    down(document.body, 2);
    vi.advanceTimersByTime(1000);
    up(el, 1);
    up(document.body, 2);

    expect(onPress).not.toHaveBeenCalled();
    expect(onRelease).not.toHaveBeenCalled();
    expect(onLongPress).not.toHaveBeenCalled();
  });

  it("is dropped when the other finger is already down", () => {
    const onPress = vi.fn();
    render(<TestButton onPress={onPress} />);
    const el = screen.getByTestId("btn");

    down(document.body, 2);
    down(el, 1);
    vi.advanceTimersByTime(1000);
    up(el, 1);
    up(document.body, 2);

    expect(onPress).not.toHaveBeenCalled();
  });

  it("is dropped when the second finger lands and the first is lifted inside the window", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    render(<TestButton onPress={onPress} onRelease={onRelease} />);
    const el = screen.getByTestId("btn");

    down(el, 1);
    down(document.body, 2);
    up(el, 1);
    up(document.body, 2);
    vi.advanceTimersByTime(1000);

    expect(onPress).not.toHaveBeenCalled();
    expect(onRelease).not.toHaveBeenCalled();
  });

  it("works again after a two-finger gesture", () => {
    const onPress = vi.fn();
    render(<TestButton onPress={onPress} />);
    const el = screen.getByTestId("btn");

    down(el, 1);
    down(document.body, 2);
    up(el, 1);
    up(document.body, 2);

    down(el, 3);
    up(el, 3);
    expect(onPress).toHaveBeenCalledTimes(1);
  });
});

describe("a mouse press", () => {
  it("still fires at once", () => {
    const onPress = vi.fn();
    render(<TestButton onPress={onPress} />);

    fireEvent.pointerDown(screen.getByTestId("btn"), { pointerId: 1, pointerType: "mouse" });

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});

describe("slider and knob during a two-finger gesture", () => {
  it("a slider that a second finger touched sends nothing and goes back to its value", () => {
    const onChange = vi.fn();
    const onCommit = vi.fn();
    const { container } = render(<StatefulSlider start={30} onChange={onChange} onCommit={onCommit} />);
    const range = container.querySelector("input")!;

    down(range, 1);
    down(document.body, 2);
    fireEvent.change(range, { target: { value: "80" } });
    up(range, 1);
    up(document.body, 2);

    expect(onCommit).not.toHaveBeenCalled();
    expect(onChange).toHaveBeenLastCalledWith(30);
  });

  it("a normal one-finger slider drag still commits", () => {
    const onCommit = vi.fn();
    const { container } = render(<StatefulSlider start={30} onCommit={onCommit} />);
    const range = container.querySelector("input")!;

    down(range, 1);
    fireEvent.change(range, { target: { value: "80" } });
    up(range, 1);

    expect(onCommit).toHaveBeenCalledWith(80);
  });

  it("a knob that a second finger touched sends nothing and goes back to its value", () => {
    const onChange = vi.fn();
    const onCommit = vi.fn();
    const { container } = render(<KnobContent value={40} onChange={onChange} onCommit={onCommit} />);
    const dial = container.querySelector("svg")!;

    down(dial, 1);
    down(document.body, 2);
    up(dial, 1);
    up(document.body, 2);

    expect(onCommit).not.toHaveBeenCalled();
    expect(onChange).toHaveBeenLastCalledWith(40);
  });
});
