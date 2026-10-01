import { useEffect, useState } from "react";
import type { Widget } from "@macro/renderer";
import { useT } from "../../i18n/I18nContext";
import { normalizeWidgetName } from "../../state/widgetNames";

/** The widget's instance name, at the top of its properties. It commits on blur or Enter; an empty or taken name shows a line under the field and
 * the old name comes back. */
export function NameField({ widget, siblings, onRename }: { widget: Widget; siblings: Widget[]; onRename: (name: string) => void }) {
  const { t } = useT();
  const [text, setText] = useState(widget.name ?? "");
  const [error, setError] = useState<"empty" | "taken" | null>(null);

  // Another widget was selected, or the name changed from outside (undo).
  useEffect(() => { setText(widget.name ?? ""); setError(null); }, [widget.id, widget.name]);

  const commit = () => {
    const name = normalizeWidgetName(text);
    if (name === undefined) { setError("empty"); setText(widget.name ?? ""); return; }
    if (siblings.some((w) => w.id !== widget.id && w.name?.toLowerCase() === name.toLowerCase())) { setError("taken"); setText(widget.name ?? ""); return; }
    setError(null);
    setText(name);
    if (name !== widget.name) onRename(name);
  };

  return (
    <label className="field">
      {t("fields.name.label")}
      <input
        type="text"
        value={text}
        maxLength={64}
        onChange={(e) => { setText(e.target.value); setError(null); }}
        onBlur={commit}
        onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); if (e.key === "Escape") { setText(widget.name ?? ""); setError(null); } }}
      />
      {error && <span role="alert" style={{ fontSize: 11, color: "var(--ms-danger)" }}>{t(error === "empty" ? "fields.name.empty" : "fields.name.taken")}</span>}
      {widget.type === "web" && !error && <span style={{ fontSize: 11, color: "var(--ms-text-disabled)" }}>{t("fields.web.nameHint")}</span>}
    </label>
  );
}
