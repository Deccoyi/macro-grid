import { useCallback, useRef } from "react";

export interface PressGestureOptions {
  /** Fires immediately on pointer down — this is what a plain button press binds to. */
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

export function usePressGesture(options: PressGestureOptions): PressGestureHandlers {
  const optionsRef = useRef(options);
  optionsRef.current = options;

  const longPressTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const longPressFired = useRef(false);
  const lastReleaseAt = useRef(0);
  const isDown = useRef(false);

  const vibrate = (ms: number) => {
    if (optionsRef.current.haptics === false) return;
    navigator.vibrate?.(ms);
  };

  const clearLongPressTimer = () => {
    if (longPressTimer.current !== null) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
  };

  const onPointerDown = useCallback((event: PointerEvent) => {
    try {
      (event.target as Element | null)?.setPointerCapture?.(event.pointerId);
    } catch {
      // Browsers reject capture for a pointerId that isn't currently active (e.g. a synthetic event
      // in a test, or a stray call after the pointer already lifted). Must never abort the press itself.
    }
    isDown.current = true;
    longPressFired.current = false;
    vibrate(15);
    optionsRef.current.onPress?.();

    if (optionsRef.current.onLongPress) {
      longPressTimer.current = setTimeout(() => {
        if (!isDown.current) return;
        longPressFired.current = true;
        vibrate(30);
        optionsRef.current.onLongPress?.();
      }, optionsRef.current.longPressMs ?? DEFAULT_LONG_PRESS_MS);
    }
  }, []);

  const onPointerUp = useCallback((_event: PointerEvent) => {
    if (!isDown.current) return;
    isDown.current = false;
    clearLongPressTimer();
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
    clearLongPressTimer();
  }, []);

  return { onPointerDown, onPointerUp, onPointerCancel };
}
