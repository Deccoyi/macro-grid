import { createContext, useContext, type ReactNode } from "react";

/** Overlay triggers tool windows need but don't own the rendering of: the page context menu and the
 * Move/Copy dialog are still rendered once at the App root (like the widget context menu, which stays
 * local to DocumentArea since only the canvas opens it). Kept separate from EditorStateContext because
 * it's UI plumbing, not editor data. */
export interface WorkspaceUi {
  openPageContextMenu: (x: number, y: number, pageId: string) => void;
  openMoveCopyForWidgets: () => void;
  openMoveCopyForPage: (pageId: string) => void;
  /** Which profile's properties Properties should show instead of the widget/page inspector — set by
   * clicking a profile row in Hierarchy, cleared by selecting a page or a widget. */
  profilePropertiesTarget: { profileId: string } | null;
  showProfileProperties: (profileId: string) => void;
  clearProfileProperties: () => void;
  /** Which Hierarchy row (a page or a profile) is showing an inline rename input in place of its label —
   * set by the page context menu's "Rename" (right-click), or by F2/double-click inside HierarchyToolWindow
   * itself. A single shared trigger because the context menu lives at the App root, outside the row that
   * actually needs to switch into edit mode. */
  renameTarget: { kind: "page" | "profile"; id: string } | null;
  startRename: (kind: "page" | "profile", id: string) => void;
  clearRename: () => void;
  /** The Hierarchy row last clicked, for Ctrl+C/Ctrl+V — separate from `profilePropertiesTarget` (which
   * profile's settings show in Properties) and `renameTarget`: this is purely "what would Ctrl+C copy, and
   * where would Ctrl+V paste". A folder selection is a paste target itself (paste goes inside it); a leaf
   * selection's `parentFolderId` is the paste target (paste lands as its sibling). */
  treeSelection:
    | { kind: "page" | "pageFolder"; id: string; profileId: string; parentFolderId: string | null }
    | { kind: "profile" | "profileFolder"; id: string; parentFolderId: string | null }
    | null;
  setTreeSelection: (sel: WorkspaceUi["treeSelection"]) => void;
  /** "Go to widget" from a diagnostic line: the action ActionEditor should open, scroll to and outline once, then clear. */
  focusAction: FocusAction | null;
  setFocusAction: (focus: FocusAction | null) => void;
}

export interface FocusAction {
  widgetId: string;
  event: string;
  /** 0-based position of the action in the event's list. */
  index: number;
}

const WorkspaceUiContext = createContext<WorkspaceUi | null>(null);

export function WorkspaceUiProvider({ value, children }: { value: WorkspaceUi; children: ReactNode }) {
  return <WorkspaceUiContext.Provider value={value}>{children}</WorkspaceUiContext.Provider>;
}

export function useWorkspaceUi(): WorkspaceUi {
  const value = useContext(WorkspaceUiContext);
  if (!value) throw new Error("useWorkspaceUi() used outside <WorkspaceUiProvider>");
  return value;
}
