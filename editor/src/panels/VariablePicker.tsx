import type { ReactNode } from "react";
import { useMemo } from "react";
import type { VariableInfo } from "../api/types";
import { useCatalogText } from "../i18n/catalogText";
import { useT } from "../i18n/I18nContext";
import { PickerShell, usePickerFilter, usePickerOpenState } from "./PickerShell";

interface VariablePickerProps {
  catalog: VariableInfo[];
  /** Inserts the token (e.g. "{system.cpu|0}") at the caller's current cursor position. */
  onInsert: (token: string) => void;
  buttonLabel?: string;
  /** "template" inserts the ready-to-use "{name|format}" token (for text fields); "bare" inserts just "name" (for picking a variable to watch, e.g. in the dynamization editor). */
  mode?: "template" | "bare";
  /** Custom trigger (e.g. a pill showing the currently-picked variable) in place of the default ghost button. */
  renderTrigger?: (open: () => void) => ReactNode;
}

/** "+ Add variable": categorized (by provider — built-in "System" today, a plugin's own category later), searchable. */
export function VariablePicker({ catalog, onInsert, buttonLabel, mode = "template", renderTrigger }: VariablePickerProps) {
  const { t } = useT();
  const catalogText = useCatalogText();
  const picker = usePickerOpenState();
  const label = buttonLabel ?? t("variable.add");

  const categories = useMemo(() => {
    const counts = new Map<string, number>();
    for (const v of catalog) counts.set(v.category, (counts.get(v.category) ?? 0) + 1);
    return [...counts.entries()].map(([id, badge]) => ({ id, label: catalogText.categoryLabel(id), badge }));
  }, [catalog, catalogText]);

  const items = usePickerFilter(
    catalog,
    picker.category,
    picker.query,
    (v) => v.category,
    (v, q) => v.name.toLowerCase().includes(q) || catalogText.variableDescription(v).toLowerCase().includes(q),
  );

  if (catalog.length === 0) return null;

  return (
    <>
      {renderTrigger ? renderTrigger(picker.openPicker) : (
        <button type="button" className="ghost" onClick={picker.openPicker}>{label}</button>
      )}
      {picker.open && (
        <PickerShell
          title={t("variable.pickTitle")}
          categories={categories}
          activeCategory={picker.category}
          onCategoryChange={picker.setCategory}
          searchPlaceholder={t("variable.searchPlaceholder")}
          query={picker.query}
          onQueryChange={picker.setQuery}
          onClose={picker.closePicker}
        >
          {items.length === 0 && <div style={{ color: "var(--ms-text-secondary)", fontSize: 12, padding: 8 }}>{t("variable.noMatch")}</div>}
          {items.map((v) => (
            <button
              key={v.name}
              type="button"
              className="ghost"
              onClick={() => { onInsert(mode === "bare" ? v.name : v.example); picker.closePicker(); }}
              title={v.example}
              style={{ display: "block", width: "100%", textAlign: "left", borderRadius: 0, padding: "6px 10px" }}
            >
              <div style={{ fontFamily: "ui-monospace, monospace", fontSize: 12 }}>{`{${v.name}}`}</div>
              <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{catalogText.variableDescription(v)}</div>
            </button>
          ))}
        </PickerShell>
      )}
    </>
  );
}
