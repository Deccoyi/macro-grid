import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import type { UpdateCheckOutcome, UpdateSnapshot } from "../api/types";

/** The server's update state (GET /api/update) and the calls that change it. Refetches when the window is focused again,
 * since a check may have run in the background while it was behind another window. */
export function useUpdate() {
  const [snapshot, setSnapshot] = useState<UpdateSnapshot | null>(null);
  const [checking, setChecking] = useState(false);
  const [outcome, setOutcome] = useState<UpdateCheckOutcome | null>(null);

  const refresh = useCallback(() => {
    api.getUpdate().then(setSnapshot).catch(() => {});
  }, []);

  useEffect(() => {
    refresh();
    window.addEventListener("focus", refresh);
    return () => window.removeEventListener("focus", refresh);
  }, [refresh]);

  const check = useCallback(async () => {
    setChecking(true);
    setOutcome(null);
    try {
      const result = await api.checkForUpdates();
      setSnapshot(result.snapshot);
      setOutcome(result.outcome);
    } catch {
      setOutcome("failed");
    } finally {
      setChecking(false);
    }
  }, []);

  const install = useCallback(async () => {
    await api.installUpdate().catch(() => {});
    refresh();
  }, [refresh]);

  // While the server downloads, and while the setup is open, follow it closely: a cancelled setup leaves the app running and the window has to say so
  // without waiting for the person to come back to it. A setup that goes on to install closes the app, and the polling ends with it.
  const installState = snapshot?.install?.state;
  useEffect(() => {
    if (installState !== "downloading" && installState !== "starting") return;
    const timer = setInterval(refresh, 500);
    return () => clearInterval(timer);
  }, [installState, refresh]);

  const snooze = useCallback(() => api.snoozeUpdate().then(refresh), [refresh]);
  const skip = useCallback(() => api.skipUpdate().then(refresh), [refresh]);

  return { snapshot, checking, outcome, check, install, snooze, skip };
}
