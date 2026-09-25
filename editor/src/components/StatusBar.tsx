import { lazy, Suspense, useMemo } from "react";
import dynamicIconImports from "lucide-react/dynamicIconImports";
import { Circle } from "lucide-react";
import { api } from "../api/client";
import type { StatusEntry, StatusLevel } from "../api/types";

const LEVEL_COLOR: Record<StatusLevel, string> = {
  Idle: "var(--ms-text-disabled)",
  Ok: "var(--ms-success, #4ade80)",
  Busy: "var(--ms-accent)",
  Warning: "var(--ms-warning, #facc15)",
  Error: "var(--ms-danger)",
};

function StatusIcon({ name }: { name?: string | null }) {
  const Loaded = useMemo(() => {
    const load = name ? dynamicIconImports[name as keyof typeof dynamicIconImports] : undefined;
    return load ? lazy(load) : null;
  }, [name]);
  if (!Loaded) return null;
  return (
    <Suspense fallback={null}>
      <Loaded size={12} />
    </Suspense>
  );
}

function StatusChip({ entry, onClick }: { entry: StatusEntry; onClick?: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={entry.tooltip ?? entry.text}
      className={onClick ? "ghost" : undefined}
      disabled={!onClick}
      style={{
        display: "flex", alignItems: "center", gap: 5, fontSize: 11.5, padding: "2px 6px",
        color: "var(--ms-text-secondary)", background: "none", border: "none",
        cursor: onClick ? "pointer" : "default",
      }}
    >
      <StatusIcon name={entry.icon} />
      <span>{entry.text}</span>
      <Circle size={7} fill={LEVEL_COLOR[entry.level]} color={LEVEL_COLOR[entry.level]} />
    </button>
  );
}

/**
 * Window-wide status bar — the last row of App.tsx's root grid. Core items (server version, connected
 * device count) sit on the left; plugin-owned items (see IPluginHost.CreateStatusItem, e.g. a connection state)
 * on the right. Clicking a plugin item opens that plugin's settings window; a plugin
 * with no registered IPluginSettingsPage just shows an empty window (see PluginSettingsWindow.tsx).
 */
export function StatusBar({ items }: { items: StatusEntry[] }) {
  const core = items.filter((i) => i.pluginId === "core");
  const plugins = items.filter((i) => i.pluginId !== "core");

  return (
    <footer
      style={{
        display: "flex", alignItems: "center", borderTop: "1px solid var(--ms-border)",
        padding: "0 8px", height: 26, background: "var(--ms-bg-surface)",
      }}
    >
      <div style={{ display: "flex", alignItems: "center" }}>
        {core.map((entry) => (
          <StatusChip key={entry.id} entry={entry} onClick={entry.id === "update" ? () => api.openToolWindow("update") : undefined} />
        ))}
      </div>
      <div style={{ flex: 1 }} />
      <div style={{ display: "flex", alignItems: "center" }}>
        {plugins.map((entry) => (
          <StatusChip key={`${entry.pluginId}.${entry.id}`} entry={entry} onClick={() => api.openPluginSettingsWindow(entry.pluginId)} />
        ))}
      </div>
    </footer>
  );
}
