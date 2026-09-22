import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { RefreshCw, Smartphone, Trash2, X } from "lucide-react";
import { api } from "../api/client";
import type { PairedDeviceInfo, PairingQrInfo } from "../api/types";
import { useT } from "../i18n/I18nContext";

export interface PairingPanelProps {
  onClose: () => void;
}

/** "Eşleştirme" modal: a QR code (and fallback PIN text) a brand-new device scans/enters to join, plus
 * the list of already-paired ones. A fresh, short-lived PIN/QR is issued every time this panel opens —
 * see docs/agent-notes.md on why a static code would defeat the point of scanning it. */
export function PairingPanel({ onClose }: PairingPanelProps) {
  const { t, lang } = useT();
  const [qr, setQr] = useState<PairingQrInfo | null>(null);
  const [qrImage, setQrImage] = useState<string | null>(null);
  const [devices, setDevices] = useState<PairedDeviceInfo[] | null>(null);

  const refreshDevices = () => api.listDevices().then(setDevices).catch(() => {});
  const refreshQr = () => api.regeneratePairingQr().then(setQr).catch(() => {});

  useEffect(() => {
    refreshQr();
    refreshDevices();
  }, []);

  useEffect(() => {
    if (!qr?.text) {
      setQrImage(null);
      return;
    }
    let cancelled = false;
    QRCode.toDataURL(qr.text, { margin: 1, width: 220 })
      .then((url) => { if (!cancelled) setQrImage(url); })
      .catch(() => { if (!cancelled) setQrImage(null); });
    return () => { cancelled = true; };
  }, [qr?.text]);

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 60, display: "flex", alignItems: "center", justifyContent: "center" }} onClick={onClose}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 420, background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border)", borderRadius: 10, boxShadow: "0 24px 60px rgba(0,0,0,.5)", display: "flex", flexDirection: "column" }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "16px 20px", borderBottom: "1px solid var(--ms-border)" }}>
          <div style={{ fontSize: 14, fontWeight: 600, flex: 1 }}>{t("pairing.title")}</div>
          <button type="button" className="ghost" onClick={onClose} aria-label={t("header.close")} style={{ display: "flex", padding: 5 }}>
            <X size={18} />
          </button>
        </div>

        <div style={{ padding: "18px 20px", display: "flex", flexDirection: "column", gap: 16 }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <div className="section-label" style={{ flex: 1 }}>{t("pairing.newDeviceCode")}</div>
              <button
                type="button"
                className="ghost"
                title={t("pairing.regenerate")}
                onClick={refreshQr}
                style={{ display: "flex", padding: 6 }}
              >
                <RefreshCw size={14} />
              </button>
            </div>

            {qr && !qr.text && (
              <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: 0 }}>
                {t("pairing.noLan")}
              </p>
            )}

            {qr?.text && (
              <div style={{ display: "flex", justifyContent: "center", padding: "8px 0" }}>
                {qrImage
                  ? <img src={qrImage} width={180} height={180} alt={t("pairing.title")} style={{ borderRadius: 8, background: "#fff", padding: 8 }} />
                  : <div style={{ width: 180, height: 180, borderRadius: 8, background: "var(--ms-bg-inset)" }} />}
              </div>
            )}

            <div style={{ fontFamily: "ui-monospace, monospace", fontSize: 24, fontWeight: 700, letterSpacing: ".1em", textAlign: "center", padding: "8px 0", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 8 }}>
              {qr?.pin ?? "······"}
            </div>

            <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: "8px 0 0" }}>
              {t("pairing.instructions")}
            </p>
          </div>

          <div>
            <div className="section-label">{t("pairing.pairedDevices")}</div>
            {devices === null && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pairing.loading")}</div>}
            {devices?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>{t("pairing.noDevices")}</div>}
            {devices?.map((d) => (
              <div key={d.id} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 0", borderTop: "1px solid var(--ms-border)" }}>
                <Smartphone size={14} color="var(--ms-text-secondary)" />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13 }}>{d.name}</div>
                  <div style={{ fontSize: 11, color: "var(--ms-text-disabled)" }}>{t("pairing.lastSeen", new Date(d.lastSeenAt).toLocaleString(lang === "en" ? "en-US" : "tr-TR"))}</div>
                </div>
                <button
                  type="button"
                  className="ghost"
                  title={t("pairing.revoke")}
                  onClick={() => api.revokeDevice(d.id).then(refreshDevices)}
                  style={{ display: "flex", padding: 5, color: "var(--ms-danger)" }}
                >
                  <Trash2 size={14} />
                </button>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
