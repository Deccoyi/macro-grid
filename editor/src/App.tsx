import { useEffect, useState } from "react";
import type { Page, Profile, Widget } from "@macro/renderer";
import { Copy, Smartphone, Trash2 } from "lucide-react";
import { api } from "./api/client";
import type { ProfileTreeNode } from "./api/types";
import { confirmAsync } from "./dialogs/dialogStore";
import { DialogHost } from "./dialogs/DialogHost";
import { DiagnosticsProvider } from "./diagnostics/DiagnosticsContext";
import type { DeviceSize } from "./grid/DevicePreviewFrame";
import { useT } from "./i18n/I18nContext";
import { useDocumentTitle } from "./i18n/useDocumentTitle";
import type { DictKey } from "./i18n/tr";
import { StatusBar } from "./components/StatusBar";
import { ContextMenu, type ContextMenuItem } from "./panels/ContextMenu";
import { MenuBar } from "./panels/MenuBar";
import { MoveCopyDialog } from "./panels/MoveCopyDialog";
import { usePreferences } from "./preferences/PreferencesContext";
import { getClipboard, setClipboard } from "./state/clipboard";
import { EditorStateProvider } from "./state/EditorStateContext";
import { ProfileTreeProvider, useProfileTreeContext } from "./state/ProfileTreeContext";
import { tempId } from "./state/tempId";
import { findNode, flattenLeafIds } from "./state/tree";
import { useEditorState } from "./state/useEditorState";
import { DockWorkspace } from "./workspace/DockWorkspace";
import { WorkspaceProvider } from "./workspace/WorkspaceContext";
import { WorkspaceUiProvider, type WorkspaceUi } from "./workspace/WorkspaceUiContext";

/** Common phone/tablet CSS-px viewport sizes (device-independent px, same units widget fontSize uses)
 * for the "Preview" picker. User-defined sizes from Preferences are appended after these. "Custom"
 * reveals free-form width/height inputs. */
const BUILTIN_DEVICE_PRESETS: { id: string; key: DictKey; size: DeviceSize | null }[] = [
  { id: "free", key: "device.free", size: null },
  { id: "phone-portrait", key: "device.phonePortrait", size: { width: 390, height: 844 } },
  { id: "phone-landscape", key: "device.phoneLandscape", size: { width: 844, height: 390 } },
  { id: "tablet-portrait", key: "device.tabletPortrait", size: { width: 834, height: 1194 } },
  { id: "tablet-landscape", key: "device.tabletLandscape", size: { width: 1194, height: 834 } },
];

/** Wraps AppContent in the profile-tree data it (and HierarchyToolWindow) both need — a tool window's
 * Content component takes no props, so a shared context is the only way to give both the same
 * `useProfileTree()` instance rather than two independently-fetched copies going out of sync. */
export function App() {
  return (
    <ProfileTreeProvider>
      <AppContent />
    </ProfileTreeProvider>
  );
}

function AppContent() {
  const { t } = useT();
  useDocumentTitle("app.title");
  const { previewProfiles } = usePreferences();
  const state = useEditorState();
  const { profile, currentPage } = state;
  const [devicePresetId, setDevicePresetId] = useState("free");
  const [customSize, setCustomSize] = useState<DeviceSize>({ width: 390, height: 844 });
  const devicePresets = [
    ...BUILTIN_DEVICE_PRESETS.map((p) => ({ id: p.id, label: t(p.key), size: p.size })),
    ...previewProfiles.map((p) => ({ id: p.id, label: p.name, size: { width: p.width, height: p.height } })),
    { id: "custom", label: t("device.custom"), size: null },
  ];
  const deviceSize = devicePresetId === "custom" ? customSize : (devicePresets.find((p) => p.id === devicePresetId)?.size ?? null);
  const [pageMenu, setPageMenu] = useState<{ x: number; y: number; pageId: string } | null>(null);
  const [moveCopyOpen, setMoveCopyOpen] = useState(false);
  const [copyPageTarget, setCopyPageTarget] = useState<string | null>(null);
  const [profilePropertiesTarget, setProfilePropertiesTarget] = useState<{ profileId: string } | null>(null);
  const [renameTarget, setRenameTarget] = useState<{ kind: "page" | "profile"; id: string } | null>(null);
  const [treeSelection, setTreeSelection] = useState<WorkspaceUi["treeSelection"]>(null);
  const profileTree = useProfileTreeContext();

  // Each profile remembers its own "Preview" preset (a tablet profile reopens in tablet preview, a
  // phone profile in phone preview, ...) — switch to it whenever a *different* profile is loaded, but
  // never on every in-place edit (profile is a fresh object on every mutate(), so this keys off the id).
  useEffect(() => {
    if (!profile) return;
    const wanted = profile.previewDeviceId;
    setDevicePresetId(wanted && devicePresets.some((p) => p.id === wanted) ? wanted : "free");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile?.id]);

  // If the currently-selected preview profile is deleted in Tercihler, fall back to "free" on its own
  // instead of pointing at a size that no longer exists.
  useEffect(() => {
    if (devicePresetId === "free" || devicePresetId === "custom") return;
    if (!devicePresets.some((p) => p.id === devicePresetId)) setDevicePresetId("free");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [devicePresets, devicePresetId]);

  // Where a paste lands, in whichever tree the clipboard entry's family owns — the Hierarchy selection's
  // own folder (a folder selection) or its parent (a leaf selection), or the root when nothing in that
  // family is selected. A page-family selection is meaningless for a profile-family paste and vice versa,
  // so each only ever looks at its own family's selection.
  const pageParentFolderId = (): string | null => {
    if (!treeSelection || (treeSelection.kind !== "page" && treeSelection.kind !== "pageFolder")) return null;
    return treeSelection.kind === "pageFolder" ? treeSelection.id : treeSelection.parentFolderId;
  };
  const profileParentFolderId = (): string | null => {
    if (!treeSelection || (treeSelection.kind !== "profile" && treeSelection.kind !== "profileFolder")) return null;
    return treeSelection.kind === "profileFolder" ? treeSelection.id : treeSelection.parentFolderId;
  };

  const copyProfileFolder = async (folderId: string) => {
    const folderNode = findNode(profileTree.tree, folderId);
    if (!folderNode) return;
    const ids = flattenLeafIds(folderNode.children ?? [], "profile");
    const fetched = await Promise.all(ids.map((id) => (id === profile?.id ? profile : api.getProfile(id).catch(() => null))));
    const profiles = fetched.filter((p): p is Profile => !!p);
    setClipboard({ kind: "profileFolder", folder: structuredClone(folderNode), profiles: structuredClone(profiles) });
  };

  const copyProfileLeaf = async (id: string) => {
    const source = id === profile?.id ? profile : await api.getProfile(id).catch(() => null);
    if (source) setClipboard({ kind: "profile", profile: structuredClone(source) });
  };

  /** Fresh ids all the way down (a new profile per leaf, via the real API, plus fresh page/widget ids) —
   * the same "paste is always a copy, never a reference" rule pastePage/pastePageFolder follow. Never
   * auto-opens what it creates: unlike pastePage (which does), switching the whole editor to a
   * just-pasted PROFILE while the user is mid-organizing the tree would be more disruptive than helpful,
   * and a folder can create many of them at once. */
  const cloneProfileTreeNode = async (node: ProfileTreeNode, profilesById: Map<string, Profile>): Promise<ProfileTreeNode> => {
    if (node.type === "profile") {
      const source = profilesById.get(node.id);
      if (!source) return { type: "profile", id: tempId("profile") };
      const pages: Page[] = source.pages.map((p) => ({
        ...structuredClone(p),
        id: tempId("page"),
        widgets: structuredClone(p.widgets).map((w: Widget) => ({ ...w, id: tempId("widget") })),
      }));
      const created = await api.createProfile();
      await api.saveProfile({ ...structuredClone(source), id: created.id, pages });
      return { type: "profile", id: created.id };
    }
    const children: ProfileTreeNode[] = [];
    for (const child of node.children ?? []) children.push(await cloneProfileTreeNode(child, profilesById));
    return { type: "folder", id: tempId("pfolder"), name: node.name, children };
  };

  // Ctrl+S / Ctrl+C / Ctrl+V at window level, all skipped while a text field has focus so normal text
  // editing (renaming something, a Properties field, a dialog) keeps its native copy/paste — only Ctrl+S
  // has no competing native meaning to preserve, so it alone isn't gated on that.
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (!(e.ctrlKey || e.metaKey)) return;
      const key = e.key.toLowerCase();

      if (key === "s") {
        e.preventDefault();
        if (state.dirty && !state.saving) state.save();
        return;
      }

      if (key !== "c" && key !== "v") return;
      const target = e.target as HTMLElement | null;
      const isTextField = !!target && (target.tagName === "INPUT" || target.tagName === "TEXTAREA" || target.isContentEditable);
      if (isTextField) return;

      if (key === "c") {
        // Priority: selected widgets on the canvas, then whichever Hierarchy row is the current
        // selection (a page, a page folder, a profile or a profile folder), then the open page as a
        // fallback so Ctrl+C still does something sensible with nothing explicitly selected in the tree.
        if (state.selectedWidgets.length > 0) {
          setClipboard({ kind: "widgets", widgets: structuredClone(state.selectedWidgets) });
        } else if (treeSelection?.kind === "pageFolder" && profile) {
          const folderNode = findNode(profile.pageTree ?? [], treeSelection.id);
          if (folderNode) {
            const pageIds = flattenLeafIds(folderNode.children ?? [], "page");
            const pages = profile.pages.filter((p) => pageIds.includes(p.id));
            setClipboard({ kind: "pageFolder", folder: structuredClone(folderNode), pages: structuredClone(pages) });
          }
        } else if (treeSelection?.kind === "profileFolder") {
          copyProfileFolder(treeSelection.id);
        } else if (treeSelection?.kind === "profile") {
          copyProfileLeaf(treeSelection.id);
        } else if (treeSelection?.kind === "page" && profile) {
          const page = profile.pages.find((p) => p.id === treeSelection.id);
          if (page) setClipboard({ kind: "page", page: structuredClone(page) });
        } else if (currentPage) {
          setClipboard({ kind: "page", page: structuredClone(currentPage) });
        }
        return;
      }

      // key === "v"
      const entry = getClipboard();
      if (!entry) return;
      if (entry.kind === "widgets") {
        state.pasteWidgets(entry.widgets);
      } else if (entry.kind === "page") {
        state.pastePage(entry.page, pageParentFolderId());
      } else if (entry.kind === "pageFolder") {
        state.pastePageFolder(entry.folder, entry.pages, pageParentFolderId());
      } else if (entry.kind === "profile") {
        const parentFolderId = profileParentFolderId();
        api.createProfile().then(async (created) => {
          const pages: Page[] = entry.profile.pages.map((p) => ({
            ...structuredClone(p),
            id: tempId("page"),
            widgets: structuredClone(p.widgets).map((w: Widget) => ({ ...w, id: tempId("widget") })),
          }));
          await api.saveProfile({ ...structuredClone(entry.profile), id: created.id, pages });
          await profileTree.insertPrebuiltNode({ type: "profile", id: created.id }, parentFolderId);
          await state.refreshProfileList();
        });
      } else {
        const parentFolderId = profileParentFolderId();
        const profilesById = new Map(entry.profiles.map((p) => [p.id, p]));
        cloneProfileTreeNode(entry.folder, profilesById).then(async (newNode) => {
          await profileTree.insertPrebuiltNode(newNode, parentFolderId);
          await state.refreshProfileList();
        });
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state, profile, currentPage, treeSelection, profileTree]);

  if (!profile || !currentPage) {
    return <div style={{ padding: 20, color: "var(--ms-text-secondary)" }}>{t("app.loading")}</div>;
  }

  return (
    <WorkspaceProvider>
    <div style={{ display: "grid", gridTemplateRows: "auto auto 1fr auto", height: "100%" }}>
      <MenuBar
        profile={profile}
        onImportProfile={(data: Profile) => state.importProfileFromJson(data)}
      />

      <header style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 12px", borderBottom: "1px solid var(--ms-border)" }}>
        <div style={{ flex: 1 }} />

        <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
          {t("header.preview")}
          <select
            value={devicePresetId}
            onChange={(e) => { setDevicePresetId(e.target.value); state.setPreviewDevice(e.target.value); }}
            style={{ width: "auto" }}
          >
            {devicePresets.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
          </select>
        </label>
        {devicePresetId === "custom" && (
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
            <input
              type="number" min={100} max={4000} value={customSize.width} style={{ width: 56 }}
              onChange={(e) => setCustomSize((s) => ({ ...s, width: Number(e.target.value) }))}
            />
            ×
            <input
              type="number" min={100} max={4000} value={customSize.height} style={{ width: 56 }}
              onChange={(e) => setCustomSize((s) => ({ ...s, height: Number(e.target.value) }))}
            />
          </label>
        )}

        <button className="ghost" onClick={state.refreshVariables}>{t("header.refreshVariables")}</button>
        <button className="ghost" onClick={() => api.openToolWindow("pairing")} style={{ display: "flex", alignItems: "center", gap: 5 }}>
          <Smartphone size={13} /> {t("header.pairing")}
        </button>
        <button className="primary save-btn" onClick={state.save} disabled={!state.dirty || state.saving}>
          {state.saving ? t("header.saving") : state.dirty ? t("header.save") : t("header.saved")}
        </button>
      </header>

      <DialogHost />

      <EditorStateProvider value={state}>
       <DiagnosticsProvider>
        <WorkspaceUiProvider
          value={{
            openPageContextMenu: (x, y, pageId) => setPageMenu({ x, y, pageId }),
            openMoveCopyForWidgets: () => setMoveCopyOpen(true),
            openMoveCopyForPage: (pageId) => setCopyPageTarget(pageId),
            profilePropertiesTarget,
            showProfileProperties: (profileId) => setProfilePropertiesTarget({ profileId }),
            clearProfileProperties: () => setProfilePropertiesTarget(null),
            renameTarget,
            startRename: (kind, id) => setRenameTarget({ kind, id }),
            clearRename: () => setRenameTarget(null),
            treeSelection,
            setTreeSelection,
          }}
        >
          <div style={{ display: "flex", flexDirection: "column", minHeight: 0, minWidth: 0 }}>
            {state.error && (
              <div style={{ padding: "6px 12px", background: "rgba(192,57,43,.15)", color: "var(--ms-danger)", fontSize: 12, display: "flex", justifyContent: "space-between" }}>
                <span>{state.error}</span>
                <button className="ghost" onClick={state.clearError}>{t("header.close")}</button>
              </div>
            )}

            <div style={{ display: "flex", flex: 1, minHeight: 0 }}>
              <DockWorkspace deviceSize={deviceSize} />
            </div>
          </div>

          {pageMenu && (
            <ContextMenu
              x={pageMenu.x}
              y={pageMenu.y}
              onClose={() => setPageMenu(null)}
              items={pageContextItems(t, pageMenu.pageId, profile.pages.length > 1, {
                onRename: (pageId) => setRenameTarget({ kind: "page", id: pageId }),
                onDuplicate: state.duplicatePage,
                onCopyToProfile: (pageId) => setCopyPageTarget(pageId),
                onDelete: async (pageId) => {
                  const page = profile.pages.find((p) => p.id === pageId);
                  if (await confirmDeletePage(t, page?.name ?? "")) state.deletePage(pageId);
                },
              })}
            />
          )}

          {moveCopyOpen && currentPage && (
            <MoveCopyDialog
              title={t("moveCopy.widgetsTitle", String(state.selectedIds.length))}
              profiles={state.profiles}
              currentProfileId={profile.id}
              currentProfilePages={profile.pages}
              onClose={() => setMoveCopyOpen(false)}
              onConfirm={(targetProfileId, targetPageId, mode) => {
                state.moveOrCopyWidgets(state.selectedIds, targetProfileId, targetPageId, mode);
                setMoveCopyOpen(false);
              }}
            />
          )}

          {copyPageTarget && (
            <MoveCopyDialog
              title={t("moveCopy.pageTitle")}
              profiles={state.profiles}
              currentProfileId={profile.id}
              currentProfilePages={profile.pages}
              wholePage
              onClose={() => setCopyPageTarget(null)}
              onConfirm={(targetProfileId) => {
                state.duplicatePageToProfile(copyPageTarget, targetProfileId);
                setCopyPageTarget(null);
              }}
            />
          )}
        </WorkspaceUiProvider>
       </DiagnosticsProvider>
      </EditorStateProvider>

      <StatusBar items={state.status} />
    </div>
    </WorkspaceProvider>
  );
}

type T = ReturnType<typeof useT>["t"];

async function confirmDeletePage(t: T, name: string): Promise<boolean> {
  return confirmAsync(t("page.deleteConfirm", name), { title: t("page.delete"), danger: true });
}

function pageContextItems(
  t: T,
  pageId: string,
  canDelete: boolean,
  handlers: {
    onRename: (pageId: string) => void;
    onDuplicate: (pageId: string) => void;
    onCopyToProfile: (pageId: string) => void;
    onDelete: (pageId: string) => void;
  },
): ContextMenuItem[] {
  return [
    { label: t("ctx.page.rename"), onSelect: () => handlers.onRename(pageId) },
    { label: t("page.duplicate"), icon: <Copy size={13} />, onSelect: () => handlers.onDuplicate(pageId) },
    { label: t("page.copyToProfile"), onSelect: () => handlers.onCopyToProfile(pageId) },
    { label: t("page.delete"), icon: <Trash2 size={13} />, danger: true, disabled: !canDelete, onSelect: () => handlers.onDelete(pageId) },
  ];
}
