import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePressGesture, type PressGestureOptions } from "../src/interaction/usePressGesture";

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

function pointerDown() {
  const el = screen.getByTestId("btn");
  fireEvent.pointerDown(el, { pointerId: 1 });
  return el;
}

function pointerUp(el: HTMLElement) {
  fireEvent.pointerUp(el, { pointerId: 1 });
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(performance, "now").mockImplementation(() => Date.now());
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

describe("usePressGesture", () => {
  it("fires onPress on pointer down and onRelease on pointer up", () => {
    const onPress = vi.fn();
    const onRelease = vi.fn();
    render(<TestButton onPress={onPress} onRelease={onRelease} />);

    const el = pointerDown();
    expect(onPress).toHaveBeenCalledTimes(1);
    expect(onRelease).not.toHaveBeenCalled();

    pointerUp(el);
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it("fires onLongPress after the threshold while still held down", () => {
    const onLongPress = vi.fn();
    render(<TestButton onLongPress={onLongPress} longPressMs={500} />);

    pointerDown();
    vi.advanceTimersByTime(499);
    expect(onLongPress).not.toHaveBeenCalled();

    vi.advanceTimersByTime(2);
    expect(onLongPress).toHaveBeenCalledTimes(1);
  });

  it("does not fire onLongPress if released before the threshold", () => {
    const onLongPress = vi.fn();
    render(<TestButton onLongPress={onLongPress} longPressMs={500} />);

    const el = pointerDown();
    vi.advanceTimersByTime(200);
    pointerUp(el);
    vi.advanceTimersByTime(1000);

    expect(onLongPress).not.toHaveBeenCalled();
  });

  it("fires onDoubleTap when two taps land within the window, without suppressing onPress", () => {
    const onPress = vi.fn();
    const onDoubleTap = vi.fn();
    render(<TestButton onPress={onPress} onDoubleTap={onDoubleTap} doubleTapMs={300} />);

    const el = pointerDown();
    pointerUp(el);
    vi.advanceTimersByTime(100);
    pointerDown();
    pointerUp(el);

    expect(onPress).toHaveBeenCalledTimes(2);
    expect(onDoubleTap).toHaveBeenCalledTimes(1);
  });

  it("does not fire onDoubleTap when taps are too far apart", () => {
    const onDoubleTap = vi.fn();
    render(<TestButton onDoubleTap={onDoubleTap} doubleTapMs={300} />);

    const el = pointerDown();
    pointerUp(el);
    vi.advanceTimersByTime(400);
    pointerDown();
    pointerUp(el);

    expect(onDoubleTap).not.toHaveBeenCalled();
  });

  it("a long press does not also count as a double-tap candidate", () => {
    const onDoubleTap = vi.fn();
    render(<TestButton onLongPress={() => {}} onDoubleTap={onDoubleTap} longPressMs={500} doubleTapMs={300} />);

    const el = pointerDown();
    vi.advanceTimersByTime(500);
    pointerUp(el);
    pointerDown();
    pointerUp(el);

    expect(onDoubleTap).not.toHaveBeenCalled();
  });

  it("vibrates on press by default and skips it when haptics is false", () => {
    const vibrate = vi.spyOn(navigator, "vibrate");
    const { unmount } = render(<TestButton />);
    pointerDown();
    expect(vibrate).toHaveBeenCalled();
    unmount();

    vibrate.mockClear();
    render(<TestButton haptics={false} />);
    pointerDown();
    expect(vibrate).not.toHaveBeenCalled();
  });
});
