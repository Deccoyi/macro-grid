import { useEffect, useMemo } from "react";
import type { Profile } from "@macro/renderer";
import { usePluginWidgets } from "../state/usePluginWidgets";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import type { DictKey } from "../i18n/tr";
import { useT } from "../i18n/I18nContext";
import { useDiagnostics } from "./DiagnosticsContext";
import { checkProfile, PROFILE_CHECK_SOURCE } from "./profileCheck";

interface Props {
  profile: Profile | null;
  profiles: ProfileSummary[];
  actions: ActionInfo[];
  variableCatalog: VariableInfo[];
  /** The names of the variables that have a value right now, joined into one sorted text so the 2-second value poll does not re-run the check. */
  liveNames: string;
}

/** Runs the profile check 300 ms after the profile, the action list or the plugin widgets change, and replaces the earlier lines of that check. */
export function ProfileCheckReporter({ profile, profiles, actions, variableCatalog, liveNames }: Props) {
  const pluginWidgets = usePluginWidgets();
  const { report } = useDiagnostics();
  const { t } = useT();
  const live = useMemo(() => new Set(liveNames ? liveNames.split("\n") : []), [liveNames]);
  const catalogNames = useMemo(() => new Set(variableCatalog.map((v) => v.name)), [variableCatalog]);

  useEffect(() => {
    // An empty action list means "not loaded yet", and would flag every action.
    if (!profile || actions.length === 0) return;
    const timer = window.setTimeout(() => {
      const profileIds = new Set(profiles.map((p) => p.id));
      profileIds.add(profile.id);
      report(PROFILE_CHECK_SOURCE, checkProfile(profile, { actions, variableNames: catalogNames, liveVariableNames: live, pluginWidgets, profileIds }, (e) => t(`action.event.${e}` as DictKey)));
    }, 300);
    return () => window.clearTimeout(timer);
    // `report` changes identity without changing what the check finds.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile, profiles, actions, catalogNames, live, pluginWidgets, t]);

  return null;
}
