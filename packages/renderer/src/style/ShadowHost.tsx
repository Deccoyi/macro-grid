import { useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { createPortal } from "react-dom";

export interface ShadowHostProps {
  /** Base look derived from the widget's own Style fields; injected before customCss so the cascade favors the user's overrides. */
  baseCss?: string;
  /** Sanitized user CSS (see sanitizeWidgetCss); never pass raw, untrusted CSS here. */
  customCss?: string;
  /** Reflected as the `data-active` attribute on the host, so base/custom CSS can style it via `:host([data-active])`. */
  active?: boolean;
  className?: string;
  style?: CSSProperties;
  onPointerDown?: (event: PointerEvent) => void;
  onPointerUp?: (event: PointerEvent) => void;
  onPointerCancel?: (event: PointerEvent) => void;
  children: ReactNode;
}

/**
 * Renders `children` into a real shadow root so a widget's customCss can never leak out and
 * affect sibling widgets or the editor chrome, and so the editor's own styles can never leak in.
 *
 * Pointer handlers are attached with native addEventListener, not JSX props: React's synthetic
 * event system can invoke a JSX onPointerDown/Up/Cancel prop TWICE for one real pointer event when
 * the element is a portal's host and also sits at a shadow-root boundary (its retargeting logic
 * ends up walking the fiber tree via two paths to the same node). Going through the real DOM API
 * sidesteps that entirely — one native event, one call.
 */
export function ShadowHost({
  baseCss,
  customCss,
  active,
  className,
  style,
  onPointerDown,
  onPointerUp,
  onPointerCancel,
  children,
}: ShadowHostProps) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const [shadowRoot, setShadowRoot] = useState<ShadowRoot | null>(null);

  useLayoutEffect(() => {
    const host = hostRef.current;
    if (!host) return;
    setShadowRoot(host.shadowRoot ?? host.attachShadow({ mode: "open" }));
  }, []);

  useLayoutEffect(() => {
    const host = hostRef.current;
    if (!host) return;

    const down = (e: PointerEvent) => onPointerDown?.(e);
    const up = (e: PointerEvent) => onPointerUp?.(e);
    const cancel = (e: PointerEvent) => onPointerCancel?.(e);

    host.addEventListener("pointerdown", down);
    host.addEventListener("pointerup", up);
    host.addEventListener("pointercancel", cancel);
    return () => {
      host.removeEventListener("pointerdown", down);
      host.removeEventListener("pointerup", up);
      host.removeEventListener("pointercancel", cancel);
    };
  }, [onPointerDown, onPointerUp, onPointerCancel]);

  return (
    <div ref={hostRef} className={className} style={style} data-ms-widget-host="" data-active={active ? "" : undefined}>
      {shadowRoot &&
        createPortal(
          <>
            {baseCss && <style>{baseCss}</style>}
            {customCss && <style>{customCss}</style>}
            {children}
          </>,
          shadowRoot,
        )}
    </div>
  );
}
