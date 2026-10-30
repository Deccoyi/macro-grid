import { useEffect, useRef } from "react";
import type { Profile } from "@macro/renderer";
import { usePluginWidgets } from "../state/usePluginWidgets";
import { useDiagnostics } from "./DiagnosticsContext";
import { SAVE_SUMMARY_SOURCE, saveSummary } from "./saveSummary";

/**
 * After every save, replaces the earlier save messages in the Diagnostic Messages with new ones: how many plugin widgets the profile has and whether
 * a page has more than is recommended. It shows nothing until the first save.
 */
export function SaveSummaryReporter({ profile, saveCount }: { profile: Profile | null; saveCount: number }) {
  const { report } = useDiagnostics();
  const available = usePluginWidgets();
  const reported = useRef(0);

  useEffect(() => {
    if (saveCount === 0 || saveCount === reported.current || !profile) return;
    reported.current = saveCount;
    report(SAVE_SUMMARY_SOURCE, saveSummary(profile, available));
    // The summary is made once per save, from the profile as it was saved.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [saveCount]);

  return null;
}
