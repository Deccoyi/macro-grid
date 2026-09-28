import type { Page, PageTreeNode } from "@macro/renderer";
import { flattenLeafIds } from "./tree";

/** Repairs `tree` against the profile's actual `pages`: a page missing from the tree is appended at the
 * root; a node naming a page that no longer exists is dropped; a duplicate is dropped (first occurrence
 * wins). A profile with no tree at all (old data, or one that never had folders) normalizes to a flat
 * root-level list in `pages` order — the same order it already showed in before folders existed. */
export function normalizePageTree(tree: PageTreeNode[] | undefined, pages: Page[]): PageTreeNode[] {
  const pageIds = new Set(pages.map((p) => p.id));
  const seen = new Set<string>();
  const pruned = prune(tree ?? [], pageIds, seen);
  const missing = pages.filter((p) => !seen.has(p.id));
  return [...pruned, ...missing.map((p): PageTreeNode => ({ type: "page", id: p.id }))];
}

function prune(nodes: PageTreeNode[], pageIds: Set<string>, seen: Set<string>): PageTreeNode[] {
  const result: PageTreeNode[] = [];
  for (const node of nodes) {
    if (node.type === "folder") {
      result.push({ ...node, children: prune(node.children ?? [], pageIds, seen) });
    } else if (pageIds.has(node.id) && !seen.has(node.id)) {
      seen.add(node.id);
      result.push(node);
    }
  }
  return result;
}

/** The tree's depth-first page order. */
export function pageOrderFromTree(tree: PageTreeNode[]): string[] {
  return flattenLeafIds(tree, "page");
}

/** Reorders `pages` to match the tree — the plan's invariant, kept after every tree change so the phone
 * app's page order (swipes, "next page") always matches what the editor's Hierarchy shows. */
export function reorderPagesByTree(pages: Page[], tree: PageTreeNode[]): Page[] {
  const order = pageOrderFromTree(tree);
  const byId = new Map(pages.map((p) => [p.id, p]));
  const ordered = order.map((id) => byId.get(id)).filter((p): p is Page => !!p);
  // Defensive: any page the tree somehow still doesn't mention (shouldn't happen post-normalize) is kept,
  // appended, rather than silently dropped.
  const seen = new Set(ordered.map((p) => p.id));
  return [...ordered, ...pages.filter((p) => !seen.has(p.id))];
}
