import { widgetOptionOf } from "./widgetPermission";
import type { ReactNode } from "react";
import { Braces, FolderOpen, Globe, Keyboard, ShieldAlert, ShieldCheck, Terminal, TriangleAlert, Zap } from "lucide-react";
import { useT } from "../i18n/I18nContext";
import { httpTargetScope, type HttpTargetScope } from "./httpTarget";

const SCOPE_COLOR: Record<HttpTargetScope, string> = {
  local: "var(--ms-success, #4ade80)",
  lan: "var(--ms-warning, #facc15)",
  internet: "var(--ms-danger)",
};

export const tint = (color: string, percent = 16) => `color-mix(in srgb, ${color} ${percent}%, transparent)`;

export function Row({ icon, children, chip }: { icon: ReactNode; children: ReactNode; chip?: { text: string; color: string } }) {
  return (
    <li style={{ display: "flex", alignItems: "center", gap: 10, padding: "7px 0", listStyle: "none" }}>
      <span style={{
        width: 26, height: 26, flexShrink: 0, borderRadius: 6, display: "flex", alignItems: "center", justifyContent: "center",
        background: "var(--ms-bg-surface-raised)", color: "var(--ms-text-secondary)",
      }}>{icon}</span>
      <span style={{ flex: 1, fontSize: 12.5, lineHeight: 1.4 }}>{children}</span>
      {chip && (
        <span style={{ fontSize: 10.5, padding: "1px 8px", borderRadius: 999, color: chip.color, background: tint(chip.color), flexShrink: 0 }}>
          {chip.text}
        </span>
      )}
    </li>
  );
}

/** The body of the "before you install this plugin" dialog — shared by every install path (folder, catalog,
 * pasted link). A native (C#) plugin has no permission gate by design (docs/plans/security-hardening-plan.md), so
 * it gets a full-access warning; a JS plugin lists the permissions it asks for, each with what it means. */
export function InstallConsent({ name, kind, permissions = [], sourceLabel }: {
  name: string;
  kind: string | undefined;
  permissions?: string[];
  /** Set for a plugin from somewhere other than the official source: "owner/repo". */
  sourceLabel?: string;
}) {
  const { t } = useT();
  const native = kind !== "js";
  const accent = native ? "var(--ms-danger)" : "var(--ms-accent)";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      {sourceLabel && (
        <div style={{ display: "flex", gap: 8, alignItems: "flex-start", padding: "8px 10px", borderRadius: 6, fontSize: 12, lineHeight: 1.45,
          color: "var(--ms-warning, #facc15)", background: tint("var(--ms-warning, #facc15)", 12) }}>
          <TriangleAlert size={14} style={{ flexShrink: 0, marginTop: 1 }} />
          <span>{t("consent.thirdParty", sourceLabel)}</span>
        </div>
      )}

      <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
        <span style={{
          width: 44, height: 44, flexShrink: 0, borderRadius: 10, display: "flex", alignItems: "center", justifyContent: "center",
          color: accent, background: tint(accent, 16),
        }}>
          {native ? <ShieldAlert size={24} /> : <ShieldCheck size={24} />}
        </span>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 14, fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{name}</div>
          <div style={{ fontSize: 11.5, color: accent }}>{native ? t("consent.native.kind") : t("consent.js.kind")}</div>
        </div>
      </div>

      <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>
        {native ? t("consent.native.lead") : permissions.length > 0 ? t("consent.js.lead") : t("consent.js.none")}
      </div>

      {native ? (
        <ul style={{ margin: 0, padding: 0 }}>
          <Row icon={<FolderOpen size={14} />}>{t("consent.native.files")}</Row>
          <Row icon={<Terminal size={14} />}>{t("consent.native.programs")}</Row>
          <Row icon={<Globe size={14} />}>{t("consent.native.network")}</Row>
        </ul>
      ) : permissions.length > 0 && (
        <ul style={{ margin: 0, padding: 0 }}>
          {permissions.map((p) => <PermissionRow key={p} permission={p} />)}
        </ul>
      )}

      {native && (
        <div style={{ padding: "8px 10px", borderRadius: 6, fontSize: 12, lineHeight: 1.45, color: "var(--ms-danger)", background: tint("var(--ms-danger)", 12) }}>
          {t("consent.native.notice")}
        </div>
      )}
    </div>
  );
}

function PermissionRow({ permission }: { permission: string }) {
  const { t } = useT();
  if (permission === "variables") return <Row icon={<Braces size={14} />}>{t("plugins.permission.variables")}</Row>;
  if (permission === "actions") return <Row icon={<Zap size={14} />}>{t("plugins.permission.actions")}</Row>;
  if (permission === "input") return <Row icon={<Keyboard size={14} />}>{t("plugins.permission.input")}</Row>;
  const widgetOption = widgetOptionOf(permission);
  if (widgetOption) return <Row icon={<ShieldCheck size={14} />}>{t(`plugins.permission.widget.${widgetOption}`)}</Row>;
  if (permission.startsWith("http:")) {
    const target = permission.slice(5);
    const scope = httpTargetScope(target);
    return (
      <Row icon={<Globe size={14} />} chip={{ text: t(`consent.scope.${scope}`), color: SCOPE_COLOR[scope] }}>
        {t("consent.http", target)}
      </Row>
    );
  }
  return <Row icon={<ShieldCheck size={14} />}>{t("plugins.permission.unknown", permission)}</Row>;
}
