import { useMemo, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { useT } from "../i18n/I18nContext";
import { useBackdropClose } from "../components/useBackdropClose";

interface PickerCategory {
  id: string;
  label: string;
  /** Shown next to the label, e.g. an item count. */
  badge?: string | number;
}

interface PickerShellProps {
  title: string;
  categories: PickerCategory[];
  /** "all" is always available in addition to whatever ids are passed. */
  activeCategory: string;
  onCategoryChange: (id: string) => void;
  searchPlaceholder: string;
  query: string;
  onQueryChange: (q: string) => void;
  footer?: ReactNode;
  onClose: () => void;
  children: ReactNode;
}

/**
 * Shared modal shape for "pick one of many, grouped by source": a left sidebar of categories/packs
 * (variable categories today, icon packs — lucide now, plugin-provided ones later) plus a search box
 * over the current category. Both VariablePicker and IconPicker are this shell with different content.
 */
export function PickerShell({
  title,
  categories,
  activeCategory,
  onCategoryChange,
  searchPlaceholder,
  query,
  onQueryChange,
  footer,
  onClose,
  children,
}: PickerShellProps) {
  const { t } = useT();
  const backdrop = useBackdropClose(onClose);
  // Portalled to <body>: the picker is opened from inside Properties fields, and rendered inline it
  // inherits their rules (for example `.pf-actions button { height: 18px }`, which squashed every
  // two-line row to 18 px so the rows overlapped). The z-index still has to beat dockview's splitters (99).
  return createPortal(
    <div className="picker-backdrop" {...backdrop}>
      <div className="picker-dialog" role="dialog" aria-label={title} onClick={(e) => e.stopPropagation()}>
        <div className="picker-head">
          <h1>{title}</h1>
          <button type="button" className="ghost picker-close" onClick={onClose} aria-label={t("picker.close")} title={t("picker.close")}>
            <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>
          </button>
        </div>

        <div className="picker-body">
          <div className="picker-cats">
            <CategoryButton active={activeCategory === "all"} onClick={() => onCategoryChange("all")} label={t("picker.all")} />
            {categories.map((c) => (
              <CategoryButton key={c.id} active={activeCategory === c.id} onClick={() => onCategoryChange(c.id)} label={c.label} badge={c.badge} />
            ))}
          </div>

          <div className="picker-main">
            <div className="picker-search">
              <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" aria-hidden="true"><circle cx="11" cy="11" r="7" /><path d="m20 20-4-4" /></svg>
              <input type="text" autoFocus placeholder={searchPlaceholder} aria-label={searchPlaceholder} value={query} onChange={(e) => onQueryChange(e.target.value)} />
            </div>
            <div className="picker-list">{children}</div>
          </div>
        </div>

        {footer && <div className="picker-foot">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

function CategoryButton({ active, onClick, label, badge }: { active: boolean; onClick: () => void; label: string; badge?: string | number }) {
  return (
    <button type="button" className={active ? "picker-cat on" : "picker-cat"} onClick={onClick} aria-pressed={active}>
      <span>{label}</span>
      {badge != null && <span className="picker-badge">{badge}</span>}
    </button>
  );
}

/** Small helper so each picker only writes its own matching predicate, not the open/query/category plumbing. */
export function usePickerFilter<T>(items: T[], activeCategory: string, query: string, getCategory: (item: T) => string, matches: (item: T, q: string) => boolean) {
  return useMemo(() => {
    const byCategory = activeCategory === "all" ? items : items.filter((i) => getCategory(i) === activeCategory);
    const q = query.trim().toLowerCase();
    return q ? byCategory.filter((i) => matches(i, q)) : byCategory;
  }, [items, activeCategory, query, getCategory, matches]);
}

export function usePickerOpenState() {
  const [open, setOpen] = useState(false);
  const [category, setCategory] = useState("all");
  const [query, setQuery] = useState("");
  return {
    open,
    openPicker: () => { setOpen(true); setCategory("all"); setQuery(""); },
    closePicker: () => setOpen(false),
    category,
    setCategory,
    query,
    setQuery,
  };
}
