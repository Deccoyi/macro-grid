import { createContext, useContext, useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { ChevronDown, ChevronRight, X } from "lucide-react";
import { useT } from "../../i18n/I18nContext";
import { usePreferences } from "../../preferences/PreferencesContext";

/** Shared preset palette offered by every color field's popover (see ColorField below). */
const SWATCHES = [
  "#374151", "#475569", "#b91c1c", "#c2410c", "#b45309", "#84761f", "#15803d", "#0f766e",
  "#0e7490", "#1d4ed8", "#4338ca", "#6d28d9", "#a21caf", "#be185d", "#78350f", "#111827",
];

/** A hint box the person can close. The X asks which kind of closing: "close for now" hides it in this window until it is opened again, "do not show
 * again" stores `id` in the preferences (server side), so it stays closed on every window and after a restart; the Preferences window can show all
 * closed boxes again. Only for hints: a warning about the current input must not use it. */
export function DismissibleNote({ id, accent = "var(--ms-accent)", children }: { id: string; accent?: string; children: ReactNode }) {
  const { t } = useT();
  const { dismissedNotices, dismissNotice } = usePreferences();
  const [asking, setAsking] = useState(false);
  const [hiddenNow, setHiddenNow] = useState(false);
  if (dismissedNotices[id] || hiddenNow) return null;
  return (
    <div
      role="note"
      style={{
        position: "relative", display: "flex", flexDirection: "column", gap: 4, padding: "8px 26px 8px 10px", fontSize: 11.5, lineHeight: 1.4,
        color: "var(--ms-text-primary)", background: "var(--ms-accent-bg-muted)", borderLeft: `3px solid ${accent}`, borderRadius: 4,
      }}
    >
      {children}
      {asking ? (
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }} onKeyDown={(e) => { if (e.key === "Escape") setAsking(false); }}>
          <button type="button" autoFocus onClick={() => setHiddenNow(true)}>{t("notice.closeNow")}</button>
          <button type="button" onClick={() => dismissNotice(id)}>{t("notice.dontShow")}</button>
        </div>
      ) : (
        <button
          type="button"
          className="ghost"
          onClick={() => setAsking(true)}
          title={t("notice.dismiss")}
          aria-label={t("notice.dismiss")}
          style={{ position: "absolute", top: 4, right: 4, width: 18, height: 18, padding: 0, display: "flex", alignItems: "center", justifyContent: "center", border: "none", background: "transparent", color: "var(--ms-text-secondary)", cursor: "pointer" }}
        >
          <X size={12} />
        </button>
      )}
    </div>
  );
}

/** Small caps label above a group of fields — the only "section" affordance in the properties panel
 * (see docs/ui/ui-guidelines.md: no card-per-section, a thin divider + label is enough). */
export function SectionLabel({ children }: { children: string }) {
  return <div className="section-label pf-group-label">{children}</div>;
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
        <span className="collapsible-caret">{collapsed ? <ChevronRight size={14} /> : <ChevronDown size={14} />}</span>
        {label}
      </button>
      {!collapsed && <div className="pf-body">{children}</div>}
    </div>
  );
}

/** Swatch + hex trigger that opens our own compact popover (a native color-wheel swatch, hex entry, and
 * the shared preset palette) — not the browser's plain OS color dialog on its own. The popover is
 * portaled to <body> and positioned from the trigger's own screen rect, right-edge-aligned to it, so an
 * `overflow-y: auto` ancestor (the Properties panel) can never clip it — a plain absolutely-positioned
 * child WOULD be clipped there, because a lone `overflow-y` forces the element's `overflow-x` to `auto`
 * too (see docs/ui/ui-guidelines.md: "compact desktop menu", never a floating web card). */
export function ColorField({ value, onChange, disabled, title }: { value?: string; onChange: (v: string) => void; disabled?: boolean; title?: string }) {
  const { t } = useT();
  const field = useContext(FieldContext);
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
      <button ref={triggerRef} type="button" className="color-field" id={field?.id} aria-labelledby={field?.labelId} disabled={disabled} title={title} style={{ width: "100%", cursor: "pointer" }} onClick={() => setOpen((o) => !o)}>
        <span style={{ width: 16, height: 16, borderRadius: 3, background: isColor ? value : "transparent", border: isColor ? "1px solid rgba(255,255,255,.18)" : "1px dashed var(--ms-text-disabled)", flexShrink: 0 }} />
        <span style={{ fontFamily: "ui-monospace, monospace", fontSize: 12, flex: 1, textAlign: "left", color: isColor ? "var(--ms-text-primary)" : "var(--ms-text-disabled)" }}>
          {value || "—"}
        </span>
      </button>

      {open && pos && createPortal(
        <div ref={popoverRef} className="color-field-popover" style={{ position: "fixed", top: pos.top, right: pos.right }}>
          <div className="color-field">
            <label
              title={t("color.pickFromWheel")}
              style={{ width: 16, height: 16, borderRadius: 3, background: isColor ? value : "transparent", border: isColor ? "1px solid rgba(255,255,255,.18)" : "1px dashed var(--ms-text-disabled)", flexShrink: 0, position: "relative", overflow: "hidden", cursor: "pointer" }}
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
          <div className="section-label" style={{ margin: "8px 0 4px" }}>{t("color.presets")}</div>
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

interface SegOption<T extends string> {
  value: T;
  label: ReactNode;
  title?: string;
}

/** A segmented control (icon or text) — replaces plain <select> for small, fixed option sets so the
 * choice is visible at a glance instead of hidden behind a click. */
export function Seg<T extends string>({ value, options, onChange }: { value: T; options: SegOption<T>[]; onChange: (v: T) => void }) {
  const field = useContext(FieldContext);
  return (
    <div className="seg" role="group" aria-labelledby={field?.labelId}>
      {options.map((o) => (
        <button key={o.value} type="button" title={o.title} aria-pressed={value === o.value} className={value === o.value ? "on" : undefined} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </div>
  );
}

interface FieldContextValue { id: string; labelId: string }
const FieldContext = createContext<FieldContextValue | null>(null);

/** The id of the control inside the nearest <see cref="Field"/>, so a native input or select is labelled by the field's label. */
export function useFieldId(): string | undefined {
  return useContext(FieldContext)?.id;
}

interface FieldProps {
  label: string;
  /** Small actions on the right of the label row (Add variable, Make dynamic, Refresh), each 18 px high. */
  action?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  /** Label left, control right on one 28 px row (Bool). */
  inline?: boolean;
  /** A single-value field (Number, Select, Color, Segmented) that becomes a Label | Value row in a wide panel. */
  single?: boolean;
  /** Full text of a label that may be cut with an ellipsis; defaults to the label. */
  title?: string;
  children: ReactNode;
}

/** One field of the Properties panel: label row (18), control, hint or error. Every kind of field is drawn through it (see
 * docs/ui/ui-guidelines.md, "Properties panel field system"). */
export function Field({ label, action, hint, error, inline, single, title, children }: FieldProps) {
  const id = useId();
  const labelId = `${id}-l`;
  const message = error ?? hint;
  const classes = ["pf-field", inline ? "inline" : "", single ? "single" : "", error ? "invalid" : ""].filter(Boolean).join(" ");
  return (
    <FieldContext.Provider value={{ id, labelId }}>
      <div className={classes}>
        {inline ? (
          <>
            <div className="pf-label-text">
              <label className="pf-name pf-label" id={labelId} htmlFor={id} title={title ?? label}>{label}</label>
              {message && <span className={error ? "pf-hint error" : "pf-hint"}>{message}</span>}
            </div>
            {children}
          </>
        ) : (
          <>
            <div className="pf-label-row">
              <label className="pf-label" id={labelId} htmlFor={id} title={title ?? label}>{label}</label>
              {action && <span className="pf-actions">{action}</span>}
            </div>
            <div className="pf-control">{children}</div>
            {message && <span className={error ? "pf-hint error" : "pf-hint"}>{message}</span>}
          </>
        )}
      </div>
    </FieldContext.Provider>
  );
}

/** Fields placed side by side. The panel width decides how many columns stay (see theme.css, the @container rules). */
export function FieldGrid({ cols, children }: { cols: 2 | 3; children: ReactNode }) {
  return <div className={cols === 3 ? "pf-grid-3" : "pf-grid-2"}>{children}</div>;
}

/** A small on/off toggle, 28x16. A real button with role="switch". */
export function Switch({ checked, onChange, disabled, label }: { checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; label?: string }) {
  const field = useContext(FieldContext);
  return (
    <button
      type="button"
      role="switch"
      id={field?.id}
      aria-checked={checked}
      aria-label={field ? undefined : label}
      aria-labelledby={field?.labelId}
      className="pf-switch"
      disabled={disabled}
      onClick={() => onChange(!checked)}
    />
  );
}

interface NumberInputProps {
  value: number;
  onChange: (v: number) => void;
  min?: number;
  max?: number;
  step?: number | "any";
  /** Shown dimmed inside the input on the right (px, ms). */
  unit?: string;
  disabled?: boolean;
  label?: string;
  /** A second input in the same Field (a width and a height): named by the label, but without the id the first one has. */
  secondary?: boolean;
}

/** A number input, 28 high, with an optional unit inside on the right. */
export function NumberInput({ value, onChange, min, max, step, unit, disabled, label, secondary }: NumberInputProps) {
  const field = useContext(FieldContext);
  return (
    <div className={unit ? "pf-num has-unit" : "pf-num"}>
      <input
        type="number"
        id={secondary ? undefined : field?.id}
        aria-labelledby={secondary ? field?.labelId : undefined}
        aria-label={field ? undefined : label}
        value={value}
        min={min}
        max={max}
        step={step}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      {unit && <span className="unit" aria-hidden="true">{unit}</span>}
    </div>
  );
}

/** A slider track with a 56 px editable value box; the box keeps the value inside min and max. */
export function RangeInput({ value, onChange, min, max, step = 1, disabled, label }: { value: number; onChange: (v: number) => void; min: number; max: number; step?: number; disabled?: boolean; label?: string }) {
  const field = useContext(FieldContext);
  const clamp = (v: number) => Math.min(max, Math.max(min, v));
  return (
    <div className="pf-range">
      <input
        type="range"
        id={field?.id}
        aria-label={field ? undefined : label}
        value={value}
        min={min}
        max={max}
        step={step}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
      />
      <span className="value">
        <input
          type="number"
          aria-labelledby={field?.labelId}
          aria-label={field ? undefined : label}
          value={value}
          min={min}
          max={max}
          step={step}
          disabled={disabled}
          onChange={(e) => { if (e.target.value !== "") onChange(clamp(Number(e.target.value))); }}
        />
      </span>
    </div>
  );
}

/** A single-line text input, 28 high, labelled by the nearest Field. */
export function TextInput({ value, onChange, placeholder, readOnly, disabled, maxLength, label, className, onClick }: {
  value: string; onChange?: (v: string) => void; placeholder?: string; readOnly?: boolean; disabled?: boolean; maxLength?: number; label?: string; className?: string; onClick?: () => void;
}) {
  const field = useContext(FieldContext);
  return (
    <input
      type="text"
      id={field?.id}
      aria-label={field ? undefined : label}
      className={className}
      value={value}
      placeholder={placeholder}
      readOnly={readOnly}
      disabled={disabled}
      maxLength={maxLength}
      onClick={onClick}
      onChange={(e) => onChange?.(e.target.value)}
    />
  );
}

/** A multi-line text input labelled by the nearest Field: controlled with `value`, or uncontrolled with `defaultValue` and `onBlur` (for JSON text). */
export function TextAreaInput({ value, onChange, defaultValue, onBlur, rows = 2, placeholder, mono }: {
  value?: string; onChange?: (v: string) => void; defaultValue?: string; onBlur?: (v: string) => void; rows?: number; placeholder?: string; mono?: boolean;
}) {
  const field = useContext(FieldContext);
  return (
    <textarea
      id={field?.id}
      rows={rows}
      className={mono ? "pf-mono" : undefined}
      value={value}
      defaultValue={defaultValue}
      placeholder={placeholder}
      onChange={onChange ? (e) => onChange(e.target.value) : undefined}
      onBlur={onBlur ? (e) => onBlur(e.target.value) : undefined}
    />
  );
}

/** A native select with a chevron, 28 high, labelled by the nearest Field. */
export function SelectInput({ value, onChange, children, disabled, label }: { value: string; onChange: (v: string) => void; children: ReactNode; disabled?: boolean; label?: string }) {
  const field = useContext(FieldContext);
  return (
    <div className="pf-sel">
      <select id={field?.id} aria-label={field ? undefined : label} value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>{children}</select>
      <span className="chev" aria-hidden="true"><ChevronDown size={14} /></span>
    </div>
  );
}

/** A text input that keeps its own draft and hands the trimmed text over on blur or Enter; Escape puts the old text back. */
export function CommitTextInput({ value, onCommit, disabled }: { value: string; onCommit: (v: string) => void; disabled?: boolean }) {
  const [draft, setDraft] = useState(value);
  useEffect(() => setDraft(value), [value]);
  return (
    <div
      onBlur={() => onCommit(draft.trim())}
      onKeyDown={(e) => {
        if (e.key === "Enter") (e.target as HTMLElement).blur();
        if (e.key === "Escape") setDraft(value);
      }}
    >
      <TextInput value={draft} disabled={disabled} onChange={setDraft} />
    </div>
  );
}
