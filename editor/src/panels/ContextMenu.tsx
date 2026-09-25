import { useEffect, useRef } from "react";
import type { ReactNode } from "react";

export interface ContextMenuItem {
  label: string;
  icon?: ReactNode;
  danger?: boolean;
  disabled?: boolean;
  onSelect: () => void;
}

interface ContextMenuProps {
  x: number;
  y: number;
  items: ContextMenuItem[];
  onClose: () => void;
}

/** A small right-click menu, styled like the rest of the app chrome (flat, thin border, no shadow-heavy
 * "web" look) — see docs/ui/ui-guidelines.md. Closes on outside click, Escape, or scroll. */
export function ContextMenu({ x, y, items, onClose }: ContextMenuProps) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handlePointer = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("mousedown", handlePointer, true);
    window.addEventListener("keydown", handleKey);
    window.addEventListener("scroll", onClose, true);
    return () => {
      window.removeEventListener("mousedown", handlePointer, true);
      window.removeEventListener("keydown", handleKey);
      window.removeEventListener("scroll", onClose, true);
    };
  }, [onClose]);

  // Keep the menu on-screen near the click point without overflowing the viewport edge.
  const left = Math.min(x, window.innerWidth - 200);
  const top = Math.min(y, window.innerHeight - items.length * 30 - 16);

  return (
    <div
      ref={ref}
      style={{
        position: "fixed",
        left,
        top,
        minWidth: 180,
        background: "var(--ms-bg-surface-raised)",
        border: "1px solid var(--ms-border)",
        borderRadius: 6,
        boxShadow: "0 8px 24px rgba(0,0,0,.4)",
        padding: 4,
        zIndex: 100,
        display: "flex",
        flexDirection: "column",
      }}
    >
      {items.map((item, i) => (
        <button
          key={i}
          className="ctx-menu-item"
          disabled={item.disabled}
          onClick={() => { item.onSelect(); onClose(); }}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            padding: "6px 8px",
            border: "none",
            background: "transparent",
            borderRadius: 4,
            fontSize: 12.5,
            color: item.danger ? "var(--ms-danger)" : "var(--ms-text-primary)",
            justifyContent: "flex-start",
          }}
        >
          {item.icon}
          {item.label}
        </button>
      ))}
    </div>
  );
}
