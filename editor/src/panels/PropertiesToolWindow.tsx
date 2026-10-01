import { useEditorStateContext } from "../state/EditorStateContext";
import { usePluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { useProfileTreeContext } from "../state/ProfileTreeContext";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import { Inspector } from "./Inspector";
import { PluginTreeItemProperties } from "./PluginTreeItemProperties";
import { ProfileProperties } from "./ProfileProperties";

/** Registry adapter for the Properties tool window: same props App.tsx used to pass Inspector directly,
 * plus a profile-properties view swapped in while a Hierarchy profile row is the current selection, and a
 * plugin-settings view swapped in while a Plugins tool window row is selected (see pluginTreeSelectionStore.ts
 * and docs/design/plugins-tool-window.md — the two tool windows share this through a module-level store
 * rather than a context, since neither is an ancestor of the other). */
export function PropertiesToolWindow() {
  const state = useEditorStateContext();
  const profileTree = useProfileTreeContext();
  const { openMoveCopyForWidgets, profilePropertiesTarget } = useWorkspaceUi();
  const pluginTreeSelection = usePluginTreeSelection();
  const { profile, currentPage } = state;
  if (!profile || !currentPage) return null;

  if (pluginTreeSelection) {
    return <PluginTreeItemProperties selection={pluginTreeSelection} />;
  }

  if (profilePropertiesTarget) {
    const target = state.profiles.find((p) => p.id === profilePropertiesTarget.profileId);
    if (target) {
      const isCurrent = target.id === profile.id;
      return (
        <ProfileProperties
          target={target}
          isCurrent={isCurrent}
          appMatches={isCurrent ? (profile.appMatches ?? []) : []}
          onAppMatchesChange={state.setAppMatches}
          onRename={(name) => state.renameProfile(name)}
          onDelete={async () => { await state.deleteProfile(profile.id); profileTree.removeProfileNode(profile.id); }}
          canDelete={state.profiles.length > 1}
        />
      );
    }
  }

  return (
    <Inspector
      selectedWidgets={state.selectedWidgets}
      page={currentPage}
      pages={profile.pages}
      profiles={state.profiles}
      actions={state.actions}
      variableCatalog={state.variableCatalog}
      onChange={(fn) => state.selectedWidget && state.updateWidget(state.selectedWidget.id, fn)}
      onRename={(name) => state.selectedWidget && state.updateWidget(state.selectedWidget.id, (w) => { w.name = name; }, { label: "undo.renameWidget" })}
      onDelete={() => state.selectedWidget && state.deleteWidget(state.selectedWidget.id)}
      onDeleteSelected={state.deleteSelectedWidgets}
      onDuplicateSelected={state.duplicateSelectedWidgets}
      onMoveCopySelected={openMoveCopyForWidgets}
      onRenamePage={state.renamePage}
      onSetPageGrid={state.setPageGrid}
      onSetPageGap={state.setPageGap}
      onSetPagePadding={state.setPagePadding}
      onSetPageAlignment={state.setPageAlignment}
      onDuplicatePage={state.duplicatePage}
      onDeletePage={state.deletePage}
      canDeletePage={profile.pages.length > 1}
    />
  );
}
