import { useState } from "react";
import { Plus, X } from "lucide-react";
import { commandItem } from "../commands/commandItem";
import { useCommands } from "../commands/CommandsContext";
import type { Command } from "../commands/types";
import type { DeviceSize } from "../grid/DevicePreviewFrame";
import { DevicePreviewFrame } from "../grid/DevicePreviewFrame";
import { EditorCanvas } from "../grid/EditorCanvas";
import { useT } from "../i18n/I18nContext";
import { ContextMenu, type ContextMenuEntry } from "../panels/ContextMenu";
import { useEditorStateContext } from "../state/EditorStateContext";
import { clearPluginTreeSelection } from "../state/pluginTreeSelectionStore";
import { useOpenPages } from "./OpenPagesContext";
import { useWorkspaceUi } from "./WorkspaceUiContext";

/** The document area: page tabs — every page the user has opened from Hierarchy stays open as a tab
 * (default-layout.html) — plus the existing device preview and canvas, moved here from App.tsx
 * unchanged. Fixed in the layout: it can't be closed, floated, auto-hidden or tabbed with a tool window
 * (docking-workspace.md "Document area"). Open tabs live in OpenPagesContext (a sibling, Toolbox,
 * needs to see whether any page tab is open too) — persisting them per profile (docking-workspace.md
 * phase 5, "openPageIds") waits for phase 2's layout file. */
export function DocumentArea({ deviceSize }: { deviceSize: DeviceSize | null }) {
  const { t } = useT();
  const state = useEditorStateContext();
  const { get } = useCommands();
  const { openMoveCopyForWidgets, clearProfileProperties } = useWorkspaceUi();
  const { openPageIds, closeTab } = useOpenPages();
  const [widgetMenu, setWidgetMenu] = useState<{ x: number; y: number } | null>(null);
  const { currentPage, currentPageId, profile } = state;

  if (!currentPage || !profile) return null;

  const openPages = openPageIds.map((id) => profile.pages.find((p) => p.id === id)).filter((p): p is NonNullable<typeof p> => !!p);

  return (
    <div style={{ width: "100%", height: "100%", display: "flex", flexDirection: "column", minHeight: 0, minWidth: 0, background: "var(--ms-bg-canvas)" }}>
      <div style={{ height: 28, flex: "0 0 auto", display: "flex", alignItems: "stretch", background: "var(--ms-bg-surface)", borderBottom: "1px solid var(--ms-border)", overflow: "auto" }}>
        {openPages.map((page) => {
          const active = page.id === currentPageId;
          return (
            <div
              key={page.id}
              onClick={() => { if (!active) { state.setCurrentPageId(page.id); state.setSelectedIds([]); } }}
              onMouseDown={(e) => { if (e.button === 1) { e.preventDefault(); closeTab(page.id); } }} // middle-click closes
              style={{
                padding: "0 10px 0 16px",
                display: "flex",
                alignItems: "center",
                gap: 8,
                background: active ? "var(--ms-bg-canvas)" : "transparent",
                color: active ? "var(--ms-text-primary)" : "var(--ms-text-secondary)",
                borderBottom: active ? "2px solid var(--ms-accent)" : "2px solid transparent",
                borderRight: "1px solid var(--ms-border)",
                fontSize: 12.5,
                whiteSpace: "nowrap",
                cursor: "pointer",
                flex: "0 0 auto",
              }}
            >
              {page.name}
              {openPages.length > 1 && (
                <button
                  type="button"
                  aria-label={t("panel.close")}
                  onClick={(e) => { e.stopPropagation(); closeTab(page.id); }}
                  style={{ display: "flex", color: "inherit", opacity: 0.7, background: "transparent", border: "none", cursor: "pointer", padding: 0 }}
                >
                  <X size={11} strokeWidth={2} />
                </button>
              )}
            </div>
          );
        })}
        <button
          type="button"
          aria-label={t("page.add")}
          title={t("page.add")}
          onClick={() => state.addPage()}
          style={{ width: 28, flex: "0 0 auto", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--ms-text-secondary)", background: "transparent", border: "none", cursor: "pointer" }}
        >
          <Plus size={13} strokeWidth={2} />
        </button>
      </div>

      <div style={{ flex: "1 1 auto", padding: 16, minWidth: 0, minHeight: 0 }}>
        {openPages.length === 0 ? (
          <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center", color: "var(--ms-text-secondary)", fontSize: 12.5 }}>
            {t("document.noOpenPage")}
          </div>
        ) : (
          <div
            style={{ width: "100%", height: "100%", border: "1px solid var(--ms-border)", borderRadius: 4, background: "var(--ms-bg-surface)" }}
            // Any click on the canvas makes the canvas the active object again. Doing it here rather than in onSelect covers a click on a
            // widget that is already selected (onSelect is not called for it) and a click on the empty page.
            onPointerDownCapture={() => { clearProfileProperties(); clearPluginTreeSelection(); }}
          >
            <DevicePreviewFrame size={deviceSize}>
              <EditorCanvas
                page={currentPage}
                selectedIds={state.selectedIds}
                onSelect={(ids) => { state.setSelectedIds(ids); if (ids.length > 0) { clearProfileProperties(); clearPluginTreeSelection(); } }}
                onToggleSelect={state.toggleSelected}
                onRectChange={state.setWidgetRect}
                onContextMenu={(x, y) => setWidgetMenu({ x, y })}
                variables={state.variables}
              />
            </DevicePreviewFrame>
          </div>
        )}
      </div>

      {widgetMenu && (
        <ContextMenu
          x={widgetMenu.x}
          y={widgetMenu.y}
          onClose={() => setWidgetMenu(null)}
          items={widgetContextItems(t, get, openMoveCopyForWidgets)}
        />
      )}
    </div>
  );
}

type T = ReturnType<typeof useT>["t"];

/** Undo/Redo, Cut/Copy/Paste/Duplicate/Delete plus the existing "Move/Copy to..." — the same commands as
 * the Edit menu and the shortcuts (docs/design/editor-edit-commands.md, "Menus"). */
function widgetContextItems(t: T, get: (id: string) => Command | undefined, onMoveCopy: () => void): ContextMenuEntry[] {
  const cmd = (id: string) => commandItem(get(id)!, t);
  return [
    cmd("edit.undo"),
    cmd("edit.redo"),
    { divider: true },
    cmd("edit.cut"),
    cmd("edit.copy"),
    cmd("edit.paste"),
    cmd("edit.duplicate"),
    cmd("edit.delete"),
    { label: t("ctx.widget.moveCopy", t("ctx.widget.labelSingle")), onSelect: onMoveCopy },
  ];
}
