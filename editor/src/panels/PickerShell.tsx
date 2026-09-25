import { useMemo, useState, type ReactNode } from "react";
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
  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 50, display: "flex", alignItems: "center", justifyContent: "center" }} {...backdrop}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border)", borderRadius: 6,
          width: 620, height: 460, display: "grid", gridTemplateRows: "auto 1fr auto", gridTemplateColumns: "1fr",
          overflow: "hidden",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 10px", borderBottom: "1px solid var(--ms-border)" }}>
          <span style={{ fontSize: 12, color: "var(--ms-text-secondary)", textTransform: "uppercase", letterSpacing: ".04em" }}>{title}</span>
          <div style={{ flex: 1 }} />
          <button type="button" className="ghost" onClick={onClose}>{t("picker.close")}</button>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "150px 1fr", minHeight: 0 }}>
          <div style={{ borderRight: "1px solid var(--ms-border)", overflowY: "auto", padding: 6 }}>
            <CategoryButton active={activeCategory === "all"} onClick={() => onCategoryChange("all")} label={t("picker.all")} />
            {categories.map((c) => (
              <CategoryButton key={c.id} active={activeCategory === c.id} onClick={() => onCategoryChange(c.id)} label={c.label} badge={c.badge} />
            ))}
          </div>

          <div style={{ display: "flex", flexDirection: "column", minHeight: 0, padding: 8, gap: 8 }}>
            <input type="text" autoFocus placeholder={searchPlaceholder} value={query} onChange={(e) => onQueryChange(e.target.value)} />
            <div style={{ flex: 1, overflowY: "auto" }}>{children}</div>
          </div>
        </div>

        {footer && <div style={{ padding: "6px 10px", borderTop: "1px solid var(--ms-border)", fontSize: 11, color: "var(--ms-text-secondary)" }}>{footer}</div>}
      </div>
    </div>
  );
}

function CategoryButton({ active, onClick, label, badge }: { active: boolean; onClick: () => void; label: string; badge?: string | number }) {
  return (
    <button
      type="button"
      className={active ? "active" : "ghost"}
      onClick={onClick}
      style={{ display: "flex", justifyContent: "space-between", width: "100%", textAlign: "left", marginBottom: 2 }}
    >
      <span>{label}</span>
      {badge != null && <span style={{ color: "var(--ms-text-secondary)", fontSize: 11 }}>{badge}</span>}
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
