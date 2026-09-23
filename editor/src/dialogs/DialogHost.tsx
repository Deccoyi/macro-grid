import { useEffect, useState } from "react";
import { useT } from "../i18n/I18nContext";
import { type DialogRequest, subscribe } from "./dialogStore";

/** Renders the single active confirm/prompt request as an app-styled modal (same chrome as
 * PairingPanel) instead of the browser's native confirm()/prompt(). Mount once near the app root. */
export function DialogHost() {
  const { t } = useT();
  const [request, setRequest] = useState<DialogRequest | null>(null);
  const [value, setValue] = useState("");

  useEffect(() => subscribe(setRequest), []);

  useEffect(() => {
    if (request?.kind === "prompt") setValue(request.defaultValue);
  }, [request]);

  if (!request) return null;

  const cancel = () => {
    if (request.kind === "confirm") request.resolve(false);
    else if (request.kind === "prompt") request.resolve(null);
    else request.resolve();
    setRequest(null);
  };
  const accept = () => {
    if (request.kind === "confirm") request.resolve(true);
    else if (request.kind === "prompt") request.resolve(value);
    else request.resolve();
    setRequest(null);
  };

  return (
    <div style={{ position: "fixed", inset: 0, background: "rgba(0,0,0,.5)", zIndex: 80, display: "flex", alignItems: "center", justifyContent: "center" }} onClick={cancel}>
      <div
        onClick={(e) => e.stopPropagation()}
        style={{ width: 320, background: "var(--ms-bg-surface)", border: "1px solid var(--ms-border-strong)", borderRadius: 6 }}
      >
        <div style={{ padding: "16px 18px 4px", fontSize: 14, fontWeight: 600 }}>
          {request.title ??
            (request.kind === "confirm" ? t("dialog.confirmTitle") : request.kind === "prompt" ? t("dialog.promptTitle") : t("dialog.alertTitle"))}
        </div>
        <div style={{ padding: "8px 18px 18px", display: "flex", flexDirection: "column", gap: 10 }}>
          <p style={{ margin: 0, fontSize: 12.5, color: "var(--ms-text-secondary)" }}>{request.message}</p>
          {request.kind === "prompt" && (
            <input
              type="text"
              autoFocus
              value={value}
              onChange={(e) => setValue(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") accept();
                if (e.key === "Escape") cancel();
              }}
            />
          )}
        </div>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, padding: "12px 18px", borderTop: "1px solid var(--ms-border)" }}>
          {request.kind !== "alert" && <button className="ghost" onClick={cancel}>{t("dialog.cancel")}</button>}
          <button className={request.kind === "confirm" && request.danger ? "primary danger" : "primary"} onClick={accept}>
            {request.kind === "confirm" ? t("dialog.confirm") : t("dialog.ok")}
          </button>
        </div>
      </div>
    </div>
  );
}
