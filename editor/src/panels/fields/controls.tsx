import type { ReactNode } from "react";
import { usePreferences } from "../../preferences/PreferencesContext";

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

/** Swatch + hex in one flat row, shared by every color field in the panel. */
export function ColorField({ value, onChange }: { value?: string; onChange: (v: string) => void }) {
  return (
    <div className="color-field">
      <input type="color" value={/^#([0-9a-f]{6})$/i.test(value ?? "") ? value : "#000000"} onChange={(e) => onChange(e.target.value)} />
      <input type="text" value={value ?? ""} onChange={(e) => onChange(e.target.value)} placeholder="#374151" />
    </div>
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
