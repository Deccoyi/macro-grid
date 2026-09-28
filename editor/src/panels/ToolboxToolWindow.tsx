import { useEditorStateContext } from "../state/EditorStateContext";
import { useOpenPages } from "../workspace/OpenPagesContext";
import { WidgetPalette } from "./WidgetPalette";

/** Registry adapter for the Toolbox tool window: disabled while no page tab is open (DocumentArea's empty
 * state) so a widget can never be added to a page nothing on screen represents as open. */
export function ToolboxToolWindow() {
  const state = useEditorStateContext();
  const { openPageIds } = useOpenPages();
  return <WidgetPalette onAdd={state.addWidget} disabled={openPageIds.length === 0} />;
}
