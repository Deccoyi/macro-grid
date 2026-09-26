import { useEffect, useState } from "react";
import QRCode from "qrcode";
import { RefreshCw, Smartphone, Trash2 } from "lucide-react";
import { api } from "../api/client";
import type { PairedDeviceInfo, PairingQrInfo, ProfileSummary } from "../api/types";
import { useT } from "../i18n/I18nContext";
import { useDocumentTitle } from "../i18n/useDocumentTitle";
import { SectionLabel } from "../panels/fields/controls";
import { ToolWindowLayout } from "./ToolWindowLayout";

type Category = "code" | "devices";

/** Well under the server's 15-second pairing lease. */
const PAIRING_POLL_MS = 3000;

/** The whole page of the "Pairing" tool window (see ToolWindow.cs) — a real separate, non-modal OS
 * window using the same left-categories/right-content shell as Plugins/Preferences/Help, instead of the
 * old centered modal-over-a-dark-backdrop (see docs/ui/ui-guidelines.md: Device Manager-style screens get
 * their own window, never an in-page overlay). A fresh, short-lived PIN/QR is issued every time this
 * window opens: a static code that stayed valid would defeat the point of scanning it. */
export function PairingWindow() {
  const { t, lang } = useT();
  useDocumentTitle("pairing.title");
  const [category, setCategory] = useState<Category>("code");
  const [qr, setQr] = useState<PairingQrInfo | null>(null);
  const [qrImage, setQrImage] = useState<string | null>(null);
  const [devices, setDevices] = useState<PairedDeviceInfo[] | null>(null);
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);

  const refreshDevices = () => api.listDevices().then(setDevices).catch(() => {});
  const refreshQr = () => api.regeneratePairingQr().then(setQr).catch(() => {});

  useEffect(() => {
    refreshQr();
    refreshDevices();
    api.listProfiles().then(setProfiles).catch(() => {});
  }, []);

  // Pairing is open only while this window keeps asking for the code (see PairingService on the server):
  // polling keeps it open and picks up a new PIN after one was used, expired or replaced after wrong tries.
  // Closing the window stops the polling and pairing closes by itself a few seconds later.
  useEffect(() => {
    const timer = setInterval(() => {
      api
        .pairingQr()
        .then((next) => setQr((prev) => (prev && prev.pin === next.pin && prev.text === next.text ? prev : next)))
        .catch(() => {});
      refreshDevices();
    }, PAIRING_POLL_MS);
    return () => clearInterval(timer);
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

  const categories = [
    { id: "code", label: t("pairing.newDeviceCode") },
    { id: "devices", label: t("pairing.pairedDevices") },
  ];

  return (
    <ToolWindowLayout categories={categories} activeId={category} onSelect={(id) => setCategory(id as Category)}>
      {category === "code" ? (
        <div style={{ maxWidth: 260 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
            <SectionLabel>{t("pairing.newDeviceCode")}</SectionLabel>
            <div style={{ flex: 1 }} />
            <button type="button" className="ghost" title={t("pairing.regenerate")} onClick={refreshQr} style={{ display: "flex", padding: 6 }}>
              <RefreshCw size={14} />
            </button>
          </div>

          {qr && !qr.text && (
            <p style={{ fontSize: 12, color: "var(--ms-danger)", margin: 0 }}>{t("pairing.noLan")}</p>
          )}

          {qr?.text && (
            <div style={{ display: "flex", justifyContent: "center", padding: "8px 0" }}>
              {qrImage
                ? <img src={qrImage} width={180} height={180} alt={t("pairing.title")} style={{ borderRadius: 4, background: "#fff", padding: 8 }} />
                : <div style={{ width: 180, height: 180, borderRadius: 4, background: "var(--ms-bg-inset)" }} />}
            </div>
          )}

          <div style={{ fontFamily: "ui-monospace, monospace", fontSize: 24, fontWeight: 700, letterSpacing: ".1em", textAlign: "center", padding: "8px 0", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 4 }}>
            {qr?.pin ?? "······"}
          </div>

          <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: "8px 0 0" }}>
            {t("pairing.instructions")}
          </p>

          {qr?.host && (
            <div style={{ marginTop: 14, paddingTop: 14, borderTop: "1px solid var(--ms-border)" }}>
              <SectionLabel>{t("pairing.browser")}</SectionLabel>
              <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: "4px 0 8px" }}>{t("pairing.browser.hint")}</p>
              <a
                href={`http://${qr.host}:${qr.port}/deck/`}
                target="_blank"
                rel="noreferrer"
                style={{ fontFamily: "ui-monospace, monospace", fontSize: 13, color: "var(--ms-accent, #60a5fa)", wordBreak: "break-all" }}
              >
                {`http://${qr.host}:${qr.port}/deck/`}
              </a>
            </div>
          )}
        </div>
      ) : (
        <div style={{ maxWidth: 420 }}>
          <SectionLabel>{t("pairing.pairedDevices")}</SectionLabel>
          {devices === null && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("pairing.loading")}</div>}
          {devices?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)", marginTop: 8 }}>{t("pairing.noDevices")}</div>}
          {devices?.map((d) => (
            <div key={d.id} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 0", borderTop: "1px solid var(--ms-border)" }}>
              <Smartphone size={14} color="var(--ms-text-secondary)" />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontSize: 13 }}>{d.name}</div>
                <div style={{ fontSize: 11, color: "var(--ms-text-disabled)" }}>{t("pairing.lastSeen", new Date(d.lastSeenAt).toLocaleString(lang === "en" ? "en-US" : "tr-TR"))}</div>
              </div>
              <select
                value={d.assignedProfileId ?? ""}
                title={t("pairing.deviceProfile")}
                onChange={(e) => {
                  const profileId = e.target.value || null;
                  setDevices((prev) => prev?.map((x) => (x.id === d.id ? { ...x, assignedProfileId: profileId } : x)) ?? prev);
                  api.assignDeviceProfile(d.id, profileId).catch(refreshDevices);
                }}
                style={{ maxWidth: 130, fontSize: 12 }}
              >
                <option value="">{t("pairing.deviceProfile.default")}</option>
                {profiles.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <label title={t("pairing.followActiveWindow.hint")} style={{ display: "flex", alignItems: "center", gap: 4, fontSize: 11, color: "var(--ms-text-secondary)", whiteSpace: "nowrap" }}>
                <input
                  type="checkbox"
                  checked={d.followActiveWindow}
                  onChange={(e) => {
                    const followActiveWindow = e.target.checked;
                    setDevices((prev) => prev?.map((x) => (x.id === d.id ? { ...x, followActiveWindow } : x)) ?? prev);
                    api.setDeviceFollowActiveWindow(d.id, followActiveWindow).catch(refreshDevices);
                  }}
                />
                {t("pairing.followActiveWindow")}
              </label>
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
      )}
    </ToolWindowLayout>
  );
}
