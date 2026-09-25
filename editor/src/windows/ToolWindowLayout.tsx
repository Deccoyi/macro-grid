import type { ReactNode } from "react";

interface ToolWindowCategory {
  id: string;
  label: string;
}

interface ToolWindowLayoutProps {
  categories: ToolWindowCategory[];
  activeId: string;
  onSelect: (id: string) => void;
  children: ReactNode;
}

/** Left categories + right settings — the layout native desktop preferences/plugins windows use, not a
 * modal dialog. This mounts as the whole page of its own separate OS window (see ToolWindow.cs /
 * main.tsx's `?window=` routing), so it fills the window rather than floating over it. */
export function ToolWindowLayout({ categories, activeId, onSelect, children }: ToolWindowLayoutProps) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "170px 1fr", height: "100%", background: "var(--ms-bg-canvas)", color: "var(--ms-text-primary)" }}>
      <div style={{ borderRight: "1px solid var(--ms-border)", background: "var(--ms-bg-surface)", padding: 6, display: "flex", flexDirection: "column", gap: 2 }}>
        {categories.map((c) => (
          <button
            key={c.id}
            type="button"
            className="ghost"
            onClick={() => onSelect(c.id)}
            style={{
              textAlign: "left",
              justifyContent: "flex-start",
              padding: "7px 10px",
              background: activeId === c.id ? "var(--ms-accent-bg-muted)" : "transparent",
              color: activeId === c.id ? "var(--ms-accent)" : "var(--ms-text-primary)",
              fontWeight: activeId === c.id ? 600 : 400,
              fontSize: 12.5,
            }}
          >
            {c.label}
          </button>
        ))}
      </div>
      <div style={{ padding: 20, overflowY: "auto" }}>{children}</div>
    </div>
  );
}
