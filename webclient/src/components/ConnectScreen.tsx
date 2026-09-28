import { useEffect, useState, type CSSProperties } from "react";
import { t } from "../i18n";
import type { MessageKey } from "../i18n/en";
import type { ConnectionStatus, PairingError } from "../ws/connection";

/** Maps a server-sent pairing_required `reason` code to this client's own localized text, so the message
 * follows the phone/browser's language instead of always being the server's English prose. A `reason` this
 * client doesn't recognize (an older build against a newer server) is not in this map — falls back to
 * `error.message` in that case, same as before this existed. `locked_out` isn't here: it always goes through
 * the live countdown text instead (see `secondsLeft` below), never this map. */
const REASON_KEYS: Partial<Record<string, MessageKey>> = {
  wrong_pin: "connect.reason.wrong_pin",
  pairing_closed: "connect.reason.pairing_closed",
  not_paired: "connect.reason.not_paired",
};

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

const errorTextStyle: CSSProperties = { color: "#f87171", fontSize: 13, textAlign: "center", margin: 0, maxWidth: 320 };

export function ConnectScreen({
  status,
  error,
  onSubmitPin,
}: {
  status: ConnectionStatus;
  error?: PairingError | null;
  onSubmitPin: (pin: string) => void;
}) {
  const [pin, setPin] = useState("");
  const pairing = status === "pairing_required";
  const complete = pin.length === PIN_LENGTH;

  // A fresh `error` object (a new pairing attempt, blocked again after already counting down once, ...)
  // restarts the countdown; a `retryAfterSeconds` of null (wrong PIN, pairing closed) never starts one.
  const [secondsLeft, setSecondsLeft] = useState<number | null>(null);
  useEffect(() => {
    if (error?.retryAfterSeconds == null) {
      setSecondsLeft(null);
      return;
    }
    setSecondsLeft(error.retryAfterSeconds);
    const interval = setInterval(() => setSecondsLeft((prev) => (prev === null || prev <= 1 ? 0 : prev - 1)), 1000);
    return () => clearInterval(interval);
  }, [error]);

  const reasonKey = error?.reason ? REASON_KEYS[error.reason] : undefined;
  const errorText =
    secondsLeft !== null
      ? t("connect.pairBlockedSeconds").replace("{seconds}", String(secondsLeft))
      : reasonKey
        ? t(reasonKey)
        : error?.message;

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
          {errorText && <p style={errorTextStyle}>{errorText}</p>}
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
