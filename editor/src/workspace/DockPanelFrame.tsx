import { useEffect, useRef } from "react";
import type { IDockviewPanelProps } from "dockview-react";
import { getPanelNode } from "./PanelHost";

export interface ToolWindowPanelParams {
  toolWindowId: string;
}

/** The library's panel component for tool windows: it renders no content of its own, it only attaches
 * that tool window's stable portal node from PanelHost (see PanelHost.tsx for why). */
export function DockPanelFrame({ params }: IDockviewPanelProps<ToolWindowPanelParams>) {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    const node = getPanelNode(params.toolWindowId);
    container?.appendChild(node);
  }, [params.toolWindowId]);

  return <div ref={containerRef} style={{ width: "100%", height: "100%", minHeight: 0, display: "flex", flexDirection: "column", overflow: "hidden" }} />;
}
