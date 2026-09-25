import { useCallback } from "react";
import type { ActionBinding, Widget, WidgetType } from "@macro/renderer";
import { api } from "../api/client";
import { findFreeCell } from "../grid/collision";
import { useT } from "../i18n/I18nContext";
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
      mutatePage(currentPage.id, (p) => p.widgets.push(widget));
      setSelectedIds([widget.id]);
    },
    [currentPage, mutatePage, setError, setSelectedIds, t],
  );

  const updateWidget = useCallback(
    (widgetId: string, fn: (widget: Widget) => void) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => {
        const widget = p.widgets.find((w) => w.id === widgetId);
        if (widget) fn(widget);
      });
    },
    [currentPage, mutatePage],
  );

  const setWidgetRect = useCallback(
    (widgetId: string, rect: { x: number; y: number; w: number; h: number }) =>
      updateWidget(widgetId, (w) => Object.assign(w, rect)),
    [updateWidget],
  );

  const setWidgetActions = useCallback(
    (widgetId: string, event: string, bindings: ActionBinding[]) =>
      updateWidget(widgetId, (w) => {
        if (bindings.length === 0) delete (w.actions as Record<string, unknown>)[event];
        else (w.actions as Record<string, ActionBinding[]>)[event] = bindings;
      }),
    [updateWidget],
  );

  const deleteWidget = useCallback(
    (widgetId: string) => {
      if (!currentPage) return;
      mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => w.id !== widgetId); });
      setSelectedIds((sel) => sel.filter((id) => id !== widgetId));
    },
    [currentPage, mutatePage, setSelectedIds],
  );

  const deleteSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    mutatePage(currentPage.id, (p) => { p.widgets = p.widgets.filter((w) => !selectedIds.includes(w.id)); });
    setSelectedIds([]);
  }, [currentPage, mutatePage, selectedIds, setSelectedIds]);

  const duplicateSelectedWidgets = useCallback(() => {
    if (!currentPage || selectedIds.length === 0) return;
    const clones = selectedWidgets.map((w) => {
      const cell = findFreeCell(w.w, w.h, currentPage.widgets, currentPage.cols, currentPage.rows);
      return { ...structuredClone(w), id: tempId("widget"), x: cell?.x ?? w.x, y: cell?.y ?? w.y };
    });
    mutatePage(currentPage.id, (p) => p.widgets.push(...clones));
    setSelectedIds(clones.map((c) => c.id));
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
        });
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

  return {
    addWidget, updateWidget, setWidgetRect, setWidgetActions,
    deleteWidget, deleteSelectedWidgets, duplicateSelectedWidgets, moveOrCopyWidgets,
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
    default: return "";
  }
}
