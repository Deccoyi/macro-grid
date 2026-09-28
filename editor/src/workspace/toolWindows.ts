import { FolderTree, ListX, Shapes, Wrench, type LucideIcon } from "lucide-react";
import type { ComponentType } from "react";
import type { DictKey } from "../i18n/tr";
import { ErrorListPanel } from "../panels/ErrorListPanel";
import { HierarchyToolWindow } from "../panels/HierarchyToolWindow";
import { PropertiesToolWindow } from "../panels/PropertiesToolWindow";
import { ToolboxToolWindow } from "../panels/ToolboxToolWindow";

/** One dockable tool window. Adding a new one is: a content component in panels/, one entry here,
 * two i18n keys — see docs/design/docking-workspace.md ("Adding a tool window"). Panels never know
 * how they're docked; DockWorkspace alone decides where a panel is. */
export interface ToolWindowDefinition {
  /** Stable and persisted: never rename one that has shipped. */
  id: string;
  titleKey: DictKey;
  /** 14 px in the View menu and on auto-hide rail tabs. */
  icon: LucideIcon;
  Content: ComponentType;
  defaultPlacement: {
    edge: "left" | "right" | "bottom";
    /** Panels with the same group id start tabbed together. */
    group: string;
    order: number;
    /** px across the edge: width for left/right, height for bottom — except a panel stacked below
     * another one in the same column (toolbox, under hierarchy), where it's that split's height. */
    size: number;
  };
  minSize: { width: number; height: number };
  /** false: starts closed, reachable from View (not used until phase 3's View menu). */
  defaultOpen: boolean;
}

export const TOOL_WINDOWS: ToolWindowDefinition[] = [
  {
    id: "hierarchy",
    titleKey: "panel.hierarchy",
    icon: FolderTree,
    Content: HierarchyToolWindow,
    defaultPlacement: { edge: "left", group: "left-top", order: 0, size: 178 },
    minSize: { width: 160, height: 120 },
    defaultOpen: true,
  },
  {
    id: "toolbox",
    titleKey: "panel.toolbox",
    icon: Shapes,
    Content: ToolboxToolWindow,
    defaultPlacement: { edge: "left", group: "left-bottom", order: 1, size: 285 },
    minSize: { width: 160, height: 100 },
    defaultOpen: true,
  },
  {
    id: "properties",
    titleKey: "panel.properties",
    icon: Wrench,
    Content: PropertiesToolWindow,
    defaultPlacement: { edge: "right", group: "right", order: 0, size: 240 },
    minSize: { width: 240, height: 160 },
    defaultOpen: true,
  },
  {
    id: "errorList",
    titleKey: "panel.errorList",
    icon: ListX,
    Content: ErrorListPanel,
    defaultPlacement: { edge: "bottom", group: "bottom", order: 0, size: 248 },
    minSize: { width: 320, height: 100 },
    defaultOpen: true,
  },
];

export function toolWindowById(id: string): ToolWindowDefinition | undefined {
  return TOOL_WINDOWS.find((t) => t.id === id);
}
