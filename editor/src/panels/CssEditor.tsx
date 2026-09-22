import { useMemo } from "react";
import { sanitizeWidgetCss } from "@macro/renderer";

export interface CssEditorProps {
  value: string | undefined;
  onChange: (css: string) => void;
}

/** Plain textarea + sanitize warnings for now; a syntax-highlighted editor (Monaco) can replace this later
 * without changing the contract. No section label of its own — the caller (Inspector, inside a collapsed
 * <details>) already provides one via its <summary>. */
export function CssEditor({ value, onChange }: CssEditorProps) {
  const { removed } = useMemo(() => sanitizeWidgetCss(value), [value]);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <textarea
        rows={8}
        spellCheck={false}
        style={{ fontFamily: "ui-monospace, monospace", fontSize: 12 }}
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value)}
        placeholder={":host {\n  background: linear-gradient(135deg, #f59e0b, #dc2626);\n  border: 2px solid gold;\n}"}
      />
      {removed.length > 0 && (
        <ul style={{ margin: 0, padding: "6px 10px", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 4, color: "var(--ms-text-secondary)", fontSize: 11 }}>
          {removed.map((note, i) => (
            <li key={i}>{note}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
