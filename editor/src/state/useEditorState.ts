import { useProfileDocument } from "./useProfileDocument";
import { usePageActions } from "./usePageActions";
import { useServerCatalogs } from "./useServerCatalogs";
import { useWidgetActions } from "./useWidgetActions";

/**
 * The editor's whole state and every operation on it, composed from focused hooks:
 * the open profile document, page edits, widget edits and the server-provided catalogs.
 */
export function useEditorState() {
  const document = useProfileDocument();
  const pages = usePageActions(document);
  const widgets = useWidgetActions(document);
  const catalogs = useServerCatalogs();

  return {
    profiles: document.profiles,
    profile: document.profile,
    currentPage: document.currentPage,
    currentPageId: document.currentPageId,
    selectedWidget: document.selectedWidget,
    selectedWidgets: document.selectedWidgets,
    selectedIds: document.selectedIds,
    dirty: document.dirty,
    saving: document.saving,
    error: document.error,
    clearError: () => document.setError(null),
    variables: catalogs.variables,
    actions: catalogs.actions,
    variableCatalog: catalogs.variableCatalog,
    status: catalogs.status,
    refreshVariables: catalogs.refreshVariables,
    setCurrentPageId: document.setCurrentPageId,
    setSelectedIds: document.setSelectedIds,
    toggleSelected: document.toggleSelected,
    selectProfile: document.selectProfile,
    createProfile: document.createProfile,
    importProfileFromJson: document.importProfileFromJson,
    deleteProfile: document.deleteProfile,
    renameProfile: document.renameProfile,
    setPreviewDevice: document.setPreviewDevice,
    setAppMatches: document.setAppMatches,
    save: document.save,
    ...pages,
    ...widgets,
  };
}
