import type { AppMatch } from "@macro/renderer";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { AppMatchesEditor } from "./AppMatchesEditor";
import { CommitTextInput, Field } from "./fields/controls";

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
    <>
      <div className="pf-section head">
        <span className="section-label">{t("profile.properties.title")}</span>
      </div>

      <div className="pf-section">
        <div className="pf-body">
          <Field label={t("profile.properties.name")}>
            <CommitTextInput key={target.id} value={target.name} disabled={!isCurrent} onCommit={(v) => { if (v && v !== target.name) onRename(v); }} />
          </Field>

          {isCurrent ? (
            <AppMatchesEditor matches={appMatches} onChange={onAppMatchesChange} />
          ) : (
            <p className="pf-hint">{t("profile.properties.switching")}</p>
          )}
        </div>
      </div>

      <div className="pf-section">
        <button
          className="ghost pf-btn pf-danger"
          disabled={!isCurrent || !canDelete}
          onClick={async () => { if (await confirmAsync(t("profile.deleteConfirm", target.name), { title: t("profile.delete"), danger: true })) onDelete(); }}
        >
          {t("profile.properties.delete")}
        </button>
      </div>
    </>
  );
}
