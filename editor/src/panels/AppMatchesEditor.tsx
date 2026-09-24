import { useEffect, useState } from "react";
import { Trash2 } from "lucide-react";
import type { AppMatch } from "@macro/renderer";
import type { RunningWindowInfo } from "../api/types";
import { api } from "../api/client";
import { useT } from "../i18n/I18nContext";
import { SectionLabel } from "./fields/controls";

/** "Activate automatically" — the profile's foreground-window auto-switch rules (docs/auto-profile-switch.md).
 * Lives inside ProfilePagesPanel, toggled open by the AppWindow icon next to rename/new/delete. */
export function AppMatchesEditor({ matches, onChange }: { matches: AppMatch[]; onChange: (matches: AppMatch[]) => void }) {
  const { t } = useT();
  const [running, setRunning] = useState<RunningWindowInfo[]>([]);
  const [manualName, setManualName] = useState("");

  useEffect(() => {
    api.listRunningWindows().then(setRunning).catch(() => {});
  }, []);

  const add = (processName: string) => {
    const trimmed = processName.trim();
    if (!trimmed || matches.some((m) => m.processName.toLowerCase() === trimmed.toLowerCase())) return;
    onChange([...matches, { processName: trimmed }]);
  };

  const remove = (processName: string) => onChange(matches.filter((m) => m.processName !== processName));

  return (
    <div style={{ padding: "8px 10px", borderTop: "1px solid var(--ms-border)", background: "var(--ms-bg-inset)" }}>
      <SectionLabel>{t("profile.autoSwitch")}</SectionLabel>
      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: "2px 0 8px" }}>{t("profile.autoSwitch.hint")}</p>

      {matches.map((m) => (
        <div key={m.processName} style={{ display: "flex", alignItems: "center", gap: 6, padding: "4px 0" }}>
          <span style={{ flex: 1, fontSize: 12, fontFamily: "ui-monospace, monospace" }}>{m.processName}</span>
          <button type="button" className="ghost" title={t("profile.autoSwitch.remove")} onClick={() => remove(m.processName)} style={{ padding: 4, color: "var(--ms-danger)" }}>
            <Trash2 size={12} />
          </button>
        </div>
      ))}

      <div style={{ display: "flex", gap: 6, marginTop: 6 }}>
        <select
          value=""
          onChange={(e) => { if (e.target.value) add(e.target.value); }}
          style={{ flex: 1, fontSize: 11.5, minWidth: 0 }}
          title={t("profile.autoSwitch.pickRunning")}
        >
          <option value="">{t("profile.autoSwitch.pickRunning")}</option>
          {running
            .filter((w) => !matches.some((m) => m.processName.toLowerCase() === w.processName.toLowerCase()))
            .map((w) => (
              <option key={w.processName} value={w.processName}>{w.processName} — {w.title}</option>
            ))}
        </select>
      </div>
      <div style={{ display: "flex", gap: 6, marginTop: 6 }}>
        <input
          type="text"
          placeholder={t("profile.autoSwitch.manualPlaceholder")}
          value={manualName}
          onChange={(e) => setManualName(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter" && manualName.trim()) { add(manualName); setManualName(""); } }}
          style={{ flex: 1, fontSize: 11.5, minWidth: 0 }}
        />
        <button
          type="button"
          className="ghost"
          disabled={!manualName.trim()}
          onClick={() => { add(manualName); setManualName(""); }}
        >
          {t("profile.autoSwitch.add")}
        </button>
      </div>
    </div>
  );
}
