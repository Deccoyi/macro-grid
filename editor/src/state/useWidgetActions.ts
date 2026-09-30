import { useCallback } from "react";
import type { ActionBinding, Widget, WidgetType } from "@macro/renderer";
import { api } from "../api/client";
import type { PluginWidgetInfo } from "../api/types";
import { findFreeCell, findFreeCellsForBatch } from "../grid/collision";
import { useT } from "../i18n/I18nContext";
import type { DictKey } from "../i18n/tr";
import { emitDiagnostic, noFreeCellWarning } from "../diagnostics/editorEvents";
import { setClipboard } from "./clipboard";
import { showStatusNotice } from "./statusNotice";
import { tempId } from "./tempId";
import type { ProfileDocument } from "./useProfileDocument";

/** Widget-level edits on the current page: add, update, move/resize, action bindings, delete, duplicate, move/copy. */
export function useWidgetActions({
  profile, currentPage, selectedIds, selectedWidgets, mutate, mutatePage, setSelectedIds, setError,
}: ProfileDocument) {
  const { t } = useT();

  const addWidget = useCallback(
    (type: WidgetType) => {
      if (!currentPage) return;
      const cell = findFreeCell(1, 1, currentPage.widgets, currentPage.cols, currentPage.rows);
      if (!cell) {
        emitDiagnostic(noFreeCellWarning());
        setError(t("state.noFreeCell"));
        return;
      }
      const widget: Widget = {
        id: tempId("widget"),
        type,
        x: cell.x,
        y: cell.y,
        w: 1,
        h: 1,
        text: defaultTextFor(type, t),
        style: {},
        actions: {},
      };
      mutatePage(currentPage.id, (p) => p.widgets.push(widget), { label: "undo.addWidget" });
      setSelectedIds([widget.id]);
    },
    [currentPage, mutatePage, setError, setSelectedIds, t],
  );

  /** Adds a plugin's custom widget at the size its manifest asks for (as much as fits), with the defaults of its settings. */
  const addPluginWidget = useCallback(
    (info: PluginWidgetInfo) => {
      if (!currentPage) return;
      const w = Math.min(info.size.w, currentPage.cols);
      const h = Math.min(info.size.h, currentPage.rows);
      const cell = findFreeCell(w, h, currentPage.widgets, currentPage.cols, currentPage.rows) ?? findFreeCell(1, 1, currentPage.widgets, currentPage.cols, currentPage.rows);
      if (!cell) {
        emitDiagnostic(noFreeCellWarning());
        setError(t("state.noFreeCell"));
        return;
      }
      const fits = findFreeCell(w, h, currentPage.widgets, currentPage.cols, currentPage.rows) !== null;
      const settings: Record<string, unknown> = {};
      for (const field of info.settings ?? [])
        if (field.default !== undefined && field.default !== null && field.kind !== "Notice" && field.kind !== "Button") settings[field.key] = field.default;
      const widget: Widget = {
        id: tempId("widget"),
        type: "plugin-widget",
        x: cell.x,
        y: cell.y,
        w: fits ? w : 1,
        h: fits ? h : 1,
        text: info.name,
        style: {},
        props: { plugin: info.plugin, widget: info.widget, settings },
        actions: {},
      };
      mutatePage(currentPage.id, (p) => p.widgets.push(widget), { label: "undo.addWidget" });
      setSelectedIds([widget.id]);
    },
    [currentPage, mutatePage, setError, setSelectedIds, t],
  );

  const updateWidget = useCallback(
    (widgetId: string, fn: (widget: Widget) => void, options?: { label?: DictKey; coalesceKey?: string }) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => {
        const widget = p.widgets.find((w) => w.id === widgetId);
        if (widget) fn(widget);
      }, { label: options?.label ?? "undo.editProperty", coalesceKey: options?.coalesceKey });
    },
    [currentPage, mutatePage],
  );

  const setWidgetRect = useCallback(
    (widgetId: string, rect: { x: number; y: number; w: number; h: number }) =>
      updateWidget(widgetId, (w) => Object.assign(w, rect), { label: "undo.moveWidget", coalesceKey: `widget:rect:${widgetId}` }),
    [updateWidget],
  );

  const setWidgetActions = useCallback(
    (widgetId: string, event: string, bindings: ActionBinding[]) =>
      updateWidget(widgetId, (w) => {
        if (bindings.length === 0) delete (w.actions as Record<string, unknown>)[event];
        else (w.actions as Record<string, ActionBinding[]>)[event] = bindings;
      }, { label: "undo.widgetActions" }),
    [updateWidget],
  );

  const deleteWidget = useCallback(
    (widgetId: string) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => w.id !== widgetId); }, { label: "undo.deleteWidget" });
      setSelectedIds((sel) => sel.filter((id) => id !== widgetId));
    },
    [currentPage, mutatePage, setSelectedIds],
  );

  const deleteSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !selectedIds.includes(w.id)); }, { label: "undo.deleteWidgets" });
    setSelectedIds([]);
  }, [currentPage, mutatePage, selectedIds, setSelectedIds]);

  const duplicateSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    const clones = selectedWidgets.map((w) => {
      const cell = findFreeCell(w.w, w.h, currentPage.widgets, currentPage.cols, currentPage.rows);
      return { ...structuredClone(w), id: tempId("widget"), x: cell?.x ?? w.x, y: cell?.y ?? w.y };
    });
    mutatePage(currentPage.id, (p) => p.widgets.push(...clones), { label: "undo.duplicateWidgets" });
    setSelectedIds(clones.map((c) => c.id));
  }, [currentPage, mutatePage, selectedIds, selectedWidgets, setSelectedIds]);

  /** Copies the current selection to the in-memory clipboard, then deletes it — one undo step (edit
   * commands plan, "Clipboard": "Cut = copy, then delete"). */
  const cutSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    setClipboard({ kind: "widgets", widgets: structuredClone(selectedWidgets) });
    mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !selectedIds.includes(w.id)); }, { label: "undo.cutWidgets" });
    setSelectedIds([]);
  }, [currentPage, mutatePage, selectedIds, selectedWidgets, setSelectedIds]);

  /** Copies (or moves) the given widgets onto another page, in this profile or a different one. A
   * cross-profile target is fetched and saved directly (see duplicatePageToProfile — the editor
   * never holds two profiles in memory at once). */
  const moveOrCopyWidgets = useCallback(
    async (widgetIds: string[], targetProfileId: string, targetPageId: string, mode: "move" | "copy") => {
      if (!currentPage || !profile) return;
      const widgets = currentPage.widgets.filter((w) => widgetIds.includes(w.id));
      if (widgets.length === 0) return;
      const clones = widgets.map((w) => ({ ...structuredClone(w), id: tempId("widget") }));

      if (targetProfileId === profile.id) {
        mutate((draft) => {
          const targetPage = draft.pages.find((p) => p.id === targetPageId);
          if (targetPage) targetPage.widgets.push(...clones);
          if (mode === "move") {
            const sourcePage = draft.pages.find((p) => p.id === currentPage.id);
            if (sourcePage) sourcePage.widgets = sourcePage.widgets.filter((w) => !widgetIds.includes(w.id));
          }
          return draft;
        }, { label: "undo.moveOrCopyWidgets" });
        
      } else {
        const target = await api.getProfile(targetProfileId);
        const targetPage = target.pages.find((p) => p.id === targetPageId);
        if (targetPage) {
          targetPage.widgets.push(...clones);
          await api.saveProfile(target);
        }
        if (mode === "move") {
          mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !widgetIds.includes(w.id)); });
        }
      }
      setSelectedIds([]);
    },
    [currentPage, profile, mutate, mutatePage, setSelectedIds],
  );

  /** Ctrl+V of widgets copied with Ctrl+C (see clipboard.ts) — onto the current page, whichever page or
   * profile that is. Reserves a free cell for every pasted widget before placing any of them (so two
   * pasted widgets never land on each other) and refuses the whole paste, with a warning, if even one of
   * them has nowhere to go — a partial paste would be more confusing than none. */
  const pasteWidgets = useCallback(
    (widgets: Widget[]) => {
      if (!currentPage || widgets.length === 0) return;
      let toPlace = widgets;
      let cells = findFreeCellsForBatch(toPlace.map((w) => ({ w: w.w, h: w.h })), currentPage.widgets, currentPage.cols, currentPage.rows);
      // If not everything fits, place as many as will fit (smallest-first has the best chance) rather
      // than refusing the whole paste, and say how many were left out (edit commands plan, "Clipboard").
      if (!cells) {
        for (let n = widgets.length - 1; n > 0 && !cells; n--) {
          toPlace = widgets.slice(0, n);
          cells = findFreeCellsForBatch(toPlace.map((w) => ({ w: w.w, h: w.h })), currentPage.widgets, currentPage.cols, currentPage.rows);
        }
        if (!cells) {
          emitDiagnostic(noFreeCellWarning());
          setError(t("state.noFreeCell"));
          return;
        }
        showStatusNotice("undo.pastePartial", String(toPlace.length), String(widgets.length));
      }
      const clones = toPlace.map((w, i) => ({ ...structuredClone(w), id: tempId("widget"), x: cells![i]!.x, y: cells![i]!.y }));
      mutatePage(currentPage.id, (p) => p.widgets.push(...clones), { label: "undo.pasteWidgets" });
      setSelectedIds(clones.map((c) => c.id));
    },
    [currentPage, mutatePage, setError, setSelectedIds, t],
  );

  return {
    addWidget, addPluginWidget, updateWidget, setWidgetRect, setWidgetActions,
    deleteWidget, deleteSelectedWidgets, duplicateSelectedWidgets, cutSelectedWidgets, moveOrCopyWidgets, pasteWidgets,
  };
}

function defaultTextFor(type: WidgetType, t: ReturnType<typeof useT>["t"]): string {
  switch (type) {
    case "button": return t("widget.type.button");
    case "toggle": return t("widget.type.toggle");
    case "label": return t("widget.type.label");
    case "slider": return t("widget.type.slider");
    case "knob": return t("widget.type.knob");
    case "image": return "";
    case "web": return t("widget.type.web");
    case "plugin-html": return t("widget.type.plugin-html");
    case "plugin-widget": return "";
    default: return "";
  }
}
