import { useEffect, useRef, useState } from "react";
import { Grid, WidgetView, type Profile, type WidgetState } from "@macro/renderer";
import { getDeviceId } from "./deviceId";
import { ActionErrorToast } from "./components/ActionErrorToast";
import { ConnectScreen } from "./components/ConnectScreen";
import { TopBar } from "./components/TopBar";
import { t } from "./i18n";
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
    const connection = new ServerConnection(getDeviceId(), t("device.name"), {
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
