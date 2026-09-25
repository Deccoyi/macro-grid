import { useCallback, useRef } from "react";
import { isMultiTouch, multiTouchEpoch } from "./multiTouch";

export interface PressGestureOptions {
  /** Fires on pointer down — this is what a plain button press binds to. A finger waits PRESS_CONFIRM_MS first (see below). */
  onPress?: () => void;
  /** Fires on pointer up, whenever onPress also fired (mirrors the down/up pair sent over the wire). */
  onRelease?: () => void;
  /** Fires once, in addition to onPress/onRelease, if the pointer stays down past longPressMs. */
  onLongPress?: () => void;
  /**
   * Fires in addition to onPress/onRelease when a second tap lands within doubleTapMs of the first.
   * Both taps still fire their own onPress/onRelease — doubleTap is an extra event, not a replacement.
   */
  onDoubleTap?: () => void;
  longPressMs?: number;
  doubleTapMs?: number;
  /** Vibrates on press/long-press when the platform supports it. Default true. */
  haptics?: boolean;
}

export interface PressGestureHandlers {
  onPointerDown: (event: PointerEvent) => void;
  onPointerUp: (event: PointerEvent) => void;
  onPointerCancel: (event: PointerEvent) => void;
}

const DEFAULT_LONG_PRESS_MS = 500;
const DEFAULT_DOUBLE_TAP_MS = 300;
/**
 * A touch press is held back this long. The two fingers of a page swipe never land at exactly the same moment, so
 * the first one may be on a button; if a second finger arrives inside this window the press is dropped instead of
 * triggering the button. A mouse or pen press fires at once, and a tap released before the window ends fires on release.
 */
export const PRESS_CONFIRM_MS = 45;

export function usePressGesture(options: PressGestureOptions): PressGestureHandlers {
  const optionsRef = useRef(options);
  optionsRef.current = options;

  const longPressTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const confirmTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const longPressFired = useRef(false);
  const lastReleaseAt = useRef(0);
  const isDown = useRef(false);
  /** onPress has been called for the current touch. */
  const pressFired = useRef(false);
  /** A second finger made this a multi-touch gesture: nothing of it may reach the widget's actions. */
  const dropped = useRef(false);
  const downEpoch = useRef(0);

  const vibrate = (ms: number) => {
    if (optionsRef.current.haptics === false) return;
    navigator.vibrate?.(ms);
  };

  const clearTimers = () => {
    if (longPressTimer.current !== null) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    if (confirmTimer.current !== null) {
      clearTimeout(confirmTimer.current);
      confirmTimer.current = null;
    }
  };

  const firePress = () => {
    pressFired.current = true;
    vibrate(15);
    optionsRef.current.onPress?.();
  };

  const onPointerDown = useCallback((event: PointerEvent) => {
    try {
      (event.target as Element | null)?.setPointerCapture?.(event.pointerId);
    } catch {
      // Browsers reject capture for a pointerId that isn't currently active (e.g. a synthetic event
      // in a test, or a stray call after the pointer already lifted). Must never abort the press itself.
    }
    clearTimers();
    isDown.current = true;
    longPressFired.current = false;
    pressFired.current = false;
    dropped.current = false;
    downEpoch.current = multiTouchEpoch();

    const touch = event.pointerType === "touch";
    if (touch && isMultiTouch()) {
      dropped.current = true; // another finger is already down: this is part of a two-finger gesture
      return;
    }
    if (touch) {
      confirmTimer.current = setTimeout(() => {
        confirmTimer.current = null;
        if (!isDown.current) return;
        if (multiTouchEpoch() !== downEpoch.current) {
          dropped.current = true; // a second finger landed inside the window
          clearTimers();
          return;
        }
        firePress();
      }, PRESS_CONFIRM_MS);
    } else {
      firePress();
    }

    if (optionsRef.current.onLongPress) {
      longPressTimer.current = setTimeout(() => {
        if (!isDown.current || dropped.current || !pressFired.current) return;
        longPressFired.current = true;
        vibrate(30);
        optionsRef.current.onLongPress?.();
      }, optionsRef.current.longPressMs ?? DEFAULT_LONG_PRESS_MS);
    }
  }, []);

  const onPointerUp = useCallback((_event: PointerEvent) => {
    if (!isDown.current) return;
    isDown.current = false;
    const confirming = confirmTimer.current !== null;
    clearTimers();
    if (dropped.current) return;

    if (confirming) {
      // Released before the confirmation window ended: a quick tap, unless a second finger came in meanwhile.
      if (multiTouchEpoch() !== downEpoch.current) {
        dropped.current = true;
        return;
      }
      firePress();
    }
    if (!pressFired.current) return;
    optionsRef.current.onRelease?.();

    if (!longPressFired.current && optionsRef.current.onDoubleTap) {
      const now = performance.now();
      const window = optionsRef.current.doubleTapMs ?? DEFAULT_DOUBLE_TAP_MS;
      if (now - lastReleaseAt.current <= window) {
        lastReleaseAt.current = 0;
        optionsRef.current.onDoubleTap();
      } else {
        lastReleaseAt.current = now;
      }
    }
  }, []);

  const onPointerCancel = useCallback((_event: PointerEvent) => {
    isDown.current = false;
    clearTimers();
  }, []);

  return { onPointerDown, onPointerUp, onPointerCancel };
}
