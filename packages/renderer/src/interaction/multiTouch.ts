/**
 * Tracks how many fingers are on the screen, so a widget can tell a tap or a drag from a two-finger gesture
 * (the deck changes page with a two-finger swipe). The listeners sit on `window` in the capture phase, so they run
 * before any widget handler and still see events a widget stops from bubbling.
 */

const down = new Set<number>();
let epoch = 0;
let installed = false;

function add(event: PointerEvent): void {
  if (event.pointerType !== "touch") return;
  down.add(event.pointerId);
  if (down.size >= 2) epoch += 1;
}

function remove(event: PointerEvent): void {
  down.delete(event.pointerId);
}

/** Forgets every finger, so a lost pointerup can never leave the deck stuck in the multi-touch state. */
function reset(): void {
  down.clear();
}

export function installMultiTouchTracking(): void {
  if (installed || typeof window === "undefined") return;
  installed = true;
  const options = { capture: true, passive: true } as const;
  window.addEventListener("pointerdown", add, options);
  window.addEventListener("pointerup", remove, options);
  window.addEventListener("pointercancel", remove, options);
  const resetWhenAllUp = (event: TouchEvent) => {
    if (event.touches.length === 0) reset();
  };
  window.addEventListener("touchend", resetWhenAllUp, options);
  window.addEventListener("touchcancel", resetWhenAllUp, options);
  window.addEventListener("blur", reset);
}

/** True while two or more fingers are down. */
export function isMultiTouch(): boolean {
  return down.size >= 2;
}

/** Grows every time a second finger lands. A gesture that saw it change was (or became) a multi-touch gesture. */
export function multiTouchEpoch(): number {
  return epoch;
}

installMultiTouchTracking();
