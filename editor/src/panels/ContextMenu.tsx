import { useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { Check, ChevronRight } from "lucide-react";

export interface ContextMenuItem {
  label: string;
  icon?: ReactNode;
  danger?: boolean;
  disabled?: boolean;
  /** Renders a checkmark before the label (and icon, if any) — for a toggleable item like a View menu
   * entry. Leave undefined for a plain action item with no check column. */
  checked?: boolean;
  /** Shown right-aligned, dimmed — a command's first shortcut ("Ctrl+Z"), see commandItem(). */
  shortcut?: string;
  /** Omitted for a submenu-only row (opens `submenu` on hover, selects nothing itself). */
  onSelect?: () => void;
  /** A nested flyout, opened by hovering this row — e.g. View > Layouts > a saved layout list. */
  submenu?: ContextMenuEntry[];
}

/** A thin separator line between groups of items — e.g. Save/Delete above, the layout list below. */
export type ContextMenuEntry = ContextMenuItem | { divider: true };

interface ContextMenuProps {
  x: number;
  y: number;
  items: ContextMenuEntry[];
  onClose: () => void;
}

/** A small right-click/menu-bar menu, styled like the rest of the app chrome (flat, thin border, no
 * shadow-heavy "web" look) — see docs/ui/ui-guidelines.md. Closes on outside click, Escape, or scroll —
 * "outside" means outside every open level, including submenus, which is what `data-ctx-menu-root` on
 * each level's own container (rather than one ref) makes possible without threading refs through the
 * recursive submenu levels. */
export function ContextMenu({ x, y, items, onClose }: ContextMenuProps) {
  useEffect(() => {
    const handlePointer = (e: MouseEvent) => {
      if (e.target instanceof Element && e.target.closest("[data-ctx-menu-root]")) return;
      onClose();
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

  return <MenuLevel x={x} y={y} items={items} onSelect={onClose} />;
}

/** One menu panel — the root, or one level of nested submenu (rendered recursively via `submenu`). Every
 * level shares the same `onSelect` (the root's onClose): picking a leaf item anywhere in the tree closes
 * the whole thing, not just the level it was clicked in. */
function MenuLevel({ x, y, items, onSelect }: { x: number; y: number; items: ContextMenuEntry[]; onSelect: () => void }) {
  const [openSubmenu, setOpenSubmenu] = useState<{ index: number; x: number; y: number } | null>(null);
  const itemRefs = useRef<Record<number, HTMLButtonElement | null>>({});

  const left = Math.min(x, window.innerWidth - 220);
  const top = Math.min(y, window.innerHeight - items.length * 30 - 16);

  return (
    <div
      data-ctx-menu-root
      style={{
        position: "fixed",
        left,
        top,
        minWidth: 200,
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
      {items.map((entry, i) => {
        if ("divider" in entry) {
          return <div key={i} style={{ height: 1, background: "var(--ms-border)", margin: "4px 6px", flex: "0 0 auto" }} />;
        }
        const item = entry;
        const hasSubmenu = !!item.submenu && item.submenu.length > 0;
        return (
          <button
            key={i}
            ref={(el) => { itemRefs.current[i] = el; }}
            className="ctx-menu-item"
            disabled={item.disabled}
            onMouseEnter={() => {
              if (!hasSubmenu) { setOpenSubmenu(null); return; }
              const rect = itemRefs.current[i]?.getBoundingClientRect();
              if (rect) setOpenSubmenu({ index: i, x: rect.right, y: rect.top });
            }}
            onClick={() => {
              if (hasSubmenu) return; // opens on hover; the row itself selects nothing
              item.onSelect?.();
              onSelect();
            }}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              padding: "6px 8px",
              border: "none",
              background: openSubmenu?.index === i ? "var(--ms-bg-surface)" : "transparent",
              borderRadius: 4,
              fontSize: 12.5,
              color: item.danger ? "var(--ms-danger)" : "var(--ms-text-primary)",
              justifyContent: "flex-start",
              width: "100%",
            }}
          >
            {item.checked !== undefined && (
              <span style={{ width: 13, flex: "0 0 auto", display: "flex", alignItems: "center", justifyContent: "center" }}>
                {item.checked && <Check size={12} strokeWidth={2} />}
              </span>
            )}
            {item.icon}
            <span style={{ flex: 1, minWidth: 0, textAlign: "left", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {item.label}
            </span>
            {item.shortcut && (
              <span style={{ flex: "0 0 auto", opacity: 0.55, fontSize: 11, marginLeft: 8 }}>{item.shortcut}</span>
            )}
            {hasSubmenu && <ChevronRight size={12} strokeWidth={2} style={{ flex: "0 0 auto", opacity: 0.7 }} />}
          </button>
        );
      })}

      {openSubmenu && (() => {
        const entry = items[openSubmenu.index];
        return entry && !("divider" in entry) && entry.submenu ? (
          <MenuLevel x={openSubmenu.x} y={openSubmenu.y} items={entry.submenu} onSelect={onSelect} />
        ) : null;
      })()}
    </div>
  );
}
