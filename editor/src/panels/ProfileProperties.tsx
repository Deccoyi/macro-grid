import type { AppMatch } from "@macro/renderer";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { AppMatchesEditor } from "./AppMatchesEditor";
import { SectionLabel } from "./fields/controls";

interface ProfilePropertiesProps {
  target: { id: string; name: string };
  /** Editing (rename, auto-switch rules, delete) only works for the currently loaded profile — the
   * editor never holds a second profile's document in memory, so a not-yet-switched-to target shows a
   * brief "switching…" state instead (see HierarchyToolWindow/WorkspaceUiContext). */
  isCurrent: boolean;
  appMatches: AppMatch[];
  onAppMatchesChange: (matches: AppMatch[]) => void;
  onRename: (name: string) => void;
  onDelete: () => void;
  canDelete: boolean;
}

/** Properties view for a profile clicked in Hierarchy — replaces the page/widget inspector while a
 * profile is the current selection (WorkspaceUiContext's profilePropertiesTarget). Auto-switch rules used
 * to live inline in the old Hierarchy profile list; they're properties of the profile, so they belong here instead. */
export function ProfileProperties({ target, isCurrent, appMatches, onAppMatchesChange, onRename, onDelete, canDelete }: ProfilePropertiesProps) {
  const { t } = useT();

  return (
    <div style={{ padding: 12, display: "flex", flexDirection: "column", gap: 14, overflowY: "auto", height: "100%" }}>
      <SectionLabel>{t("profile.properties.title")}</SectionLabel>

      <label className="field">
        {t("profile.properties.name")}
        <input
          type="text"
          defaultValue={target.name}
          key={target.id}
          disabled={!isCurrent}
          onBlur={(e) => { const v = e.target.value.trim(); if (v && v !== target.name) onRename(v); }}
          onKeyDown={(e) => { if (e.key === "Enter") (e.target as HTMLInputElement).blur(); }}
        />
      </label>

      {isCurrent ? (
        <AppMatchesEditor matches={appMatches} onChange={onAppMatchesChange} />
      ) : (
        <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)" }}>{t("profile.properties.switching")}</p>
      )}

      <hr className="sep" />

      <button
        className="ghost"
        style={{ color: "var(--ms-danger)" }}
        disabled={!isCurrent || !canDelete}
        onClick={async () => { if (await confirmAsync(t("profile.deleteConfirm", target.name), { title: t("profile.delete"), danger: true })) onDelete(); }}
      >
        {t("profile.properties.delete")}
      </button>
    </div>
  );
}
