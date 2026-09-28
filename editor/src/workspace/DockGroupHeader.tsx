import { useEffect, useState } from "react";
import { Pin, X } from "lucide-react";
import type { IDockviewPanelHeaderProps } from "dockview-react";
import { useT } from "../i18n/I18nContext";
import { toolWindowById } from "./toolWindows";
import type { ToolWindowPanelParams } from "./DockPanelFrame";
import { useWorkspace } from "./WorkspaceContext";

/** The tab/header for a tool window group — docs/ui/editor-icons.md + component-states.html: 28 px,
 * --ms-bg-surface, a 1 px --ms-border bottom line. The active tab of the focused group gets a 2 px
 * accent underline; other groups' active tabs stay neutral. Close removes the panel and pin unpins it to
 * its edge's auto-hide rail (WorkspaceContext owns both; its onDidRemovePanel listener tells a close from
 * an unpin). */
export function DockGroupHeader({ api }: IDockviewPanelHeaderProps<ToolWindowPanelParams>) {
  const { t } = useT();
  const { unpin } = useWorkspace();
  const [isActive, setIsActive] = useState(api.isActive);
  const [isGroupActive, setIsGroupActive] = useState(api.isGroupActive);

  useEffect(() => {
    const d1 = api.onDidActiveChange(() => setIsActive(api.isActive));
    const d2 = api.onDidActiveGroupChange(() => setIsGroupActive(api.isGroupActive));
    return () => { d1.dispose(); d2.dispose(); };
  }, [api]);

  const def = toolWindowById(api.id);
  const title = def ? t(def.titleKey) : (api.title ?? api.id);
  const accent = isActive && isGroupActive;

  return (
    <div
      style={{
        height: "100%",
        width: "100%",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0 4px 0 10px",
        fontSize: 12.5,
        fontWeight: 600,
        color: isActive ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
        borderBottom: accent ? "2px solid var(--ms-accent)" : "2px solid transparent",
        whiteSpace: "nowrap",
      }}
    >
      <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{title}</span>
      {isActive && (
        <span style={{ display: "flex", alignItems: "center", gap: 2, flex: "0 0 auto" }}>
          <HeaderIconButton label={t("panel.pin")} onClick={() => unpin(api.id)} accentOnHover>
            <Pin size={12} strokeWidth={2} />
          </HeaderIconButton>
          <HeaderIconButton label={t("panel.close")} onClick={() => api.close()}>
            <X size={12} strokeWidth={2} />
          </HeaderIconButton>
        </span>
      )}
    </div>
  );
}

function HeaderIconButton({ label, onClick, accentOnHover, children }: { label: string; onClick: () => void; accentOnHover?: boolean; children: React.ReactNode }) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      onClick={(e) => { e.stopPropagation(); onClick(); }}
      style={{
        width: 18,
        height: 18,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        border: "none",
        background: "transparent",
        color: "var(--ms-text-secondary)",
        cursor: "pointer",
        flex: "0 0 auto",
      }}
      onMouseEnter={(e) => { e.currentTarget.style.background = "var(--ms-bg-surface-raised)"; if (accentOnHover) e.currentTarget.style.color = "var(--ms-accent)"; }}
      onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; e.currentTarget.style.color = "var(--ms-text-secondary)"; }}
    >
      {children}
    </button>
  );
}
