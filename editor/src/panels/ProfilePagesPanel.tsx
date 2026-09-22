import { useState } from "react";
import { Copy, Pencil, Plus, Trash2 } from "lucide-react";
import type { Page } from "@macro/renderer";
import type { ProfileSummary } from "../api/types";
import { confirmAsync, promptAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "./fields/controls";

export interface ProfilePagesPanelProps {
  profile: { id: string; name: string };
  profiles: ProfileSummary[];
  pages: Page[];
  currentPageId: string | null;
  onSelectProfile: (id: string) => void;
  onCreateProfile: () => void;
  onRenameProfile: (name: string) => void;
  onDeleteProfile: () => void;
  canDeleteProfile: boolean;
  onSelectPage: (pageId: string) => void;
  onAddPage: () => void;
  onRenamePage: (pageId: string, name: string) => void;
  onDuplicatePage: (pageId: string) => void;
  onDeletePage: (pageId: string) => void;
  onPageContextMenu: (x: number, y: number, pageId: string) => void;
}

/** Left sidebar, top half — a layer-panel-style tree: current profile at top, its pages below.
 * Replaces the old top-bar profile controls and the separate PageTabs strip (see docs/ui-guidelines.md:51). */
export function ProfilePagesPanel({
  profile,
  profiles,
  pages,
  currentPageId,
  onSelectProfile,
  onCreateProfile,
  onRenameProfile,
  onDeleteProfile,
  canDeleteProfile,
  onSelectPage,
  onAddPage,
  onRenamePage,
  onDuplicatePage,
  onDeletePage,
  onPageContextMenu,
}: ProfilePagesPanelProps) {
  const { t } = useT();
  const [hoveredPageId, setHoveredPageId] = useState<string | null>(null);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }}>
      <div style={{ padding: "10px 10px 8px", display: "flex", flexDirection: "column", gap: 6 }}>
        <SectionLabel>{t("profile.label")}</SectionLabel>
        <select value={profile.id} onChange={(e) => onSelectProfile(e.target.value)} style={{ width: "100%" }}>
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
        <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
          <button
            className="ghost"
            title={t("profile.rename")}
            onClick={async () => {
              const name = await promptAsync(t("profile.renamePrompt"), profile.name, { title: t("profile.rename") });
              if (name && name.trim()) onRenameProfile(name.trim());
            }}
            style={{ flex: 1, padding: "5px 0", display: "flex", justifyContent: "center" }}
          >
            <Pencil size={13} />
          </button>
          <button className="ghost" title={t("profile.new")} onClick={onCreateProfile} style={{ flex: 1, padding: "5px 0", display: "flex", justifyContent: "center" }}>
            <Plus size={13} />
          </button>
          <button
            className="ghost"
            title={t("profile.delete")}
            disabled={!canDeleteProfile}
            onClick={async () => {
              if (await confirmAsync(t("profile.deleteConfirm", profile.name), { title: t("profile.delete"), danger: true })) onDeleteProfile();
            }}
            style={{ color: "var(--ms-danger)", flex: 1, padding: "5px 0", display: "flex", justifyContent: "center" }}
          >
            <Trash2 size={13} />
          </button>
        </div>
      </div>

      <hr className="sep" style={{ margin: "0 0 6px" }} />

      <div style={{ padding: "0 10px 4px" }}>
        <SectionLabel>{t("pages.label")}</SectionLabel>
      </div>
      <div style={{ flex: 1, overflowY: "auto", padding: "0 6px" }}>
        {pages.map((page) => {
          const active = page.id === currentPageId;
          const hovered = page.id === hoveredPageId;
          return (
            <div
              key={page.id}
              onMouseEnter={() => setHoveredPageId(page.id)}
              onMouseLeave={() => setHoveredPageId((id) => (id === page.id ? null : id))}
              onClick={() => onSelectPage(page.id)}
              onDoubleClick={async () => {
                const name = await promptAsync(t("page.renamePrompt"), page.name, { title: t("page.rename") });
                if (name && name.trim()) onRenamePage(page.id, name.trim());
              }}
              onContextMenu={(e) => {
                e.preventDefault();
                onSelectPage(page.id);
                onPageContextMenu(e.clientX, e.clientY, page.id);
              }}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 4,
                padding: "6px 8px",
                borderRadius: 4,
                cursor: "pointer",
                background: active ? "var(--ms-accent-bg-muted)" : hovered ? "var(--ms-bg-surface-raised)" : "transparent",
                color: active ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
              }}
            >
              <span style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontSize: 12.5 }}>
                {page.name}
              </span>
              <button
                className="ghost"
                title={t("page.duplicate")}
                onClick={(e) => { e.stopPropagation(); onDuplicatePage(page.id); }}
                style={{ padding: "3px 5px", visibility: hovered ? "visible" : "hidden" }}
              >
                <Copy size={12} />
              </button>
              <button
                className="ghost"
                title={t("page.delete")}
                onClick={async (e) => {
                  e.stopPropagation();
                  if (await confirmAsync(t("page.deleteConfirm", page.name), { title: t("page.delete"), danger: true })) onDeletePage(page.id);
                }}
                style={{ padding: "3px 5px", color: "var(--ms-danger)", visibility: hovered && pages.length > 1 ? "visible" : "hidden" }}
              >
                <Trash2 size={12} />
              </button>
            </div>
          );
        })}
      </div>
      <div style={{ padding: 8, borderTop: "1px solid var(--ms-border)" }}>
        <button className="ghost" onClick={onAddPage} style={{ width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: 5 }}>
          <Plus size={13} /> {t("page.add")}
        </button>
      </div>
    </div>
  );
}
