import { browserStorageBackend, WidgetDataStore } from "@macro/renderer";

/** What preview widgets keep in the editor, apart from what the phones keep. The host and the inspector's "Clear widget data" share it. */
export const editorWidgetData = new WidgetDataStore(browserStorageBackend(), "macro-grid.editor.widgetData.");
