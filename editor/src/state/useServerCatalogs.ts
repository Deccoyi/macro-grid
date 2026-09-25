import { useCallback, useEffect, useState } from "react";
import { api } from "../api/client";
import type { ActionInfo, StatusEntry, VariableInfo, VariableSnapshot } from "../api/types";
import { usePreferences } from "../preferences/PreferencesContext";
import { invalidateIconPacks } from "../panels/IconPicker";

/**
 * Read-only data the server describes: live variable values, status items, the action catalog and the
 * variable catalog. Keeps them fresh (polling, refetch on focus and on a saved language change).
 */
export function useServerCatalogs() {
  const { savedLanguage } = usePreferences();
  const [variables, setVariables] = useState<VariableSnapshot>({});
  const [actions, setActions] = useState<ActionInfo[]>([]);
  const [variableCatalog, setVariableCatalog] = useState<VariableInfo[]>([]);
  const [status, setStatus] = useState<StatusEntry[]>([]);

  const refreshVariables = useCallback(() => {
    api.variablesSnapshot().then(setVariables).catch(() => {});
    api.getStatus().then(setStatus).catch(() => {});
  }, []);

  const refreshCatalogs = useCallback(() => {
    invalidateIconPacks();
    api.listActions().then(setActions).catch(() => {});
    api.variableCatalog().then(setVariableCatalog).catch(() => {});
  }, []);

  // Runs on mount and whenever the saved language changes: plugin texts are translated by the server.
  useEffect(() => {
    refreshCatalogs();
    refreshVariables();
  }, [savedLanguage, refreshCatalogs, refreshVariables]);

  // Plugins are installed / reloaded / removed live from the separate "Plugins" window, which changes the
  // action list, variable picker and icon packs. Coming back to this window is the cheap moment to refetch.
  useEffect(() => {
    window.addEventListener("focus", refreshCatalogs);
    return () => window.removeEventListener("focus", refreshCatalogs);
  }, [refreshCatalogs]);

  // Auto-poll so the canvas's dynamic-style preview (evaluateWidgetDynamicStyle) actually reacts to
  // live values like system.cpu instead of only updating when the user clicks "Refresh variables" —
  // this is a one-shot REST snapshot, not a push, since the editor isn't a real device session.
  useEffect(() => {
    const timer = setInterval(refreshVariables, 2000);
    return () => clearInterval(timer);
  }, [refreshVariables]);

  return { variables, actions, variableCatalog, status, refreshVariables };
}
