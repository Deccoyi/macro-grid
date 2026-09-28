import { createPortal } from "react-dom";
import { toolWindowById } from "./toolWindows";

/** One stable DOM node per tool window id, created lazily and kept for the life of the app. Its
 * content is rendered into it exactly once via a React portal (see PanelHost below); DockPanelFrame
 * (and later the floating/auto-hide frames) just re-parent this same node with a plain DOM append, so
 * moving a panel between dock, float and auto-hide never remounts its content — scroll position,
 * expanded tree nodes and filter text all survive. */
const panelNodes = new Map<string, HTMLDivElement>();

export function getPanelNode(toolWindowId: string): HTMLDivElement {
  let node = panelNodes.get(toolWindowId);
  if (!node) {
    node = document.createElement("div");
    node.style.cssText = "width:100%;height:100%;min-height:0;display:flex;flex-direction:column;overflow:hidden";
    node.dataset.toolWindowId = toolWindowId;
    panelNodes.set(toolWindowId, node);
  }
  return node;
}

/** Mounts the content of every currently open tool window exactly once, each into its own portal
 * node. Render this once near the workspace root — the DockviewReact tree never renders tool window
 * content directly, only frames that attach these nodes (see DockPanelFrame.tsx). */
export function PanelHost({ openIds }: { openIds: string[] }) {
  return (
    <>
      {openIds.map((id) => {
        const def = toolWindowById(id);
        if (!def) return null;
        const Content = def.Content;
        return createPortal(<Content />, getPanelNode(id), id);
      })}
    </>
  );
}
