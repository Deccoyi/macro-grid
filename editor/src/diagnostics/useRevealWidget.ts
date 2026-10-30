import { useCallback } from "react";
import { usePreferences } from "../preferences/PreferencesContext";
import { clearPluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { useEditorStateContext } from "../state/EditorStateContext";
import { useWorkspace } from "../workspace/WorkspaceContext";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import type { DiagnosticTarget } from "./types";

/** "Go to widget": opens the profile and page the line is about (the profile switch asks about unsaved changes like any other), selects the
 * widget, brings Properties forward and, when the line names an event, opens that event's actions and outlines the action. */
export function useRevealWidget() {
  const state = useEditorStateContext();
  const workspace = useWorkspace();
  const { clearProfileProperties, setFocusAction } = useWorkspaceUi();
  const { setInspectorSectionCollapsed } = usePreferences();

  return useCallback(async (target: DiagnosticTarget) => {
    if (!target.widgetId || !target.pageId) return;
    if (state.profile?.id !== target.profileId) await state.selectProfile(target.profileId);
    state.setCurrentPageId(target.pageId);
    state.setSelectedIds([target.widgetId]);
    clearProfileProperties();
    clearPluginTreeSelection();
    workspace.bringForward("properties");
    if (target.event && target.actionIndex !== undefined) {
      setInspectorSectionCollapsed("actions", false);
      setFocusAction({ widgetId: target.widgetId, event: target.event, index: target.actionIndex });
    }
  }, [state, workspace, clearProfileProperties, setFocusAction, setInspectorSectionCollapsed]);
}
