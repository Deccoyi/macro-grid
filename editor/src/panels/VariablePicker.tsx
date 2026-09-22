import type { ReactNode } from "react";
import { useMemo } from "react";
import type { VariableInfo } from "../api/types";
import { PickerShell, usePickerFilter, usePickerOpenState } from "./PickerShell";

export interface VariablePickerProps {
  catalog: VariableInfo[];
  /** Inserts the token (e.g. "{system.cpu|0}") at the caller's current cursor position. */
  onInsert: (token: string) => void;
  buttonLabel?: string;
  /** "template" inserts the ready-to-use "{name|format}" token (for text fields); "bare" inserts just "name" (for picking a variable to watch, e.g. in the dynamization editor). */
  mode?: "template" | "bare";
  /** Custom trigger (e.g. a pill showing the currently-picked variable) in place of the default ghost button. */
  renderTrigger?: (open: () => void) => ReactNode;
}

/** "+ Değişken ekle": categorized (by provider — built-in "Sistem" today, a plugin's own category later), searchable. */
export function VariablePicker({ catalog, onInsert, buttonLabel = "+ Değişken ekle", mode = "template", renderTrigger }: VariablePickerProps) {
  const picker = usePickerOpenState();

  const categories = useMemo(() => {
    const counts = new Map<string, number>();
    for (const v of catalog) counts.set(v.category, (counts.get(v.category) ?? 0) + 1);
    return [...counts.entries()].map(([id, badge]) => ({ id, label: id, badge }));
  }, [catalog]);

  const items = usePickerFilter(
    catalog,
    picker.category,
    picker.query,
    (v) => v.category,
    (v, q) => v.name.toLowerCase().includes(q) || v.description.toLowerCase().includes(q),
  );

  if (catalog.length === 0) return null;

  return (
    <>
      {renderTrigger ? renderTrigger(picker.openPicker) : (
        <button type="button" className="ghost" onClick={picker.openPicker}>{buttonLabel}</button>
      )}
      {picker.open && (
        <PickerShell
          title="Değişken ekle"
          categories={categories}
          activeCategory={picker.category}
          onCategoryChange={picker.setCategory}
          searchPlaceholder="Değişken ara…"
          query={picker.query}
          onQueryChange={picker.setQuery}
          onClose={picker.closePicker}
        >
          {items.length === 0 && <div style={{ color: "var(--ms-text-secondary)", fontSize: 12, padding: 8 }}>Eşleşme yok.</div>}
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
              <div style={{ fontSize: 11, color: "var(--ms-text-secondary)" }}>{v.description}</div>
            </button>
          ))}
        </PickerShell>
      )}
    </>
  );
}
