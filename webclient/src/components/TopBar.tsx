import type { CSSProperties } from "react";
import { t } from "../i18n";
import type { ConnectionStatus, ProfileSummary } from "../ws/connection";

interface TopBarProps {
  status: ConnectionStatus;
  profiles: ProfileSummary[];
  currentProfileId: string;
  onPickProfile: (id: string) => void;
  pageName: string;
  canNav: boolean;
  onPrevPage: () => void;
  onNextPage: () => void;
}

const barStyle: CSSProperties = {
  display: "flex", alignItems: "center", gap: 10, padding: "8px 12px",
  background: "#16181c", borderBottom: "1px solid #2d3136", flexWrap: "wrap",
};

const titleStyle: CSSProperties = { color: "#e6e7ea", fontSize: 13, fontWeight: 600 };

const profileSelectStyle: CSSProperties = {
  background: "#0b0d10", color: "#e6e7ea", border: "1px solid #2d3136", borderRadius: 6, padding: "4px 8px", fontSize: 12.5,
};

const pageNavStyle: CSSProperties = { display: "flex", alignItems: "center", gap: 6, marginLeft: "auto" };

const pageNameStyle: CSSProperties = { color: "#9aa0a8", fontSize: 12, minWidth: 60, textAlign: "center" };

const navButtonStyle: CSSProperties = {
  padding: "4px 10px", fontSize: 14, borderRadius: 6, border: "1px solid #2d3136",
  background: "transparent", color: "#e6e7ea", cursor: "pointer",
};

export function TopBar({
  status,
  profiles,
  currentProfileId,
  onPickProfile,
  pageName,
  canNav,
  onPrevPage,
  onNextPage,
}: TopBarProps) {
  return (
    <div style={barStyle}>
      <span style={titleStyle}>Macro Grid</span>

      {profiles.length > 1 && (
        <select value={currentProfileId} onChange={(e) => onPickProfile(e.target.value)} style={profileSelectStyle}>
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      )}

      {canNav && (
        <div style={pageNavStyle}>
          <button onClick={onPrevPage} style={navButtonStyle}>←</button>
          <span style={pageNameStyle}>{pageName}</span>
          <button onClick={onNextPage} style={navButtonStyle}>→</button>
        </div>
      )}

      <StatusBadge status={status} pushRight={!canNav} />
    </div>
  );
}

function StatusBadge({ status, pushRight }: { status: ConnectionStatus; pushRight: boolean }) {
  const connected = status === "connected";
  const style: CSSProperties = {
    marginLeft: pushRight ? "auto" : 0, fontSize: 11, padding: "3px 8px", borderRadius: 999,
    background: connected ? "rgba(34,197,94,.15)" : "rgba(239,68,68,.15)",
    color: connected ? "#4ade80" : status === "connecting" ? "#facc15" : "#ef4444",
  };
  const label = connected ? t("status.connected") : status === "connecting" ? t("status.connecting") : t("status.offline");
  return <span style={style}>{label}</span>;
}
