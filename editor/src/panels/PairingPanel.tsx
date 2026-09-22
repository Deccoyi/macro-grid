import { useEffect, useState } from "react";
import { RefreshCw, Smartphone, Trash2, X } from "lucide-react";
import { api } from "../api/client";
import type { PairedDeviceInfo } from "../api/types";

export interface PairingPanelProps {
  onClose: () => void;
}

/** "Eşleştirme" modal: the PIN a brand-new device must enter, plus the list of already-paired ones. */
export function PairingPanel({ onClose }: PairingPanelProps) {
  const [pin, setPin] = useState<string | null>(null);
  const [devices, setDevices] = useState<PairedDeviceInfo[] | null>(null);

  const refreshDevices = () => api.listDevices().then(setDevices).catch(() => {});

  useEffect(() => {
    api.pairingPin().then(setPin).catch(() => {});
    refreshDevices();
  }, []);

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 60, display: "flex", alignItems: "center", justifyContent: "center" }} onClick={onClose}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 420, background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border)", borderRadius: 10, boxShadow: "0 24px 60px rgba(0,0,0,.5)", display: "flex", flexDirection: "column" }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "16px 20px", borderBottom: "1px solid var(--ms-border)" }}>
          <div style={{ fontSize: 14, fontWeight: 600, flex: 1 }}>Eşleştirme</div>
          <button type="button" className="ghost" onClick={onClose} aria-label="Kapat" style={{ display: "flex", padding: 5 }}>
            <X size={18} />
          </button>
        </div>

        <div style={{ padding: "18px 20px", display: "flex", flexDirection: "column", gap: 16 }}>
          <div>
            <div className="section-label">Yeni cihaz için PIN</div>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <div style={{ fontFamily: "ui-monospace, monospace", fontSize: 32, fontWeight: 700, letterSpacing: ".1em", flex: 1, textAlign: "center", padding: "10px 0", background: "var(--ms-bg-inset)", border: "1px solid var(--ms-border)", borderRadius: 8 }}>
                {pin ?? "······"}
              </div>
              <button
                type="button"
                className="ghost"
                title="Yeni PIN üret"
                onClick={() => api.regeneratePairingPin().then(setPin)}
                style={{ display: "flex", padding: 8 }}
              >
                <RefreshCw size={16} />
              </button>
            </div>
            <p style={{ fontSize: 11.5, color: "var(--ms-text-secondary)", margin: "8px 0 0" }}>
              Telefon uygulamasında sunucu adresini girip bağlanınca bu PIN'i bir kez girmesi istenir. Eşleştikten sonra
              cihaz kendi belirteciyle (token) bağlanır, PIN tekrar sorulmaz.
            </p>
          </div>

          <div>
            <div className="section-label">Eşleşmiş cihazlar</div>
            {devices === null && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>Yükleniyor…</div>}
            {devices?.length === 0 && <div style={{ fontSize: 12, color: "var(--ms-text-secondary)" }}>Henüz eşleşmiş cihaz yok.</div>}
            {devices?.map((d) => (
              <div key={d.id} style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 0", borderTop: "1px solid var(--ms-border)" }}>
                <Smartphone size={14} color="var(--ms-text-secondary)" />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 13 }}>{d.name}</div>
                  <div style={{ fontSize: 11, color: "var(--ms-text-disabled)" }}>Son görülme: {new Date(d.lastSeenAt).toLocaleString("tr-TR")}</div>
                </div>
                <button
                  type="button"
                  className="ghost"
                  title="Eşleşmeyi kaldır"
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
