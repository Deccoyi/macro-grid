import { useEffect, useRef, useState, type ReactNode } from "react";

export interface DeviceSize {
  width: number;
  height: number;
}

interface DevicePreviewFrameProps {
  /** Target device size in CSS px, or null for "free" (fills the container, no frame/scaling). */
  size: DeviceSize | null;
  children: ReactNode;
}

/**
 * Renders children at a fixed device pixel size (so widget fontSize/spacing/gap look exactly as they
 * will on that device) then scales the whole frame down with a CSS transform to fit the available
 * editor space — the same trick browser devtools' device toolbar uses. Never scales up past 1x (a
 * device preview showing bigger than real life would be misleading). "Free" mode (size === null)
 * skips all of this and just renders children directly, like before this component existed.
 */
export function DevicePreviewFrame({ size, children }: DevicePreviewFrameProps) {
  const outerRef = useRef<HTMLDivElement | null>(null);
  const [scale, setScale] = useState(1);

  useEffect(() => {
    if (!size) return;
    const el = outerRef.current;
    if (!el) return;
    const update = () => {
      const bounds = el.getBoundingClientRect();
      const next = Math.min(bounds.width / size.width, bounds.height / size.height, 1);
      setScale(next > 0 ? next : 1);
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, [size]);

  if (!size) return <>{children}</>;

  return (
    <div ref={outerRef} style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}>
      <div
        style={{
          width: size.width * scale,
          height: size.height * scale,
          flexShrink: 0,
          position: "relative",
          overflow: "hidden",
          border: "1px solid var(--ms-border)",
          background: "var(--ms-bg-inset)",
          boxShadow: "0 12px 34px rgba(0,0,0,.35)",
        }}
      >
        <div style={{ width: size.width, height: size.height, transform: `scale(${scale})`, transformOrigin: "top left" }}>
          {children}
        </div>
      </div>
    </div>
  );
}
