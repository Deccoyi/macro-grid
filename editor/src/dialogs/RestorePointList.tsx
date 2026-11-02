import { useState } from "react";
import type { RestorePointInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";

const REASONS = new Set(["restore", "delete", "import", "update"]);

/** The reason in the person's language; a reason this editor does not know is shown as it is. */
export const restorePointReason = (t: (key: DictKey) => string, reason: string) =>
  REASONS.has(reason) ? t(`restore.point.reason.${reason}` as DictKey) : reason;

export function formatPointSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** The body of the "pick a restore point" dialog: one choice, newest first. The caller reads `chosen.name` after the person confirms. */
export function RestorePointList({ points, chosen }: { points: RestorePointInfo[]; chosen: { name: string | null } }) {
  const { t } = useT();
  const [, redraw] = useState(0);
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8, fontSize: 12.5, color: "var(--ms-text-secondary)", maxHeight: 360, overflowY: "auto" }}>
      <p style={{ margin: 0 }}>{t("restore.point.intro")}</p>
      <ul style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 6 }}>
        {points.map((p) => (
          <li key={p.fileName}>
            <label style={{ display: "flex", gap: 8, alignItems: "center", cursor: "pointer" }}>
              <input type="radio" name="restore-point" checked={chosen.name === p.fileName} onChange={() => { chosen.name = p.fileName; redraw((n) => n + 1); }} />
              <span style={{ color: "var(--ms-text)" }}>{new Date(p.createdAt).toLocaleString()}</span>
              <span>{restorePointReason(t, p.reason)}</span>
              <span style={{ marginLeft: "auto", opacity: 0.7 }}>{formatPointSize(p.size)}</span>
            </label>
          </li>
        ))}
      </ul>
    </div>
  );
}
