import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { DockviewReact, type DockviewApi, type DockviewReadyEvent } from "dockview-react";
import "dockview-react/dist/styles/dockview.css";
import "./dock-theme.css";
import type { DeviceSize } from "../grid/DevicePreviewFrame";
import { AutoHideFlyout } from "./AutoHideFlyout";
import { AutoHideRail } from "./AutoHideRail";
import { DockGroupHeader } from "./DockGroupHeader";
import { DockPanelFrame } from "./DockPanelFrame";
import { DocumentArea } from "./DocumentArea";
import { OpenPagesProvider } from "./OpenPagesContext";
import { PanelHost } from "./PanelHost";
import { TOOL_WINDOWS } from "./toolWindows";
import { useWorkspace } from "./WorkspaceContext";

const DeviceSizeContext = createContext<DeviceSize | null>(null);

function DocumentAreaFrame() {
  return <DocumentArea deviceSize={useContext(DeviceSizeContext)} />;
}

const DOCKVIEW_COMPONENTS = { toolWindow: DockPanelFrame, document: DocumentAreaFrame };

/** Replaces App.tsx's fixed 3-column grid: renders the dockview instance and the auto-hide rails/flyout.
 * The open/closed/auto-hidden bookkeeping itself lives in WorkspaceContext (a sibling of MenuBar's View
 * menu needs to read and drive the same state), not here. No layout persistence yet (phase 2). */
export function DockWorkspace({ deviceSize }: { deviceSize: DeviceSize | null }) {
  const workspace = useWorkspace();
  const dockviewContainerRef = useRef<HTMLDivElement | null>(null);
  const [dockviewApi, setDockviewApi] = useState<DockviewApi | null>(null);

  // dockview's own resize handling (whatever triggers it — a ResizeObserver, most likely) doesn't
  // reliably catch every size change of ITS container when that change comes from a sibling in the same
  // flex row appearing or disappearing (the auto-hide rails): the panels reflow correctly, but the
  // column-divider sashes keep their stale size and start poking out past the dockview area's new edge.
  // Telling dockview its exact current size on every resize, from our own observer on its actual
  // container, sidesteps whatever dockview's internal detection is missing.
  useEffect(() => {
    const el = dockviewContainerRef.current;
    if (!el || !dockviewApi) return;
    const observer = new ResizeObserver(() => {
      const rect = el.getBoundingClientRect();
      dockviewApi.layout(rect.width, rect.height);
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [dockviewApi]);

  const onReady = useCallback(
    (event: DockviewReadyEvent) => {
      workspace.registerApi(event.api);
      setDockviewApi(event.api);
      workspace.buildDefaultLayout(event.api);
    },
    [workspace],
  );

  const byEdge = (edge: "left" | "right" | "bottom") =>
    workspace.autoHiddenIds.filter((id) => TOOL_WINDOWS.find((tw) => tw.id === id)?.defaultPlacement.edge === edge);

  const openFlyoutEdge = workspace.openFlyoutId
    ? TOOL_WINDOWS.find((tw) => tw.id === workspace.openFlyoutId)?.defaultPlacement.edge ?? null
    : null;

  return (
    <OpenPagesProvider>
    <DeviceSizeContext.Provider value={deviceSize}>
      <div style={{ flex: "1 1 auto", minHeight: 0, minWidth: 0, display: "flex", flexDirection: "row" }}>
        <AutoHideRail edge="left" ids={byEdge("left")} openId={workspace.openFlyoutId} onToggle={(id) => workspace.setOpenFlyoutId(workspace.openFlyoutId === id ? null : id)} />
        <div style={{ position: "relative", flex: "1 1 auto", minHeight: 0, minWidth: 0, display: "flex", flexDirection: "column" }}>
          <div ref={dockviewContainerRef} className="dv-theme-macro" style={{ flex: "1 1 auto", minHeight: 0 }}>
            <DockviewReact components={DOCKVIEW_COMPONENTS} defaultTabComponent={DockGroupHeader} onReady={onReady} />
          </div>
          <AutoHideRail edge="bottom" ids={byEdge("bottom")} openId={workspace.openFlyoutId} onToggle={(id) => workspace.setOpenFlyoutId(workspace.openFlyoutId === id ? null : id)} />
          {workspace.openFlyoutId && openFlyoutEdge && (
            <AutoHideFlyout
              id={workspace.openFlyoutId}
              edge={openFlyoutEdge}
              onClose={() => workspace.setOpenFlyoutId(null)}
              onPin={() => workspace.pin(workspace.openFlyoutId!)}
            />
          )}
        </div>
        <AutoHideRail edge="right" ids={byEdge("right")} openId={workspace.openFlyoutId} onToggle={(id) => workspace.setOpenFlyoutId(workspace.openFlyoutId === id ? null : id)} />
      </div>
      <PanelHost openIds={workspace.openIds} />
    </DeviceSizeContext.Provider>
    </OpenPagesProvider>
  );
}
