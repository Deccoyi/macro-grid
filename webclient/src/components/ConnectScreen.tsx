import { useState, type CSSProperties } from "react";
import { t } from "../i18n";
import type { ConnectionStatus } from "../ws/connection";

const PIN_LENGTH = 6;

const screenStyle: CSSProperties = {
  display: "flex", flexDirection: "column", gap: 14, alignItems: "center", justifyContent: "center",
  minHeight: "100vh", background: "#0b0d10", color: "#e6e7ea", fontFamily: "system-ui, sans-serif", padding: 24,
  boxSizing: "border-box",
};

const headingStyle: CSSProperties = { fontSize: 20, margin: 0 };

const statusTextStyle: CSSProperties = { color: "#9aa0a8", fontSize: 13 };

const hintStyle: CSSProperties = { color: "#9aa0a8", fontSize: 13, textAlign: "center", margin: 0, maxWidth: 320 };

const pinInputStyle: CSSProperties = {
  width: "100%", maxWidth: 200, padding: "10px 12px", fontSize: 28, borderRadius: 8, textAlign: "center",
  letterSpacing: ".2em", fontFamily: "ui-monospace, monospace",
  border: "1px solid #2d3136", background: "#16181c", color: "#e6e7ea",
};

export function ConnectScreen({ status, onSubmitPin }: { status: ConnectionStatus; onSubmitPin: (pin: string) => void }) {
  const [pin, setPin] = useState("");
  const pairing = status === "pairing_required";
  const complete = pin.length === PIN_LENGTH;

  const pairButtonStyle: CSSProperties = {
    padding: "10px 24px", fontSize: 15, borderRadius: 8, border: "none",
    background: complete ? "#3b82f6" : "#2d3136", color: "white",
    cursor: complete ? "pointer" : "default",
  };

  return (
    <div style={screenStyle}>
      <h1 style={headingStyle}>Macro Grid</h1>

      {!pairing && <p style={statusTextStyle}>{status === "connecting" ? t("status.connecting") : t("connect.lost")}</p>}

      {pairing && (
        <>
          <p style={hintStyle}>{t("connect.pairHint")}</p>
          <input
            value={pin}
            onChange={(e) => setPin(e.target.value.replace(/\D/g, "").slice(0, PIN_LENGTH))}
            onKeyDown={(e) => e.key === "Enter" && complete && onSubmitPin(pin)}
            placeholder="000000"
            inputMode="numeric"
            autoFocus
            style={pinInputStyle}
          />
          <button onClick={() => onSubmitPin(pin)} disabled={!complete} style={pairButtonStyle}>
            {t("connect.pair")}
          </button>
        </>
      )}
    </div>
  );
}
