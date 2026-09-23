import { useEffect, useRef, useState, type CSSProperties } from "react";
import { Grid, WidgetView, type Profile, type WidgetState } from "@macro/renderer";
import { getDeviceId } from "./deviceId";
import { ConnectionStatus, ProfileSummary, ServerConnection } from "./ws/connection";

export function App() {
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [profile, setProfile] = useState<Profile | null>(null);
  const [pageId, setPageId] = useState<string | null>(null);
  const [states, setStates] = useState<Record<string, WidgetState>>({});
  const [dragValues, setDragValues] = useState<Record<string, number>>({});
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [actionError, setActionError] = useState<string | null>(null);
  const connectionRef = useRef<ServerConnection | null>(null);
  const actionErrorTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const connection = new ServerConnection(getDeviceId(), "Tarayıcı", {
      onStatusChange: setStatus,
      onLayout: (nextProfile, nextPageId) => {
        setProfile(nextProfile);
        setPageId(nextPageId);
        setStates({});
        setDragValues({});
      },
      onPageChange: setPageId,
      onWidgetState: (state) => {
        setStates((prev) => ({ ...prev, [state.widgetId]: { ...prev[state.widgetId], ...state } }));
        if (state.value !== undefined) {
          setDragValues((prev) => {
            if (!(state.widgetId in prev)) return prev;
            const next = { ...prev };
            delete next[state.widgetId];
            return next;
          });
        }
      },
      onProfiles: setProfiles,
      onPaired: () => {},
      onActionError: (message) => {
        if (actionErrorTimer.current) clearTimeout(actionErrorTimer.current);
        setActionError(message);
        actionErrorTimer.current = setTimeout(() => setActionError(null), 4000);
      },
    });
    connectionRef.current = connection;
    connection.connect();
    return () => connection.disconnect();
  }, []);

  const page = profile?.pages.find((p) => p.id === pageId) ?? profile?.pages[0];

  if (!profile || !page || status === "pairing_required") {
    return <ConnectScreen status={status} onSubmitPin={(pin) => connectionRef.current?.retryWithPin(pin)} />;
  }

  return (
    <div style={{ minHeight: "100vh", background: "#0b0d10", display: "flex", flexDirection: "column" }}>
      <TopBar
        status={status}
        profiles={profiles}
        currentProfileId={profile.id}
        onPickProfile={(id) => connectionRef.current?.changeProfile(id)}
        pageName={page.name}
        canNav={profile.pages.length > 1}
        onPrevPage={() => connectionRef.current?.prevPage()}
        onNextPage={() => connectionRef.current?.nextPage()}
      />
      <div style={{ flex: 1, position: "relative" }}>
        <Grid
          page={page}
          renderWidget={(widget) => {
            const state = states[widget.id];
            return (
              <WidgetView
                widget={widget}
                liveText={state?.text}
                liveActive={state?.active}
                liveValue={dragValues[widget.id] ?? state?.value}
                liveStyle={state?.style}
                onPress={() => connectionRef.current?.send("widget.down", { pageId: page.id, widgetId: widget.id })}
                onRelease={() => connectionRef.current?.send("widget.up", { pageId: page.id, widgetId: widget.id })}
                onLongPress={() => connectionRef.current?.send("widget.longPress", { pageId: page.id, widgetId: widget.id })}
                onDoubleTap={() => connectionRef.current?.send("widget.doubleTap", { pageId: page.id, widgetId: widget.id })}
                onValueChange={(value) => setDragValues((prev) => ({ ...prev, [widget.id]: value }))}
                onValueCommit={(value) => {
                  setDragValues((prev) => ({ ...prev, [widget.id]: value }));
                  connectionRef.current?.send("widget.value", { pageId: page.id, widgetId: widget.id, value });
                }}
              />
            );
          }}
        />
      </div>
      {actionError && <ActionErrorToast message={actionError} />}
    </div>
  );
}

function ActionErrorToast({ message }: { message: string }) {
  return (
    <div
      style={{
        position: "fixed", left: "50%", bottom: 24, transform: "translateX(-50%)",
        maxWidth: "min(420px, calc(100vw - 32px))", padding: "10px 16px", borderRadius: 10,
        background: "rgba(127,29,29,.95)", color: "#fecaca", fontSize: 13, lineHeight: 1.4,
        boxShadow: "0 4px 16px rgba(0,0,0,.4)", zIndex: 1000,
      }}
    >
      {message}
    </div>
  );
}

function TopBar({
  status,
  profiles,
  currentProfileId,
  onPickProfile,
  pageName,
  canNav,
  onPrevPage,
  onNextPage,
}: {
  status: ConnectionStatus;
  profiles: ProfileSummary[];
  currentProfileId: string;
  onPickProfile: (id: string) => void;
  pageName: string;
  canNav: boolean;
  onPrevPage: () => void;
  onNextPage: () => void;
}) {
  return (
    <div
      style={{
        display: "flex", alignItems: "center", gap: 10, padding: "8px 12px",
        background: "#16181c", borderBottom: "1px solid #2d3136", flexWrap: "wrap",
      }}
    >
      <span style={{ color: "#e6e7ea", fontSize: 13, fontWeight: 600 }}>Macro Station</span>

      {profiles.length > 1 && (
        <select
          value={currentProfileId}
          onChange={(e) => onPickProfile(e.target.value)}
          style={{ background: "#0b0d10", color: "#e6e7ea", border: "1px solid #2d3136", borderRadius: 6, padding: "4px 8px", fontSize: 12.5 }}
        >
          {profiles.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
      )}

      {canNav && (
        <div style={{ display: "flex", alignItems: "center", gap: 6, marginLeft: "auto" }}>
          <button onClick={onPrevPage} style={navButtonStyle}>←</button>
          <span style={{ color: "#9aa0a8", fontSize: 12, minWidth: 60, textAlign: "center" }}>{pageName}</span>
          <button onClick={onNextPage} style={navButtonStyle}>→</button>
        </div>
      )}

      <span
        style={{
          marginLeft: canNav ? 0 : "auto", fontSize: 11, padding: "3px 8px", borderRadius: 999,
          background: status === "connected" ? "rgba(34,197,94,.15)" : "rgba(239,68,68,.15)",
          color: status === "connected" ? "#4ade80" : status === "connecting" ? "#facc15" : "#ef4444",
        }}
      >
        {status === "connected" ? "Bağlı" : status === "connecting" ? "Bağlanıyor…" : "Çevrimdışı"}
      </span>
    </div>
  );
}

const navButtonStyle: CSSProperties = {
  padding: "4px 10px", fontSize: 14, borderRadius: 6, border: "1px solid #2d3136",
  background: "transparent", color: "#e6e7ea", cursor: "pointer",
};

function ConnectScreen({ status, onSubmitPin }: { status: ConnectionStatus; onSubmitPin: (pin: string) => void }) {
  const [pin, setPin] = useState("");
  const pairing = status === "pairing_required";

  return (
    <div
      style={{
        display: "flex", flexDirection: "column", gap: 14, alignItems: "center", justifyContent: "center",
        minHeight: "100vh", background: "#0b0d10", color: "#e6e7ea", fontFamily: "system-ui, sans-serif", padding: 24,
        boxSizing: "border-box",
      }}
    >
      <h1 style={{ fontSize: 20, margin: 0 }}>Macro Station</h1>

      {!pairing && <p style={{ color: "#9aa0a8", fontSize: 13 }}>{status === "connecting" ? "Bağlanıyor…" : "Bağlantı koptu, yeniden deneniyor…"}</p>}

      {pairing && (
        <>
          <p style={{ color: "#9aa0a8", fontSize: 13, textAlign: "center", margin: 0, maxWidth: 320 }}>
            Bu tarayıcı henüz eşleşmemiş. Bilgisayarındaki Macro Station düzenleyicisinde "Eşleştirme"ye tıkla ve orada
            gösterilen 6 haneli PIN'i buraya gir.
          </p>
          <input
            value={pin}
            onChange={(e) => setPin(e.target.value.replace(/\D/g, "").slice(0, 6))}
            onKeyDown={(e) => e.key === "Enter" && pin.length === 6 && onSubmitPin(pin)}
            placeholder="000000"
            inputMode="numeric"
            autoFocus
            style={{
              width: "100%", maxWidth: 200, padding: "10px 12px", fontSize: 28, borderRadius: 8, textAlign: "center",
              letterSpacing: ".2em", fontFamily: "ui-monospace, monospace",
              border: "1px solid #2d3136", background: "#16181c", color: "#e6e7ea",
            }}
          />
          <button
            onClick={() => onSubmitPin(pin)}
            disabled={pin.length !== 6}
            style={{
              padding: "10px 24px", fontSize: 15, borderRadius: 8, border: "none",
              background: pin.length === 6 ? "#3b82f6" : "#2d3136", color: "white",
              cursor: pin.length === 6 ? "pointer" : "default",
            }}
          >
            Eşleştir
          </button>
        </>
      )}
    </div>
  );
}
