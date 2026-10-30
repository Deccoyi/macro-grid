import type { ComponentType, ReactNode } from "react";

export interface SettingsCategory {
  id: string;
  label: string;
  icon?: ComponentType<{ size?: number }>;
}

export interface SettingsGroup {
  label: string;
  categories: SettingsCategory[];
}

interface SettingsShellProps {
  groups: SettingsGroup[];
  navLabel: string;
  activeId: string;
  onSelect: (id: string) => void;
  /** Page width: 640 px (simple pages) or 760 px (tables and rule lists). */
  children: ReactNode;
}

/** The Preferences window shell: grouped navigation on the left, one page on the right, not a
 * modal dialog. This mounts as the whole page of its own separate OS window (see ToolWindow.cs /
 * main.tsx's `?window=` routing), so it fills the window rather than floating over it. Below 720 px the navigation
 * becomes a select above the page. */
export function SettingsShell({ groups, navLabel, activeId, onSelect, children }: SettingsShellProps) {
  const all = groups.flatMap((g) => g.categories);
  return (
    <div className="st-shell">
      <nav className="st-nav" aria-label={navLabel}>
        {groups.map((g) => (
          <div key={g.label} style={{ display: "contents" }}>
            <div className="st-nav-group">{g.label}</div>
            {g.categories.map((c) => (
              <button key={c.id} type="button" className="st-nav-item" aria-current={activeId === c.id ? "page" : undefined} onClick={() => onSelect(c.id)}>
                {c.icon && <c.icon size={14} />}
                <span>{c.label}</span>
              </button>
            ))}
          </div>
        ))}
      </nav>
      <div className="st-nav-select pf-root" style={{ height: "auto", overflow: "visible" }}>
        <select aria-label={navLabel} value={activeId} onChange={(e) => onSelect(e.target.value)} style={{ width: "100%" }}>
          {all.map((c) => <option key={c.id} value={c.id}>{c.label}</option>)}
        </select>
      </div>
      <main className="st-main pf-root">
        <div className="st-page">{children}</div>
      </main>
    </div>
  );
}
