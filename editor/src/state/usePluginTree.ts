import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "../api/client";
import type { PluginTreeItem } from "../api/types";

type LevelCacheEntry = "loading" | "error" | { items: PluginTreeItem[]; continuationToken: string | null };

const levelKey = (pluginId: string, parentId: string | null) => `${pluginId}::${parentId ?? ""}`;

/** Lazy, per-level tree items of every plugin that opts in to IPluginTreeProvider (mirrors
 * state/useProfileTree.ts's lazy-fetch-and-cache pattern for a non-open profile): nothing is fetched until
 * a level is actually asked for (the plugin's top level, the first time its row is expanded; a deeper level,
 * the first time that node is expanded), and a level already fetched is served from memory afterwards. While
 * mounted (the Plugins tool window is on screen) it also polls PluginTreeChangeLog and drops or reloads only
 * the levels a change actually touched — see docs/design/plugins-tool-window.md. */
export function usePluginTree() {
  const [levels, setLevels] = useState<Map<string, LevelCacheEntry>>(new Map());
  const revisionRef = useRef<number>(-1);

  const loadLevel = useCallback((pluginId: string, parentId: string | null, append = false) => {
    const key = levelKey(pluginId, parentId);
    setLevels((prev) => {
      const existing = prev.get(key);
      if (!append && prev.has(key) && existing !== "error") return prev;
      const token = append && existing && typeof existing === "object" ? (existing.continuationToken ?? undefined) : undefined;
      const next = new Map(prev);
      next.set(key, "loading");
      api.getPluginTreeItems(pluginId, parentId ?? undefined, token)
        .then((page) => {
          setLevels((p) => {
            const prior = append ? p.get(key) : undefined;
            const priorItems = prior && typeof prior === "object" ? prior.items : [];
            const n = new Map(p);
            n.set(key, { items: [...priorItems, ...page.items], continuationToken: page.continuationToken ?? null });
            return n;
          });
        })
        .catch(() => {
          setLevels((p) => new Map(p).set(key, "error"));
        });
      return next;
    });
  }, []);

  /** Called the first time a plugin row or tree node is expanded — a no-op if that level is already loaded
   * or is loading, so re-expanding after a collapse serves the cache instead of another request. */
  const ensureLevelLoaded = useCallback((pluginId: string, parentId: string | null) => {
    loadLevel(pluginId, parentId, false);
  }, [loadLevel]);

  const loadMore = useCallback((pluginId: string, parentId: string | null) => {
    loadLevel(pluginId, parentId, true);
  }, [loadLevel]);

  const getLevel = useCallback((pluginId: string, parentId: string | null): LevelCacheEntry | undefined =>
    levels.get(levelKey(pluginId, parentId)), [levels]);

  /** Drops a level's cached copy (a whole-plugin change, or a level that is not currently expanded — the
   * hierarchy doc's rule: reload only if it's on screen, otherwise just forget it). */
  const dropLevel = useCallback((pluginId: string, parentId: string | null) => {
    setLevels((prev) => {
      const key = levelKey(pluginId, parentId);
      if (!prev.has(key)) return prev;
      const next = new Map(prev);
      next.delete(key);
      return next;
    });
  }, []);

  const dropWholePlugin = useCallback((pluginId: string) => {
    setLevels((prev) => {
      const prefix = `${pluginId}::`;
      const next = new Map(prev);
      let changed = false;
      for (const key of next.keys()) if (key.startsWith(prefix)) { next.delete(key); changed = true; }
      return changed ? next : prev;
    });
  }, []);

  // Poll the change log while this hook is mounted (the Plugins tool window is open). An already-cached
  // level that changed is reloaded only if a caller still wants it (expandedLevels); everything else is
  // just dropped from the cache so the next expand fetches it fresh.
  const expandedRef = useRef<Set<string>>(new Set());
  const setExpanded = useCallback((pluginId: string, parentId: string | null, expanded: boolean) => {
    const key = levelKey(pluginId, parentId);
    if (expanded) expandedRef.current.add(key);
    else expandedRef.current.delete(key);
  }, []);

  useEffect(() => {
    let cancelled = false;
    const poll = async () => {
      try {
        const result = await api.getPluginTreeChanges(revisionRef.current);
        if (cancelled) return;
        revisionRef.current = result.revision;
        if (result.reset) {
          setLevels(new Map());
          return;
        }
        for (const change of result.changes) {
          if (change.wholePlugin) {
            dropWholePlugin(change.pluginId);
            if (expandedRef.current.has(levelKey(change.pluginId, null))) loadLevel(change.pluginId, null, false);
            continue;
          }
          const parentId = change.parentId ?? null;
          dropLevel(change.pluginId, parentId);
          if (expandedRef.current.has(levelKey(change.pluginId, parentId))) loadLevel(change.pluginId, parentId, false);
        }
      } catch {
        // offline or a transient error — try again on the next tick
      }
    };
    const interval = setInterval(poll, 4000);
    poll();
    return () => { cancelled = true; clearInterval(interval); };
  }, [dropLevel, dropWholePlugin, loadLevel]);

  return { getLevel, ensureLevelLoaded, loadMore, setExpanded };
}

export type PluginTreeApi = ReturnType<typeof usePluginTree>;
