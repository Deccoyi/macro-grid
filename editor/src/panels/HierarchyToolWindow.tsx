import { useCallback, useEffect, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Copy, Folder, FolderPlus, LayoutTemplate, LibraryBig, Pencil, Plus, Trash2 } from "lucide-react";
import type { PageTreeNode } from "@macro/renderer";
import type { ProfileTreeNode } from "../api/types";
import { api } from "../api/client";
import { choiceAsync, confirmAsync, promptAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { useEditorStateContext } from "../state/EditorStateContext";
import { normalizePageTree } from "../state/pageTree";
import { clearPluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { useProfileTreeContext } from "../state/ProfileTreeContext";
import { countContents, findNode, findParentFolderId, type GenericTreeNode, removeNode } from "../state/tree";
import { useWorkspaceUi } from "../workspace/WorkspaceUiContext";
import { ContextMenu, type ContextMenuEntry } from "./ContextMenu";

type ProfileCacheEntry = "loading" | "error" | { name: string; pages: { id: string; name: string }[]; pageTree: PageTreeNode[] };

type DragPayload = { kind: "page" | "pageFolder"; id: string } | { kind: "profile" | "profileFolder"; id: string };

/** Where a drop lands relative to `targetId`: `sourceId` is removed from the tree first (simulated, not
 * mutated) so the returned index is already correct for insertion into the post-removal tree — computing
 * it against the pre-removal tree would be off by one whenever the source and target start in the same
 * list and the source comes first. */
function resolveDropLocation<N extends GenericTreeNode>(
  tree: N[],
  sourceId: string,
  targetId: string,
  position: "above" | "inside" | "below",
  targetIsFolder: boolean,
): { parentFolderId: string | null; index: number } {
  const { tree: withoutSource } = removeNode(tree, sourceId);
  if (position === "inside" && targetIsFolder) {
    const target = findNode(withoutSource, targetId);
    return { parentFolderId: targetId, index: target?.children?.length ?? 0 };
  }
  const locate = (nodes: N[], parentId: string | null): { parentId: string | null; index: number } | null => {
    for (let i = 0; i < nodes.length; i++) {
      if (nodes[i]!.id === targetId) return { parentId, index: i };
      if (nodes[i]!.children) {
        const found = locate(nodes[i]!.children as N[], nodes[i]!.id);
        if (found) return found;
      }
    }
    return null;
  };
  const loc = locate(withoutSource, null);
  if (!loc) return { parentFolderId: null, index: 0 };
  return { parentFolderId: loc.parentId, index: position === "below" ? loc.index + 1 : loc.index };
}

/** The Hierarchy tool window: a real tree — profiles (optionally in profile folders) at the root, each
 * profile's pages (optionally in page folders) underneath. The open profile's pages are always visible and
 * live (in-memory, dirty-until-Save); every other profile's pages are loaded on demand the first time it's
 * expanded (see docs/design/hierarchy-tree-and-folders.md — GET /api/profiles/{id}, cached for the
 * session) so a large profile collection costs nothing until the user actually opens it. */
export function HierarchyToolWindow() {
  const { t, tn } = useT();
  const state = useEditorStateContext();
  const { openPageContextMenu, showProfileProperties, clearProfileProperties, profilePropertiesTarget, renameTarget, startRename, clearRename, treeSelection, setTreeSelection } = useWorkspaceUi();
  const profileTree = useProfileTreeContext();

  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());
  const [profileCache, setProfileCache] = useState<Map<string, ProfileCacheEntry>>(new Map());
  const [renamingFolderId, setRenamingFolderId] = useState<string | null>(null);
  const [ctxMenu, setCtxMenu] = useState<{ x: number; y: number; items: ContextMenuEntry[] } | null>(null);
  const [drag, setDrag] = useState<DragPayload | null>(null);
  const [dropTarget, setDropTarget] = useState<{ id: string; position: "above" | "inside" | "below" } | null>(null);

  const { profile } = state;
  const openProfileId = profile?.id ?? null;

  // A cached (non-open) profile's snapshot goes stale the moment it stops being the open one — it might
  // get edited and saved before the user comes back to it. Dropping the previous open profile's cache
  // entry on every switch forces a fresh GET /api/profiles/{id} the next time it's expanded, rather than
  // showing whatever it looked like the last time it was merely browsed.
  const prevOpenIdRef = useRef<string | null>(null);
  useEffect(() => {
    const prev = prevOpenIdRef.current;
    if (prev && prev !== openProfileId) {
      setProfileCache((p) => {
        if (!p.has(prev)) return p;
        const next = new Map(p);
        next.delete(prev);
        return next;
      });
    }
    prevOpenIdRef.current = openProfileId;
  }, [openProfileId]);

  const toggleExpanded = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const ensureProfileLoaded = useCallback((id: string) => {
    setProfileCache((prev) => {
      if (prev.has(id)) return prev;
      const next = new Map(prev);
      next.set(id, "loading");
      api.getProfile(id).then((full) => {
        const pageTree = normalizePageTree(full.pageTree, full.pages);
        setProfileCache((p) => new Map(p).set(id, { name: full.name, pages: full.pages.map((pg) => ({ id: pg.id, name: pg.name })), pageTree }));
      }).catch(() => {
        setProfileCache((p) => new Map(p).set(id, "error"));
      });
      return next;
    });
  }, []);

  const toggleProfileExpanded = (id: string) => {
    const willExpand = !expandedIds.has(id);
    toggleExpanded(id);
    if (willExpand && id !== openProfileId) ensureProfileLoaded(id);
  };

  // ---- Drag and drop -----------------------------------------------------------------------------

  const dragProps = (payload: DragPayload, canAcceptDrop: (d: DragPayload) => boolean, isFolder: boolean, targetId: string) => ({
    draggable: true,
    onDragStart: (e: React.DragEvent) => { e.dataTransfer.effectAllowed = "move"; setDrag(payload); },
    onDragEnd: () => { setDrag(null); setDropTarget(null); },
    onDragOver: (e: React.DragEvent) => {
      if (!drag || drag.id === targetId || !canAcceptDrop(drag)) return;
      e.preventDefault();
      const rect = e.currentTarget.getBoundingClientRect();
      const ratio = (e.clientY - rect.top) / rect.height;
      const position: "above" | "inside" | "below" = isFolder && ratio > 0.25 && ratio < 0.75 ? "inside" : ratio <= 0.5 ? "above" : "below";
      setDropTarget({ id: targetId, position });
    },
    onDragLeave: () => setDropTarget((d) => (d?.id === targetId ? null : d)),
    onDrop: (e: React.DragEvent) => {
      e.preventDefault();
      const current = dropTarget;
      setDrag(null);
      setDropTarget(null);
      if (!drag || !current || current.id !== targetId) return;
      handleDrop(drag, targetId, current.position, isFolder);
    },
  });

  const handleDrop = (source: DragPayload, targetId: string, position: "above" | "inside" | "below", targetIsFolder: boolean) => {
    const isPageFamily = source.kind === "page" || source.kind === "pageFolder";
    if (isPageFamily) {
      if (!profile) return;
      const { parentFolderId, index } = resolveDropLocation(profile.pageTree ?? [], source.id, targetId, position, targetIsFolder);
      state.movePageTreeNode(source.id, parentFolderId, index);
    } else {
      const { parentFolderId, index } = resolveDropLocation(profileTree.tree, source.id, targetId, position, targetIsFolder);
      profileTree.moveNode(source.id, parentFolderId, index);
    }
  };

  const dropLineStyle = (id: string): React.CSSProperties => {
    if (dropTarget?.id !== id) return {};
    if (dropTarget.position === "inside") return { background: "var(--ms-accent-bg-muted)", outline: "1px solid var(--ms-accent)", outlineOffset: -1 };
    return dropTarget.position === "above"
      ? { boxShadow: "inset 0 2px 0 var(--ms-accent)" }
      : { boxShadow: "inset 0 -2px 0 var(--ms-accent)" };
  };

  // ---- Folder commands ----------------------------------------------------------------------------

  const openFolderMenu = (
    e: React.MouseEvent,
    onRename: () => void,
    onNewSubfolder: (() => void) | null,
    onDelete: () => void,
  ) => {
    e.preventDefault();
    e.stopPropagation();
    const items: ContextMenuEntry[] = [];
    if (onNewSubfolder) items.push({ label: t("folder.new"), icon: <FolderPlus size={13} />, onSelect: onNewSubfolder });
    items.push({ label: t("folder.rename"), icon: <Pencil size={13} />, onSelect: onRename });
    items.push({ label: t("folder.delete"), icon: <Trash2 size={13} />, danger: true, onSelect: onDelete });
    setCtxMenu({ x: e.clientX, y: e.clientY, items });
  };

  const confirmDeleteFolder = async <N extends GenericTreeNode>(node: N, leafType: string, onDelete: (keepContents: boolean) => void) => {
    const { leaves, folders } = countContents(node, leafType);
    if (leaves === 0 && folders === 0) { onDelete(false); return; }
    const choice = await choiceAsync(
      tn("folder.deleteConfirm", leaves + folders),
      [
        { value: "delete", label: t("folder.delete.deleteAndContents"), danger: true },
        { value: "keep", label: t("folder.delete.keepContents"), primary: true },
      ],
      { title: t("folder.delete") },
    );
    if (choice === "delete") onDelete(false);
    else if (choice === "keep") onDelete(true);
  };

  if (!profile) return null;

  // ---- Rendering ------------------------------------------------------------------------------------

  const renderPageNodes = (nodes: PageTreeNode[], depth: number): React.ReactNode[] =>
    nodes.map((node) => (node.type === "folder" ? renderPageFolder(node, depth) : renderPageLeaf(node, depth)));

  function renderPageFolder(node: PageTreeNode, depth: number) {
    const expanded = expandedIds.has(node.id);
    const editing = renamingFolderId === node.id;
    const selected = treeSelection?.kind === "pageFolder" && treeSelection.id === node.id;
    return (
      <div key={node.id}>
        <Row
          depth={depth}
          icon={<Chevron open={expanded} onClick={() => toggleExpanded(node.id)} />}
          glyph={<Folder size={13} style={{ flex: "0 0 auto" }} />}
          label={node.name ?? ""}
          selected={selected}
          editing={editing}
          onCommitRename={(name) => { state.renamePageFolder(node.id, name); clearRenamingFolder(); }}
          onCancelRename={clearRenamingFolder}
          onClick={() => setTreeSelection({ kind: "pageFolder", id: node.id, profileId: profile!.id, parentFolderId: findParentFolderId(profile!.pageTree ?? [], node.id) ?? null })}
          onDoubleClick={() => setRenamingFolderId(node.id)}
          onKeyDownRename={() => setRenamingFolderId(node.id)}
          onContextMenu={(e) => openFolderMenu(
            e,
            () => setRenamingFolderId(node.id),
            () => createPageSubfolder(node.id),
            () => confirmDeleteFolder(node, "page", (keep) => state.deletePageFolder(node.id, keep)),
          )}
          dragProps={dragProps({ kind: "pageFolder", id: node.id }, (d) => d.kind === "page" || d.kind === "pageFolder", true, node.id)}
          dropStyle={dropLineStyle(node.id)}
        />
        {expanded && renderPageNodes(node.children ?? [], depth + 1)}
      </div>
    );
  }

  function renderPageLeaf(node: PageTreeNode, depth: number) {
    const page = profile!.pages.find((p) => p.id === node.id);
    if (!page) return null;
    const isActive = page.id === state.currentPageId && !profilePropertiesTarget;
    const dirty = state.dirtyPageIds.has(page.id);
    const editing = renameTarget?.kind === "page" && renameTarget.id === page.id;
    return (
      <div
        key={page.id}
        {...dragProps({ kind: "page", id: page.id }, (d) => d.kind === "page" || d.kind === "pageFolder", false, page.id)}
        style={dropLineStyle(page.id)}
      >
        <Row
          depth={depth}
          glyph={<PageGlyph />}
          label={page.name + (dirty ? " *" : "")}
          italic={dirty}
          activeAccent={isActive}
          editing={editing}
          onCommitRename={(name) => { state.renamePage(page.id, name); clearRename(); }}
          onCancelRename={clearRename}
          onClick={() => {
            state.setCurrentPageId(page.id);
            state.setSelectedIds([]);
            clearProfileProperties();
            clearPluginTreeSelection();
            setTreeSelection({ kind: "page", id: page.id, profileId: profile!.id, parentFolderId: findParentFolderId(profile!.pageTree ?? [], page.id) ?? null });
          }}
          onDoubleClick={() => startRename("page", page.id)}
          onKeyDownRename={() => startRename("page", page.id)}
          onContextMenu={(e) => {
            e.preventDefault();
            state.setCurrentPageId(page.id);
            openPageContextMenu(e.clientX, e.clientY, page.id);
          }}
          actions={
            <>
              <IconBtn label={t("page.duplicate")} onClick={(e) => { e?.stopPropagation(); state.duplicatePage(page.id); }}>
                <Copy size={12} />
              </IconBtn>
              {profile!.pages.length > 1 && (
                <IconBtn label={t("page.delete")} danger onClick={async (e) => {
                  e?.stopPropagation();
                  if (await confirmAsync(t("page.deleteConfirm", page.name), { title: t("page.delete"), danger: true })) state.deletePage(page.id);
                }}>
                  <Trash2 size={12} />
                </IconBtn>
              )}
            </>
          }
        />
      </div>
    );
  }

  const createPageSubfolder = async (parentFolderId: string | null) => {
    const name = await promptAsync(t("folder.renamePrompt"), t("folder.new"), { title: t("folder.new") });
    if (name && name.trim()) state.createPageFolder(parentFolderId, name.trim());
  };

  const clearRenamingFolder = () => setRenamingFolderId(null);

  function renderProfileNodes(nodes: ProfileTreeNode[], depth: number): React.ReactNode[] {
    return nodes.map((node) => (node.type === "folder" ? renderProfileFolder(node, depth) : renderProfileLeaf(node, depth)));
  }

  function renderProfileFolder(node: ProfileTreeNode, depth: number) {
    const expanded = expandedIds.has(node.id);
    const editing = renamingFolderId === node.id;
    const selected = treeSelection?.kind === "profileFolder" && treeSelection.id === node.id;
    return (
      <div key={node.id}>
        <Row
          depth={depth}
          icon={<Chevron open={expanded} onClick={() => toggleExpanded(node.id)} />}
          glyph={<Folder size={13} style={{ flex: "0 0 auto" }} />}
          label={node.name ?? ""}
          selected={selected}
          editing={editing}
          onCommitRename={(name) => { profileTree.renameProfileFolder(node.id, name); clearRenamingFolder(); }}
          onCancelRename={clearRenamingFolder}
          onClick={() => setTreeSelection({ kind: "profileFolder", id: node.id, parentFolderId: findParentFolderId(profileTree.tree, node.id) ?? null })}
          onDoubleClick={() => setRenamingFolderId(node.id)}
          onKeyDownRename={() => setRenamingFolderId(node.id)}
          onContextMenu={(e) => openFolderMenu(
            e,
            () => setRenamingFolderId(node.id),
            async () => {
              const name = await promptAsync(t("folder.renamePrompt"), t("folder.new"), { title: t("folder.new") });
              if (name && name.trim()) profileTree.createProfileFolder(node.id, name.trim());
            },
            () => confirmDeleteFolder(node, "profile", (keep) => profileTree.deleteProfileFolder(node.id, keep)),
          )}
          dragProps={dragProps({ kind: "profileFolder", id: node.id }, (d) => d.kind === "profile" || d.kind === "profileFolder", true, node.id)}
          dropStyle={dropLineStyle(node.id)}
        />
        {expanded && renderProfileNodes(node.children ?? [], depth + 1)}
      </div>
    );
  }

  function renderProfileLeaf(node: ProfileTreeNode, depth: number) {
    const isOpen = node.id === openProfileId;
    const expanded = isOpen || expandedIds.has(node.id);
    const cached = profileCache.get(node.id);
    const selected = node.id === profilePropertiesTarget?.profileId;
    const name = isOpen ? profile!.name : typeof cached === "object" ? cached.name : (state.profiles.find((p) => p.id === node.id)?.name ?? "…");
    const editing = renameTarget?.kind === "profile" && renameTarget.id === node.id;

    return (
      <div key={node.id}>
        <Row
          depth={depth}
          icon={<Chevron open={expanded} onClick={() => toggleProfileExpanded(node.id)} />}
          label={name}
          selected={selected}
          editing={editing}
          onCommitRename={(newName) => { if (isOpen) state.renameProfile(newName); clearRename(); }}
          onCancelRename={clearRename}
          onClick={() => {
            if (!isOpen) state.selectProfile(node.id);
            showProfileProperties(node.id);
            clearPluginTreeSelection();
            setTreeSelection({ kind: "profile", id: node.id, parentFolderId: findParentFolderId(profileTree.tree, node.id) ?? null });
          }}
          onDoubleClick={() => { if (isOpen) startRename("profile", node.id); }}
          onKeyDownRename={() => { if (isOpen) startRename("profile", node.id); }}
          onContextMenu={(e) => {
            e.preventDefault();
            e.stopPropagation();
            const items: ContextMenuEntry[] = [];
            if (isOpen) {
              items.push({ label: t("page.add"), icon: <Plus size={13} />, onSelect: () => state.addPage() });
              items.push({ label: t("folder.new"), icon: <FolderPlus size={13} />, onSelect: () => createPageSubfolder(null) });
              items.push({ divider: true });
            }
            items.push({ label: t("profile.rename"), icon: <Pencil size={13} />, disabled: !isOpen, onSelect: () => startRename("profile", node.id) });
            items.push({
              label: t("profile.delete"), icon: <Trash2 size={13} />, danger: true, disabled: !isOpen || state.profiles.length <= 1,
              onSelect: async () => {
                if (await confirmAsync(t("profile.deleteConfirm", name), { title: t("profile.delete"), danger: true })) {
                  await state.deleteProfile(node.id);
                  profileTree.removeProfileNode(node.id);
                }
              },
            });
            setCtxMenu({ x: e.clientX, y: e.clientY, items });
          }}
          dragProps={dragProps({ kind: "profile", id: node.id }, (d) => d.kind === "profile" || d.kind === "profileFolder", true, node.id)}
          dropStyle={dropLineStyle(node.id)}
        />
        {expanded && (
          isOpen ? renderPageNodes(profile!.pageTree ?? [], depth + 1) :
          cached === "loading" ? <LoadingRow depth={depth + 1} text={t("app.loading")} /> :
          cached === "error" ? <LoadingRow depth={depth + 1} text={t("hierarchy.loadError")} /> :
          cached ? renderCachedPageNodes(cached.pageTree, cached.pages, node.id, depth + 1) :
          null
        )}
      </div>
    );
  }

  /** A page inside a NON-open (cached) profile — clicking it switches to that profile and opens the page
   * in one step, rather than requiring "click the profile, then click the page" (switching is exactly what
   * clicking the profile row itself already does; a page two levels down should do the same, not less). */
  function renderCachedPageNodes(nodes: PageTreeNode[], pages: { id: string; name: string }[], ownerProfileId: string, depth: number): React.ReactNode[] {
    return nodes.map((node) => {
      if (node.type === "folder") {
        const expanded = expandedIds.has(node.id);
        return (
          <div key={node.id}>
            <Row
              depth={depth}
              icon={<Chevron open={expanded} onClick={() => toggleExpanded(node.id)} />}
              glyph={<Folder size={13} style={{ flex: "0 0 auto" }} />}
              label={node.name ?? ""}
              onClick={() => toggleExpanded(node.id)}
            />
            {expanded && renderCachedPageNodes(node.children ?? [], pages, ownerProfileId, depth + 1)}
          </div>
        );
      }
      const page = pages.find((p) => p.id === node.id);
      if (!page) return null;
      return (
        <Row
          key={page.id}
          depth={depth}
          glyph={<PageGlyph />}
          label={page.name}
          onClick={async () => {
            await state.selectProfile(ownerProfileId);
            state.setCurrentPageId(page.id);
            state.setSelectedIds([]);
            clearProfileProperties();
            clearPluginTreeSelection();
          }}
        />
      );
    });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }}>
      <div
        style={{ flex: 1, overflowY: "auto", padding: "6px 0" }}
        onContextMenu={(e) => {
          if (e.target !== e.currentTarget) return; // a row's own menu already handled it and stopped propagation
          e.preventDefault();
          setCtxMenu({
            x: e.clientX,
            y: e.clientY,
            items: [{
              label: t("folder.new"),
              icon: <FolderPlus size={13} />,
              onSelect: async () => {
                const name = await promptAsync(t("folder.renamePrompt"), t("folder.new"), { title: t("folder.new") });
                if (name && name.trim()) profileTree.createProfileFolder(null, name.trim());
              },
            }],
          });
        }}
      >
        {renderProfileNodes(profileTree.tree, 0)}
      </div>
      <div style={{ padding: 8, borderTop: "1px solid var(--ms-border)", display: "flex", gap: 6 }}>
        <IconBtn label={t("profile.new")} onClick={async () => { await state.createProfile(); profileTree.refresh(); }}>
          <LibraryBig size={13} />
        </IconBtn>
        <IconBtn label={t("page.add")} onClick={() => state.addPage()}>
          <LayoutTemplate size={13} />
        </IconBtn>
        <IconBtn label={t("folder.new")} onClick={() => createPageSubfolder(null)}>
          <FolderPlus size={13} />
        </IconBtn>
      </div>

      {ctxMenu && (
        <ContextMenu x={ctxMenu.x} y={ctxMenu.y} items={ctxMenu.items} onClose={() => setCtxMenu(null)} />
      )}
    </div>
  );
}

function Chevron({ open, onClick }: { open: boolean; onClick: () => void }) {
  const Icon = open ? ChevronDown : ChevronRight;
  return (
    <span
      onClick={(e) => { e.stopPropagation(); onClick(); }}
      style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 14, flex: "0 0 auto", color: "inherit" }}
    >
      <Icon size={13} strokeWidth={1.75} />
    </span>
  );
}

function PageGlyph() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" style={{ flex: "0 0 auto" }}>
      <rect x="4" y="3" width="16" height="18" rx="1.5" />
      <line x1="8" y1="8" x2="16" y2="8" />
      <line x1="8" y1="12" x2="16" y2="12" />
    </svg>
  );
}

function LoadingRow({ depth, text }: { depth: number; text: string }) {
  return (
    <div style={{ padding: `4px 10px 4px ${10 + depth * 16}px`, fontSize: 12, color: "var(--ms-text-secondary)" }}>
      {text}
    </div>
  );
}

interface RowProps {
  depth: number;
  icon?: React.ReactNode;
  glyph?: React.ReactNode;
  label: string;
  italic?: boolean;
  selected?: boolean;
  activeAccent?: boolean;
  disabled?: boolean;
  editing?: boolean;
  onCommitRename?: (name: string) => void;
  onCancelRename?: () => void;
  onClick?: () => void;
  onDoubleClick?: () => void;
  onKeyDownRename?: () => void;
  onContextMenu?: (e: React.MouseEvent) => void;
  actions?: React.ReactNode;
  dragProps?: {
    draggable: boolean;
    onDragStart: (e: React.DragEvent) => void;
    onDragEnd: () => void;
    onDragOver: (e: React.DragEvent) => void;
    onDragLeave: () => void;
    onDrop: (e: React.DragEvent) => void;
  };
  dropStyle?: React.CSSProperties;
}

/** One row of the tree — a profile, a profile folder, a page or a page folder. The highlight rule from the
 * mockups stays: no highlight just for being open/expanded, only `selected` (this profile's Properties are
 * showing) or `activeAccent` (this is the open document page) get the accent treatment. */
function Row({
  depth, icon, glyph, label, italic, selected, activeAccent, disabled, editing, onCommitRename, onCancelRename,
  onClick, onDoubleClick, onKeyDownRename, onContextMenu, actions, dragProps, dropStyle,
}: RowProps) {
  const [hovered, setHovered] = useState(false);
  const accent = selected || activeAccent;
  return (
    <div
      tabIndex={disabled ? undefined : 0}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onClick={disabled ? undefined : onClick}
      onDoubleClick={disabled ? undefined : onDoubleClick}
      onContextMenu={disabled ? undefined : onContextMenu}
      onKeyDown={(e) => { if (e.key === "F2" && onKeyDownRename) { e.preventDefault(); onKeyDownRename(); } }}
      {...dragProps}
      style={{
        padding: `4px 10px 4px ${10 + depth * 16}px`,
        marginLeft: accent ? -2 : 0,
        borderLeft: accent ? "2px solid var(--ms-accent)" : "2px solid transparent",
        display: "flex",
        alignItems: "center",
        gap: 6,
        cursor: disabled ? "default" : "pointer",
        color: disabled ? "var(--ms-text-disabled)" : accent ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
        background: accent ? "var(--ms-accent-bg-muted)" : hovered ? "var(--ms-bg-surface-raised)" : "transparent",
        ...dropStyle,
      }}
    >
      {icon}
      {glyph}
      {editing ? (
        <InlineNameInput value={label.replace(/ \*$/, "")} onCommit={(n) => onCommitRename?.(n)} onCancel={() => onCancelRename?.()} />
      ) : (
        <span style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontStyle: italic ? "italic" : "normal" }}>
          {label}
        </span>
      )}
      {!editing && hovered && actions && <span style={{ display: "flex", alignItems: "center", gap: 2 }}>{actions}</span>}
    </div>
  );
}

function InlineNameInput({ value, onCommit, onCancel }: { value: string; onCommit: (name: string) => void; onCancel: () => void }) {
  const committedRef = useRef(false);
  return (
    <input
      type="text"
      defaultValue={value}
      autoFocus
      onFocus={(e) => e.currentTarget.select()}
      onClick={(e) => e.stopPropagation()}
      onDoubleClick={(e) => e.stopPropagation()}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === "Enter") e.currentTarget.blur();
        else if (e.key === "Escape") { committedRef.current = true; onCancel(); }
      }}
      onBlur={(e) => {
        if (committedRef.current) return;
        committedRef.current = true;
        const name = e.currentTarget.value.trim();
        if (name && name !== value) onCommit(name);
        else onCancel();
      }}
      style={{
        flex: 1, minWidth: 0, height: 20, padding: "0 4px", fontSize: "inherit",
        color: "var(--ms-text-primary)", background: "var(--ms-bg-canvas)", border: "1px solid var(--ms-accent)", borderRadius: 2,
      }}
    />
  );
}

function IconBtn({
  label, onClick, danger, disabled, children,
}: {
  label: string;
  onClick: (e?: React.MouseEvent) => void;
  danger?: boolean;
  disabled?: boolean;
  children: React.ReactNode;
}) {
  const color = disabled ? "var(--ms-text-disabled)" : danger ? "var(--ms-danger)" : "var(--ms-text-secondary)";
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      disabled={disabled}
      onClick={(e) => { e.stopPropagation(); onClick(e); }}
      style={{ width: 18, height: 18, display: "flex", alignItems: "center", justifyContent: "center", border: "none", background: "transparent", color, cursor: disabled ? "default" : "pointer" }}
      onMouseEnter={(e) => { if (!disabled) e.currentTarget.style.background = "var(--ms-bg-surface-raised)"; }}
      onMouseLeave={(e) => { e.currentTarget.style.background = "transparent"; }}
    >
      {children}
    </button>
  );
}
