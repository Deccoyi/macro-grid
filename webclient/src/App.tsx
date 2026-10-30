import { useEffect, useMemo, useRef, useState } from "react";
import { Grid, PluginWidgetContext, PluginWidgetRuntime, WidgetView, type Profile, type WidgetState } from "@macro/renderer";
import { getDeviceId } from "./deviceId";
import { ActionErrorToast } from "./components/ActionErrorToast";
import { ConnectScreen } from "./components/ConnectScreen";
import { TopBar } from "./components/TopBar";
import { t } from "./i18n";
import { DeckPluginWidgetHost } from "./pluginWidgetHost";
import { ConnectionStatus, PairingError, ProfileSummary, ServerConnection } from "./ws/connection";

function pluginWidgetTexts() {
  return {
    widget: t("widget.plugin"),
    restart: t("widget.restart"),
    unavailable: {
      missing: t("widget.unavailable.missing"),
      disabled: t("widget.unavailable.disabled"),
      needsApproval: t("widget.unavailable.needsApproval"),
      incompatible: t("widget.unavailable.incompatible"),
      invalid: t("widget.unavailable.invalid"),
      noWidget: t("widget.unavailable.noWidget"),
      unsupported: t("widget.unavailable.unsupported"),
      off: t("widget.unavailable.off"),
      crashedOff: t("widget.unavailable.crashedOff"),
    },
    stopped: {
      frozen: t("widget.stopped.frozen"),
      startTimeout: t("widget.stopped.startTimeout"),
      tooBusy: t("widget.stopped.tooBusy"),
      tooMany: t("widget.stopped.tooMany"),
      crashed: t("widget.stopped.crashed"),
      failed: t("widget.stopped.failed"),
    },
  };
}

export function App() {
  const [status, setStatus] = useState<ConnectionStatus>("connecting");
  const [profile, setProfile] = useState<Profile | null>(null);
  const [pageId, setPageId] = useState<string | null>(null);
  const [states, setStates] = useState<Record<string, WidgetState>>({});
  const [dragValues, setDragValues] = useState<Record<string, number>>({});
  const [profiles, setProfiles] = useState<ProfileSummary[]>([]);
  const [actionError, setActionError] = useState<string | null>(null);
  const [pairingError, setPairingError] = useState<PairingError | null>(null);
  const connectionRef = useRef<ServerConnection | null>(null);
  const pageIdRef = useRef<string | null>(null);
  pageIdRef.current = pageId;
  // One runtime and one host for the life of the page: the runtime owns the sandboxed launcher frame and the workers.
  const widgets = useMemo(() => {
    const runtime = new PluginWidgetRuntime();
    const host = new DeckPluginWidgetHost(
      { send: (type, data) => connectionRef.current?.send(type, data) },
      () => pageIdRef.current ?? undefined,
    );
    return { runtime, host };
  }, []);
  useEffect(() => () => widgets.runtime.dispose(), [widgets]);
  const actionErrorTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  // One short message at the bottom (a failed action, or the sources an unavailable widget is missing); it clears itself.
  const showNotice = (message: string) => {
    if (actionErrorTimer.current) clearTimeout(actionErrorTimer.current);
    setActionError(message);
    actionErrorTimer.current = setTimeout(() => setActionError(null), 4000);
  };

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
      onActionError: (message) => showNotice(message),
      onPairingError: setPairingError,
      onPluginWidgetMessage: (type, data) => widgets.host.onMessage(type, data),
      onAsset: (hash, data) => widgets.host.onAsset(hash, data),
      onWelcome: () => widgets.host.onReconnected(),
    });
    connectionRef.current = connection;
    connection.connect();
    return () => connection.disconnect();
  }, [widgets]);

  const page = profile?.pages.find((p) => p.id === pageId) ?? profile?.pages[0];

  if (!profile || !page || status === "pairing_required") {
    return (
      <ConnectScreen
        status={status}
        error={pairingError}
        onSubmitPin={(pin) => {
          setPairingError(null);
          connectionRef.current?.retryWithPin(pin);
        }}
      />
    );
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
      <PluginWidgetContext.Provider value={{ runtime: widgets.runtime, host: widgets.host, texts: pluginWidgetTexts() }}>
      <div style={{ flex: 1, position: "relative" }}>
        <Grid
          style={{ position: "absolute", inset: 0, width: "auto", height: "auto" }}
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
                unavailable={state?.unavailable}
                unavailableLabel={t("widget.sourceUnavailable.hint")}
                onUnavailableTap={(names) => showNotice(t("widget.sourceUnavailable") + names.join(", "))}
                webUrl={state?.url}
                webReload={state?.reload}
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
      </PluginWidgetContext.Provider>
      {actionError && <ActionErrorToast message={actionError} />}
    </div>
  );
}
