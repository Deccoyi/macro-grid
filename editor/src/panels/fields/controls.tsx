import type { ReactNode } from "react";

/** Small caps label above a group of fields — the only "section" affordance in the properties panel
 * (see docs/ui-guidelines.md: no card-per-section, a thin divider + label is enough). */
export function SectionLabel({ children }: { children: string }) {
  return <div className="section-label">{children}</div>;
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
