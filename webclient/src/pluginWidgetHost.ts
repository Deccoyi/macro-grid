import { browserStorageBackend, PluginWidgetError, WidgetDataStore, type PluginWidgetHost, type PluginWidgetListener } from "@macro/renderer";

/** What the host needs from the connection: sending a message. */
export interface WidgetTransport {
  send(type: string, data?: unknown): void;
}

interface Pending {
  resolve: (data: unknown) => void;
  reject: (error: Error) => void;
}

/**
 * The browser deck's side of plugin widgets: it turns what a widget asks into `plugin.widget.*` messages, gives back the answers, pushes what the
 * server sends to the right widget, and fetches a widget's script and images as assets (`asset.get`). It remembers what each widget subscribed to,
 * so a new connection can ask for it again.
 */
export class DeckPluginWidgetHost implements PluginWidgetHost {
  readonly mode = "run" as const;
  readonly locale: string = typeof navigator !== "undefined" ? navigator.language : "en";
  readonly theme = "dark" as const;
  /** What widgets keep in this browser; it never leaves it. */
  readonly storage = new WidgetDataStore(browserStorageBackend(), "macro-grid.widgetData.");

  private nextId = 1;
  private readonly pending = new Map<number, Pending>();
  private readonly assets = new Map<string, string>();
  private readonly assetWaiters = new Map<string, Pending[]>();
  private readonly listeners = new Map<string, PluginWidgetListener>();
  private readonly subscriptions = new Map<string, string[]>();

  constructor(
    private readonly transport: WidgetTransport,
    private readonly currentPageId: () => string | undefined,
    private readonly onError: (widgetId: string, message: string) => void = () => undefined,
  ) {}

  loadAsset(reference: string): Promise<string> {
    const hash = reference.startsWith("asset:") ? reference.slice("asset:".length) : reference;
    const known = this.assets.get(hash);
    if (known !== undefined) return Promise.resolve(known);
    return new Promise<string>((resolve, reject) => {
      const waiter: Pending = { resolve: resolve as (d: unknown) => void, reject };
      const waiters = this.assetWaiters.get(hash);
      if (waiters) waiters.push(waiter);
      else {
        this.assetWaiters.set(hash, [waiter]);
        this.transport.send("asset.get", { hashes: [hash] });
      }
      setTimeout(() => reject(new Error("The asset did not arrive in time")), 15000);
    });
  }

  request(widgetId: string, data: unknown): Promise<unknown> {
    return this.call(widgetId, "request", data, false);
  }

  run(widgetId: string, action: string, settings: unknown, userGesture: boolean): Promise<void> {
    return this.call(widgetId, "run", { action, settings }, userGesture).then(() => undefined);
  }

  subscribe(widgetId: string, variables: string[]): void {
    this.subscriptions.set(widgetId, variables);
    this.send(widgetId, "subscribe", { variables });
  }

  ready(widgetId: string): void {
    this.send(widgetId, "ready");
  }

  reportError(widgetId: string, message: string): void {
    const pageId = this.currentPageId();
    if (pageId) this.transport.send("plugin.widget.error", { pageId, widgetId, message });
    this.onError(widgetId, message);
  }

  listen(widgetId: string, listener: PluginWidgetListener): () => void {
    this.listeners.set(widgetId, listener);
    return () => {
      if (this.listeners.get(widgetId) === listener) this.listeners.delete(widgetId);
      this.subscriptions.delete(widgetId);
    };
  }

  // ---- what the connection gives us ----

  onAsset(hash: string, data: string | null): void {
    const waiters = this.assetWaiters.get(hash) ?? [];
    this.assetWaiters.delete(hash);
    if (data !== null) this.assets.set(hash, data);
    for (const w of waiters) {
      if (data !== null) w.resolve(data);
      else w.reject(new Error("The server no longer has this asset"));
    }
  }

  onMessage(type: string, data: unknown): void {
    const message = data as { widgetId?: string; id?: number; ok?: boolean; data?: unknown; error?: string; message?: string; values?: Record<string, unknown>; name?: string };
    if (type === "plugin.widget.reply" && typeof message.id === "number") {
      const p = this.pending.get(message.id);
      if (!p) return;
      this.pending.delete(message.id);
      if (message.ok) p.resolve(message.data);
      else p.reject(new PluginWidgetError(message.error ?? "failed", message.message ?? undefined));
    } else if (type === "plugin.widget.vars" && message.widgetId && message.values) this.listeners.get(message.widgetId)?.vars(message.values);
    else if (type === "plugin.widget.event" && message.widgetId && message.name) this.listeners.get(message.widgetId)?.event(message.name, message.data);
  }

  /** A new connection knows nothing of what was asked: ask again for every running widget's variables and retained events. */
  onReconnected(): void {
    for (const p of this.pending.values()) p.reject(new PluginWidgetError("plugin_unavailable", "The connection was lost"));
    this.pending.clear();
    for (const [widgetId, variables] of this.subscriptions) {
      this.send(widgetId, "subscribe", { variables });
      this.send(widgetId, "ready");
    }
  }

  private call(widgetId: string, kind: "request" | "run", data: unknown, userGesture: boolean): Promise<unknown> {
    const pageId = this.currentPageId();
    if (!pageId) return Promise.reject(new PluginWidgetError("plugin_unavailable"));
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      this.transport.send("plugin.widget.request", { pageId, widgetId, kind, id, data, userGesture });
      setTimeout(() => {
        if (this.pending.delete(id)) reject(new PluginWidgetError("timeout"));
      }, 15000);
    });
  }

  private send(widgetId: string, kind: string, data?: unknown): void {
    const pageId = this.currentPageId();
    if (pageId) this.transport.send("plugin.widget.request", { pageId, widgetId, kind, data });
  }
}
