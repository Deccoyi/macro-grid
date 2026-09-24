import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { usePreferences } from "../../preferences/PreferencesContext";

/** Shared preset palette offered by every color field's popover (see ColorField below). */
export const SWATCHES = [
  "#374151", "#475569", "#b91c1c", "#c2410c", "#b45309", "#84761f", "#15803d", "#0f766e",
  "#0e7490", "#1d4ed8", "#4338ca", "#6d28d9", "#a21caf", "#be185d", "#78350f", "#111827",
];

/** Small caps label above a group of fields — the only "section" affordance in the properties panel
 * (see docs/ui-guidelines.md: no card-per-section, a thin divider + label is enough). */
export function SectionLabel({ children }: { children: string }) {
  return <div className="section-label">{children}</div>;
}

/** A top-level Inspector section (Appearance, the type-specific fields, Actions, Custom CSS) that can be
 * collapsed — state remembered per user, server-side (see PreferencesContext), not just for this
 * session: `id` must be stable and unique across the panel (e.g. "appearance", "css").
 *
 * Deliberately a plain button + conditional render, not a native <details>: a controlled <details open>
 * re-renders every sibling section whenever any one of them toggles (they all share the same
 * `collapsedInspectorSections` object from context), and re-applying the same `open` value on a
 * <details> the browser just toggled natively raced its own "toggle" event — one click was observed
 * flipping *other*, untouched sections' stored state too. A button has no such native toggle event to
 * race, so only the section actually clicked ever calls setInspectorSectionCollapsed. */
export function CollapsibleSection({
  id,
  label,
  defaultCollapsed = false,
  children,
}: {
  id: string;
  label: string;
  defaultCollapsed?: boolean;
  children: ReactNode;
}) {
  const { collapsedInspectorSections, setInspectorSectionCollapsed } = usePreferences();
  const collapsed = collapsedInspectorSections[id] ?? defaultCollapsed;

  return (
    <div className="collapsible">
      <button type="button" className="collapsible-summary" onClick={() => setInspectorSectionCollapsed(id, !collapsed)}>
        <span className="collapsible-caret">{collapsed ? "▸" : "▾"}</span>
        {label}
      </button>
      {!collapsed && (
        <div style={{ marginTop: 8, display: "flex", flexDirection: "column", gap: 10 }}>{children}</div>
      )}
    </div>
  );
}

/** Swatch + hex trigger that opens our own compact popover (a native color-wheel swatch, hex entry, and
 * the shared preset palette) — not the browser's plain OS color dialog on its own. The popover is
 * portaled to <body> and positioned from the trigger's own screen rect, right-edge-aligned to it, so an
 * `overflow-y: auto` ancestor (the Properties panel) can never clip it — a plain absolutely-positioned
 * child WOULD be clipped there, because a lone `overflow-y` forces the element's `overflow-x` to `auto`
 * too (see docs/ui-guidelines.md: "kompakt masaüstü menü", never a floating web card). */
export function ColorField({ value, onChange, disabled, title }: { value?: string; onChange: (v: string) => void; disabled?: boolean; title?: string }) {
  const [open, setOpen] = useState(false);
  const [pos, setPos] = useState<{ top: number; right: number } | null>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const isColor = /^#([0-9a-f]{6})$/i.test(value ?? "");

  useLayoutEffect(() => {
    if (!open || !triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    setPos({ top: rect.bottom + 4, right: window.innerWidth - rect.right });
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (e: PointerEvent) => {
      const target = e.target as Node;
      if (triggerRef.current?.contains(target) || popoverRef.current?.contains(target)) return;
      setOpen(false);
    };
    const onKeyDown = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    // A popover anchored by screen rect goes stale the moment its scroll container moves — closing on
    // any scroll is simpler and safer than re-measuring on every frame.
    const onScroll = () => setOpen(false);
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    window.addEventListener("scroll", onScroll, true);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("scroll", onScroll, true);
    };
  }, [open]);

  return (
    <>
      <button ref={triggerRef} type="button" className="color-field" disabled={disabled} title={title} style={{ width: "100%", cursor: "pointer" }} onClick={() => setOpen((o) => !o)}>
        <span style={{ width: 15, height: 15, borderRadius: 3, background: isColor ? value : "transparent", border: isColor ? "1px solid rgba(255,255,255,.18)" : "1px dashed var(--ms-text-disabled)", flexShrink: 0 }} />
        <span style={{ fontFamily: "ui-monospace, monospace", fontSize: 12, flex: 1, textAlign: "left", color: isColor ? "var(--ms-text-primary)" : "var(--ms-text-disabled)" }}>
          {value || "—"}
        </span>
      </button>

      {open && pos && createPortal(
        <div ref={popoverRef} className="color-field-popover" style={{ position: "fixed", top: pos.top, right: pos.right }}>
          <div className="color-field">
            <label
              title="Renk çarkından seç"
              style={{ width: 15, height: 15, borderRadius: 3, background: isColor ? value : "transparent", border: isColor ? "1px solid rgba(255,255,255,.18)" : "1px dashed var(--ms-text-disabled)", flexShrink: 0, position: "relative", overflow: "hidden", cursor: "pointer" }}
            >
              <input
                type="color"
                value={isColor ? value! : "#000000"}
                onChange={(e) => onChange(e.target.value)}
                style={{ position: "absolute", inset: 0, width: "100%", height: "100%", opacity: 0, border: "none", padding: 0, cursor: "pointer" }}
              />
            </label>
            <input type="text" autoFocus value={value ?? ""} onChange={(e) => onChange(e.target.value)} placeholder="#374151" />
          </div>
          <div className="section-label" style={{ margin: "8px 0 4px" }}>Hazır Renkler</div>
          <div className="swatch-grid">
            {SWATCHES.map((hex) => (
              <button
                key={hex}
                type="button"
                title={hex}
                className={value?.toLowerCase() === hex ? "swatch on" : "swatch"}
                style={{ background: hex }}
                onClick={() => { onChange(hex); setOpen(false); }}
              />
            ))}
          </div>
        </div>,
        document.body,
      )}
    </>
  );
}

export interface SegOption<T extends string> {
  value: T;
  label: ReactNode;
  title?: string;
}

/** A segmented control (icon or text) — replaces plain <select> for small, fixed option sets so the
 * choice is visible at a glance instead of hidden behind a click. */
export function Seg<T extends string>({ value, options, onChange }: { value: T; options: SegOption<T>[]; onChange: (v: T) => void }) {
  return (
    <div className="seg">
      {options.map((o) => (
        <button key={o.value} type="button" title={o.title} className={value === o.value ? "on" : undefined} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}
