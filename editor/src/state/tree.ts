/** Generic operations shared by the page tree (inside one profile) and the root profile tree — see
 * docs/plans/hierarchy-tree-and-folders-plan.md. Both PageTreeNode and ProfileTreeNode share this exact
 * shape (a "folder" node has `children`, a leaf node — "page" or "profile" — never does), so one set of
 * pure functions serves both instead of duplicating tree-walking logic per kind. */
export interface GenericTreeNode {
  type: string;
  id: string;
  name?: string;
  children?: GenericTreeNode[];
}

export function findNode<N extends GenericTreeNode>(nodes: N[], id: string): N | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children) {
      const found = findNode(node.children as N[], id);
      if (found) return found;
    }
  }
  return null;
}

/** Removes the node with `id` wherever it is (a leaf or a whole folder, with its contents). */
export function removeNode<N extends GenericTreeNode>(nodes: N[], id: string): { tree: N[]; removed: N | null } {
  let removed: N | null = null;
  const withoutHere = nodes.filter((n) => {
    if (n.id === id) { removed = n; return false; }
    return true;
  });
  if (removed) return { tree: withoutHere, removed };

  const tree = withoutHere.map((n) => {
    if (!n.children) return n;
    const result = removeNode(n.children as N[], id);
    if (!result.removed) return n;
    removed = result.removed;
    return { ...n, children: result.tree } as N;
  });
  return { tree, removed };
}

/** Inserts `node` at `index` under `parentFolderId` (null: the root list). */
export function insertNode<N extends GenericTreeNode>(nodes: N[], node: N, parentFolderId: string | null, index: number): N[] {
  if (parentFolderId === null) {
    const next = [...nodes];
    next.splice(Math.max(0, Math.min(index, next.length)), 0, node);
    return next;
  }
  return nodes.map((n) => {
    if (n.id === parentFolderId) {
      const children = [...(n.children ?? [])];
      children.splice(Math.max(0, Math.min(index, children.length)), 0, node);
      return { ...n, children } as N;
    }
    if (n.children) return { ...n, children: insertNode(n.children as N[], node, parentFolderId, index) } as N;
    return n;
  });
}

/** True if `targetId` is `ancestorId` itself or anywhere inside it — a folder can't be dropped into
 * itself or one of its own descendants. */
export function isSelfOrDescendant<N extends GenericTreeNode>(nodes: N[], ancestorId: string, targetId: string): boolean {
  if (ancestorId === targetId) return true;
  const ancestor = findNode(nodes, ancestorId);
  if (!ancestor?.children) return false;
  const stack: N[] = [...(ancestor.children as N[])];
  while (stack.length > 0) {
    const n = stack.pop()!;
    if (n.id === targetId) return true;
    if (n.children) stack.push(...(n.children as N[]));
  }
  return false;
}

/** Every leaf id (a node whose `type` is `leafType`, e.g. "page" or "profile"), depth-first — the order
 * the tree implies for whatever the leaves represent. */
export function flattenLeafIds<N extends GenericTreeNode>(nodes: N[], leafType: string): string[] {
  const ids: string[] = [];
  for (const node of nodes) {
    if (node.type === leafType) ids.push(node.id);
    if (node.children) ids.push(...flattenLeafIds(node.children as N[], leafType));
  }
  return ids;
}

/** The id of the folder directly containing `id` (null: the root), or undefined if `id` isn't in the tree
 * at all — used to compute a Hierarchy selection's paste target (a leaf pastes as its sibling, i.e. into
 * its own parent folder). */
export function findParentFolderId<N extends GenericTreeNode>(nodes: N[], id: string, parentId: string | null = null): string | null | undefined {
  for (const node of nodes) {
    if (node.id === id) return parentId;
    if (node.children) {
      const found = findParentFolderId(node.children as N[], id, node.id);
      if (found !== undefined) return found;
    }
  }
  return undefined;
}

/** Every leaf and folder id, recursively — used to count/confirm "delete folder and contents". */
export function countContents<N extends GenericTreeNode>(node: N, leafType: string): { leaves: number; folders: number } {
  let leaves = 0;
  let folders = 0;
  const walk = (n: N) => {
    if (n.type === leafType) leaves++;
    else if (n.children) { folders++; for (const c of n.children as N[]) walk(c); }
  };
  if (node.children) for (const c of node.children as N[]) walk(c);
  return { leaves, folders };
}
