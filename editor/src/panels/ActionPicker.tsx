import { lazy, Suspense, useMemo } from "react";
import dynamicIconImports from "lucide-react/dynamicIconImports";
import { Zap } from "lucide-react";
import type { ActionInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { PickerShell, usePickerFilter, usePickerOpenState } from "./PickerShell";

/** Lazily loads a single lucide icon by name (same source list IconPicker.tsx uses) — a generic bolt
 * icon while it loads or if the plugin named one that doesn't exist in this lucide version. */
function ActionIcon({ name }: { name?: string | null }) {
  const Loaded = useMemo(() => {
    const load = name ? dynamicIconImports[name as keyof typeof dynamicIconImports] : undefined;
    return load ? lazy(load) : null;
  }, [name]);
  if (!Loaded) return <Zap size={15} />;
  return (
    <Suspense fallback={<Zap size={15} />}>
      <Loaded size={15} />
    </Suspense>
  );
}

export interface ActionPickerProps {
  actions: ActionInfo[];
  onPick: (type: string) => void;
  /** Custom trigger in place of the default "+ Aksiyon ekle" button (e.g. a binding row's type button). */
  renderTrigger?: (open: () => void) => React.ReactNode;
}

/**
 * Categorized, searchable dialog for choosing an action type — replaces the old flat `<select>` (which
 * just showed every action, built-in and plugin, in one alphabetical list with no context). Same shell
 * as VariablePicker/IconPicker (see PickerShell.tsx).
 */
export function ActionPicker({ actions, onPick, renderTrigger }: ActionPickerProps) {
  const { t } = useT();
  const picker = usePickerOpenState();

  const categories = useMemo(() => {
    const counts = new Map<string, number>();
    for (const a of actions) counts.set(a.category, (counts.get(a.category) ?? 0) + 1);
    return [...counts.entries()].map(([id, badge]) => ({ id, label: id, badge }));
  }, [actions]);

  const items = usePickerFilter(
    actions,
    picker.category,
    picker.query,
    (a) => a.category,
    (a, q) => a.displayName.toLowerCase().includes(q) || (a.description ?? "").toLowerCase().includes(q),
  );

  return (
    <>
      {renderTrigger ? renderTrigger(picker.openPicker) : (
        <button type="button" className="ghost" onClick={picker.openPicker} disabled={actions.length === 0} style={{ alignSelf: "flex-start" }}>
          {t("action.add")}
        </button>
      )}
      {picker.open && (
        <PickerShell
          title={t("actionPicker.title")}
          categories={categories}
          activeCategory={picker.category}
          onCategoryChange={picker.setCategory}
          searchPlaceholder={t("actionPicker.searchPlaceholder")}
          query={picker.query}
          onQueryChange={picker.setQuery}
          onClose={picker.closePicker}
        >
          {items.length === 0 && <div style={{ color: "var(--ms-text-secondary)", fontSize: 12, padding: 8 }}>{t("actionPicker.noMatch")}</div>}
          {items.map((a) => (
            <button
              key={a.type}
              type="button"
              className="ghost"
              onClick={() => { onPick(a.type); picker.closePicker(); }}
              style={{ display: "flex", alignItems: "flex-start", gap: 8, width: "100%", textAlign: "left", borderRadius: 0, padding: "6px 10px" }}
            >
              <span style={{ marginTop: 1, flexShrink: 0, color: "var(--ms-text-secondary)" }}><ActionIcon name={a.icon} /></span>
              <span style={{ minWidth: 0 }}>
                <div style={{ fontSize: 12.5 }}>{a.displayName}</div>
                {a.description && <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{a.description}</div>}
              </span>
            </button>
          ))}
        </PickerShell>
      )}
    </>
  );
}
