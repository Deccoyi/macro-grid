import { Globe, Lock, ShieldAlert, Smartphone, TriangleAlert, Unlock } from "lucide-react";
import { useT } from "../i18n/I18nContext";
import { Row, tint } from "./InstallConsent";

/** One site an imported profile would open on a phone: the host name and whether the connection is encrypted. */
export interface WebImportSite {
  host: string;
  secure: boolean;
}

/** The body of the "this profile opens web pages" dialog, shown when a profile is imported. Same look as the plugin install consent:
 * a header tile, what it means, one row per site, and a notice. Only host names are shown (the rest of an address may hold a secret). */
export function WebImportConsent({ sites }: { sites: WebImportSite[] }) {
  const { t, tn } = useT();
  const accent = "var(--ms-accent)";
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
        <span style={{
          width: 44, height: 44, flexShrink: 0, borderRadius: 10, display: "flex", alignItems: "center", justifyContent: "center",
          color: accent, background: tint(accent, 16),
        }}>
          <Globe size={24} />
        </span>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 14, fontWeight: 600 }}>{tn("importWeb.count", sites.length)}</div>
          <div style={{ fontSize: 11.5, color: accent }}>{t("importWeb.kind")}</div>
        </div>
      </div>

      <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("importWeb.lead")}</div>

      {/* A long list scrolls inside its own box, so the notice and the buttons below never leave the dialog. */}
      <ul style={{ margin: 0, padding: 0, maxHeight: 176, overflowY: "auto" }}>
        {sites.map((s) => (
          <Row
            key={s.host}
            icon={s.secure ? <Lock size={14} /> : <Unlock size={14} />}
            chip={s.secure ? undefined : { text: t("importWeb.notEncrypted"), color: "var(--ms-warning, #facc15)" }}
          >
            {s.host}
          </Row>
        ))}
      </ul>

      <ul style={{ margin: 0, padding: 0, borderTop: "1px solid var(--ms-border)", paddingTop: 6 }}>
        <Row icon={<Smartphone size={14} />}>{t("importWeb.runsOnPhone")}</Row>
        <Row icon={<ShieldAlert size={14} />}>{t("importWeb.limits")}</Row>
      </ul>

      <div style={{ display: "flex", gap: 8, alignItems: "flex-start", padding: "8px 10px", borderRadius: 6, fontSize: 12, lineHeight: 1.45,
        color: "var(--ms-warning, #facc15)", background: tint("var(--ms-warning, #facc15)", 12) }}>
        <TriangleAlert size={14} style={{ flexShrink: 0, marginTop: 1 }} />
        <span>{t("importWeb.notice")}</span>
      </div>
    </div>
  );
}
