import { useEffect, useRef } from "react";
import { Pin } from "lucide-react";
import { useT } from "../i18n/I18nContext";
import { getPanelNode } from "./PanelHost";
import { toolWindowById } from "./toolWindows";

/** The slide-out panel an auto-hide rail tab opens — docs/design/docking-workspace.md ("Pin and
 * auto-hide"): sized as the panel was when docked, 1 px border-strong edge, no shadow, closes on Escape
 * or a click outside. Attaches the SAME portal node PanelHost created for this tool window (not a
 * remount), so its content's state survives moving in and out of auto-hide. */
export function AutoHideFlyout({
  id,
  edge,
  onClose,
  onPin,
}: {
  id: string;
  edge: "left" | "right" | "bottom";
  onClose: () => void;
  onPin: () => void;
}) {
  const { t } = useT();
  const contentRef = useRef<HTMLDivElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const def = toolWindowById(id);

  useEffect(() => {
    const node = getPanelNode(id);
    contentRef.current?.appendChild(node);
  }, [id]);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    const onPointerDown = (e: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    // Capture phase + a tick delay so the rail tab's own click (which opened this) doesn't immediately close it.
    const timer = setTimeout(() => document.addEventListener("pointerdown", onPointerDown, true), 0);
    return () => {
      clearTimeout(timer);
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("pointerdown", onPointerDown, true);
    };
  }, [onClose]);

  if (!def) return null;
  const size = def.defaultPlacement.size;
  const vertical = edge === "left" || edge === "right";

  // Left/right rails sit outside this flyout's positioned ancestor (DockWorkspace), so those edges need
  // no extra offset; the bottom rail is inside it, so a bottom flyout must clear its 22 px height.
  const positionStyle: React.CSSProperties = vertical
    ? { top: 0, bottom: 0, width: size, [edge]: 0 }
    : { left: 0, right: 0, height: size, bottom: 22 };

  return (
    <div
      ref={rootRef}
      style={{
        position: "absolute",
        ...positionStyle,
        background: "var(--ms-bg-surface)",
        border: "1px solid var(--ms-border-strong)",
        display: "flex",
        flexDirection: "column",
        zIndex: 5,
      }}
    >
      <div style={{ height: 28, flex: "0 0 auto", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 4px 0 10px", fontSize: 12.5, fontWeight: 600, color: "var(--ms-text-primary)", borderBottom: "1px solid var(--ms-border)" }}>
        <span>{t(def.titleKey)}</span>
        <button
          type="button"
          title={t("panel.pin")}
          aria-label={t("panel.pin")}
          onClick={onPin}
          style={{ width: 18, height: 18, display: "flex", alignItems: "center", justifyContent: "center", border: "none", background: "transparent", color: "var(--ms-accent)", cursor: "pointer" }}
        >
          <Pin size={12} strokeWidth={2} />
        </button>
      </div>
      <div ref={contentRef} style={{ flex: "1 1 auto", minHeight: 0, display: "flex", flexDirection: "column", overflow: "hidden" }} />
    </div>
  );
}
