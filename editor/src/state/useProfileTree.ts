import { useCallback, useEffect, useState } from "react";
import type { ProfileTreeNode } from "../api/types";
import { api } from "../api/client";
import { tempId } from "./tempId";
import { findNode, flattenLeafIds, insertNode, isSelfOrDescendant, removeNode } from "./tree";

/** Removes, from `tree`, any existing node for every profile id `node` itself contains (a leaf, or every
 * leaf inside a pasted folder) — see `insertPrebuiltNode`'s doc comment. */
function removeSelfHealedIds(tree: ProfileTreeNode[], node: ProfileTreeNode): ProfileTreeNode[] {
  let next = tree;
  for (const id of flattenLeafIds([node], "profile")) next = removeNode(next, id).tree;
  return next;
}

/** The root profile tree (docs/design/hierarchy-tree-and-folders.md) — every mutation saves to the
 * server at once (there's no "dirty" concept here the way an open profile's pages have; profiles aren't
 * loaded together, so there's nothing to batch into one Save). */
export function useProfileTree() {
  const [tree, setTree] = useState<ProfileTreeNode[]>([]);
  const [loaded, setLoaded] = useState(false);

  /** Returns the freshly-fetched tree too, not just updating state — pasting a copied profile folder
   * creates several profiles one at a time (each a separate await), during which someone else could in
   * principle have changed the tree; refreshing and inserting into that exact return value, then saving,
   * is safer than trusting a `tree` closure captured before all those awaits. */
  const refresh = useCallback(async (): Promise<ProfileTreeNode[]> => {
    try {
      const nodes = await api.getProfileTree();
      setTree(nodes);
      setLoaded(true);
      return nodes;
    } catch {
      setLoaded(true);
      return [];
    }
  }, []);

  useEffect(() => { refresh(); }, [refresh]);

  const persist = useCallback((next: ProfileTreeNode[]) => {
    setTree(next);
    api.saveProfileTree(next).catch(() => {});
  }, []);

  const createProfileFolder = useCallback(
    (parentFolderId: string | null, name: string) => {
      const folder: ProfileTreeNode = { type: "folder", id: tempId("pfolder"), name, children: [] };
      persist(insertNode(tree, folder, parentFolderId, Number.MAX_SAFE_INTEGER));
    },
    [tree, persist],
  );

  const renameProfileFolder = useCallback(
    (id: string, name: string) => {
      const rename = (nodes: ProfileTreeNode[]): ProfileTreeNode[] =>
        nodes.map((n) => (n.id === id ? { ...n, name } : n.children ? { ...n, children: rename(n.children) } : n));
      persist(rename(tree));
    },
    [tree, persist],
  );

  /** Removes the folder; `keepContents` re-parents its children to the folder's own parent (root, since
   * this function isn't told the parent — callers that need "move up into the parent" build that
   * themselves; here `keepContents` just flattens them to the root, which is the common case: most
   * profile folders aren't nested). */
  const deleteProfileFolder = useCallback(
    (id: string, keepContents: boolean) => {
      const target = findNode(tree, id);
      const { tree: withoutFolder } = removeNode(tree, id);
      if (keepContents && target?.children && target.children.length > 0) {
        persist([...withoutFolder, ...target.children]);
      } else {
        persist(withoutFolder);
      }
    },
    [tree, persist],
  );

  /** Moves (never copies) the node `id` under `targetFolderId` (null: root) at `index`. Refuses moving a
   * folder into itself or one of its own descendants. New profiles created elsewhere (New profile button)
   * aren't in the tree yet — the server appends them at the root on the next GET, so a `refresh()` after
   * creating one picks that up. */
  const moveNode = useCallback(
    (id: string, targetFolderId: string | null, index: number) => {
      if (targetFolderId && isSelfOrDescendant(tree, id, targetFolderId)) return;
      const { tree: without, removed } = removeNode(tree, id);
      if (!removed) return;
      persist(insertNode(without, removed, targetFolderId, index));
    },
    [tree, persist],
  );

  /** Inserts an already-built node (e.g. a profile folder just recreated from a paste, its profiles
   * already created on the server) against the latest tree from the server, not a possibly-stale local
   * one — see `refresh`'s doc comment. The profile(s) it references were just created via `api.createProfile`,
   * so this `refresh()` GET can itself have already self-healed the tree by appending them at the root
   * (see ProfileTreeStore.GetNormalized) — inserting on top of that would leave two nodes for the same id
   * (visible until the next reload prunes the duplicate server-side), so any such self-healed copy is
   * removed first. */
  const insertPrebuiltNode = useCallback(async (node: ProfileTreeNode, parentFolderId: string | null) => {
    const latest = await refresh();
    const withoutSelfHealed = removeSelfHealedIds(latest, node);
    const next = insertNode(withoutSelfHealed, node, parentFolderId, Number.MAX_SAFE_INTEGER);
    setTree(next);
    await api.saveProfileTree(next).catch(() => {});
  }, [refresh]);

  /** Removes a profile leaf's own tree node after the profile itself has been deleted server-side —
   * otherwise the tree still holds a leaf for an id `state.profiles` no longer has, rendering as a
   * nameless "…" row until the next reload (the server would self-heal it on the next GET, but the tree
   * already in memory here wouldn't). */
  const removeProfileNode = useCallback(
    (id: string) => persist(removeNode(tree, id).tree),
    [tree, persist],
  );

  return { tree, loaded, refresh, createProfileFolder, renameProfileFolder, deleteProfileFolder, moveNode, insertPrebuiltNode, removeProfileNode };
}

export type ProfileTreeApi = ReturnType<typeof useProfileTree>;
