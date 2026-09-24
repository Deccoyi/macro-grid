import { useRef, type MouseEvent } from "react";

/**
 * Backdrop props that close a modal only when both mousedown and mouseup
 * landed on the backdrop itself, so a text-selection drag that starts inside
 * the modal and ends outside doesn't dismiss it.
 */
export function useBackdropClose(onClose: () => void) {
  const downOnBackdrop = useRef(false);
  return {
    onMouseDown: (e: MouseEvent) => { downOnBackdrop.current = e.target === e.currentTarget; },
    onClick: (e: MouseEvent) => {
      if (downOnBackdrop.current && e.target === e.currentTarget) onClose();
      downOnBackdrop.current = false;
    },
  };
}
