import { PluginWidgetError, type PluginWidgetHost, type PluginWidgetListener } from "@macro/renderer";
import { api } from "../api/client";
import type { PluginWidgetInfo } from "../api/types";
import { editorWidgetData } from "./widgetData";

/** What the preview knows about one placed widget: which plugin widget it is and its settings (bound variables included). */
export interface PreviewWidget {
  info: PluginWidgetInfo;
  settings: Record<string, unknown>;
}

/**
 * The editor's side of plugin widgets: the same worker as on a device, with `mode: "edit"`. Variables come from the editor's own polled snapshot
 * (only the widget's own plugin's and the ones bound in its settings, as on a device), a request goes to the plugin over HTTP with the device id
 * "editor", and a run is refused, so the preview can never press anything on the PC.
 */
export class EditorPluginWidgetHost implements PluginWidgetHost {
  readonly mode = "edit" as const;
  readonly theme = "dark" as const;
  readonly locale: string;
  /** What preview widgets keep, apart from what the phones keep. "Clear widget data" in the inspector empties it. */
  readonly storage = editorWidgetData;

  private variables: Record<string, unknown> = {};
  private readonly widgets = new Map<string, PreviewWidget>();
  private readonly listeners = new Map<string, PluginWidgetListener>();
  private readonly subscriptions = new Map<string, string[]>();
  private readonly sent = new Map<string, Map<string, string>>();

  constructor(
    locale: string,
    private readonly onError: (widgetId: string, message: string) => void,
  ) {
    this.locale = locale;
  }

  /** Tells the host which plugin widget a placed widget is (called on every render of the canvas). */
  register(widgetId: string, widget: PreviewWidget): void {
    this.widgets.set(widgetId, widget);
  }

  /** Feeds the latest variable snapshot; subscribed widgets get what changed. */
  setVariables(variables: Record<string, unknown>): void {
    this.variables = variables;
    for (const widgetId of this.subscriptions.keys()) this.push(widgetId);
  }

  loadAsset(reference: string): Promise<string> {
    return api.pluginWidgetAsset(reference.startsWith("asset:") ? reference.slice("asset:".length) : reference);
  }

  async request(widgetId: string, data: unknown): Promise<unknown> {
    const widget = this.widgets.get(widgetId);
    if (!widget) throw new PluginWidgetError("plugin_unavailable");
    try {
      return (await api.pluginWidgetRequest(widget.info.plugin, widget.info.widget, widget.settings, data)).data;
    } catch (e) {
      const message = e instanceof Error ? e.message : "failed";
      const head = message.split(":")[0] ?? "";
      throw new PluginWidgetError(/^[a-z_]+$/.test(head) ? head : "failed", message);
    }
  }

  async run(): Promise<void> {
    throw new PluginWidgetError("editor", "A widget cannot run actions in the editor's preview.");
  }

  subscribe(widgetId: string, variables: string[]): void {
    this.subscriptions.set(widgetId, variables);
    this.sent.set(widgetId, new Map());
    this.push(widgetId);
  }

  ready(): void {}

  reportError(widgetId: string, message: string): void {
    this.onError(widgetId, message);
  }

  listen(widgetId: string, listener: PluginWidgetListener): () => void {
    this.listeners.set(widgetId, listener);
    return () => {
      if (this.listeners.get(widgetId) === listener) this.listeners.delete(widgetId);
      this.subscriptions.delete(widgetId);
      this.sent.delete(widgetId);
    };
  }

  private allowed(widget: PreviewWidget, name: string): boolean {
    if (name.startsWith(widget.info.plugin + ".")) return true;
    return (widget.info.settings ?? []).some((f) => f.kind === "Variable" && widget.settings[f.key] === name);
  }

  private push(widgetId: string): void {
    const widget = this.widgets.get(widgetId);
    const listener = this.listeners.get(widgetId);
    const names = this.subscriptions.get(widgetId);
    const sent = this.sent.get(widgetId);
    if (!widget || !listener || !names || !sent) return;
    const changed: Record<string, unknown> = {};
    for (const name of names) {
      if (!this.allowed(widget, name) || !(name in this.variables)) continue;
      const value = this.variables[name];
      const text = JSON.stringify(value ?? null);
      if (sent.get(name) === text) continue;
      sent.set(name, text);
      changed[name] = value ?? null;
    }
    if (Object.keys(changed).length > 0) listener.vars(changed);
  }
}
