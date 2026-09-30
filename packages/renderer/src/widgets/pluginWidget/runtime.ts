import { assembleWorkerScript } from "./bootstrap";
import { startWidgetFrame, type FrameStarter, type WidgetFrame } from "./launcher";
import {
  PLUGIN_WIDGET_LIMITS as L,
  PluginWidgetError,
  type PluginWidgetHost,
  type PluginWidgetRuntimeInfo,
  type PluginWidgetState,
  type PluginWidgetStopReason,
} from "./types";

/** Timers and clock the runtime uses, replaceable in tests. */
export interface RuntimeClock {
  now(): number;
  setInterval(fn: () => void, ms: number): unknown;
  clearInterval(handle: unknown): void;
  setTimeout(fn: () => void, ms: number): unknown;
  clearTimeout(handle: unknown): void;
}

const realClock: RuntimeClock = {
  now: () => performance.now(),
  setInterval: (fn, ms) => setInterval(fn, ms),
  clearInterval: (h) => clearInterval(h as ReturnType<typeof setInterval>),
  setTimeout: (fn, ms) => setTimeout(fn, ms),
  clearTimeout: (h) => clearTimeout(h as ReturnType<typeof setTimeout>),
};

export interface PluginWidgetRuntimeOptions {
  /** Makes the sandboxed frame with the canvas and the worker; defaults to the real one. Tests use a fake. */
  frameStarter?: FrameStarter;
  clock?: RuntimeClock;
}

export interface MountOptions {
  widgetId: string;
  container: HTMLElement;
  info: PluginWidgetRuntimeInfo;
  settings: Record<string, unknown>;
  host: PluginWidgetHost;
  width: number;
  height: number;
  dpr: number;
  onState?: (state: PluginWidgetState) => void;
}

type PortMessage = { v?: number; type?: string; id?: number; data?: Record<string, unknown> };

/**
 * Starts and watches the workers of plugin widgets (docs/design/plugin-widgets.md). One runtime per app: it counts live workers, splits the frame
 * budget and gives each widget an instance that bridges its worker to the host (the server).
 */
export class PluginWidgetRuntime {
  private readonly clock: RuntimeClock;
  private readonly frameStarter: FrameStarter;
  private readonly live = new Set<PluginWidgetInstance>();
  private seq = 0;

  constructor(options: PluginWidgetRuntimeOptions = {}) {
    this.clock = options.clock ?? realClock;
    this.frameStarter = options.frameStarter ?? startWidgetFrame;
  }

  /** Mounts one widget. It never throws: a widget that cannot start ends up stopped with a reason. */
  mount(options: MountOptions): PluginWidgetInstance {
    const instance = new PluginWidgetInstance(this, options, `pw${++this.seq}`);
    instance.begin();
    return instance;
  }

  /** How many workers are live (starting, running or paused). */
  get liveCount(): number {
    return this.live.size;
  }

  /** Stops every worker and removes every frame. */
  dispose(): void {
    for (const instance of [...this.live]) instance.dispose();
  }

  // ---- used by instances ----

  /** @internal */
  get time(): RuntimeClock {
    return this.clock;
  }

  /** @internal */
  startFrame(id: string, container: HTMLElement, script: string, port: MessagePort): Promise<WidgetFrame> {
    return this.frameStarter(id, container, script, port);
  }

  /** @internal Returns a reason when this widget may not start now. */
  admit(instance: PluginWidgetInstance): PluginWidgetStopReason | null {
    if (this.live.size >= L.maxLive) return "tooMany";
    if (!instance.verified && [...this.live].filter((i) => !i.verified).length >= L.maxLiveUnverified) return "tooMany";
    this.live.add(instance);
    this.rebalance();
    return null;
  }

  /** @internal */
  release(instance: PluginWidgetInstance): void {
    if (this.live.delete(instance)) this.rebalance();
  }

  /** Gives each live widget its share of the frame budget: the sum of the frame-rate caps never goes above the budget. */
  private rebalance(): void {
    const total = [...this.live].reduce((sum, i) => sum + i.fpsCap, 0);
    const scale = total > L.frameBudget ? L.frameBudget / total : 1;
    for (const instance of this.live) instance.setFpsScale(scale);
  }
}

/** One placed widget on one device: its canvas, its worker, and the bridge between the two and the host. */
export class PluginWidgetInstance {
  private state: PluginWidgetState = { kind: "starting" };
  private port: MessagePort | null = null;
  private frame: WidgetFrame | null = null;
  private id: string;
  private disposed = false;
  private booted = false;
  private paused = false;
  private lastPong = 0;
  private watchdog: unknown = null;
  private startTimer: unknown = null;
  private restartTimer: unknown = null;
  private restarted = false;
  private stopListening: (() => void) | null = null;
  private busyStrikes = 0;
  private fpsScale = 1;
  private gestureUntil = 0;
  private requestStamps: number[] = [];
  private errorStamps: number[] = [];
  private assetCache = new Map<string, Promise<{ bytes: ArrayBuffer; mime: string }>>();
  private options: MountOptions;

  constructor(
    private readonly runtime: PluginWidgetRuntime,
    options: MountOptions,
    private readonly baseId: string,
  ) {
    this.options = options;
    this.id = baseId;
  }

  get verified(): boolean {
    return this.options.info.verified === true;
  }

  get fpsCap(): number {
    return Math.max(1, Math.min(60, this.options.info.fps ?? 15));
  }

  get current(): PluginWidgetState {
    return this.state;
  }

  /** @internal */
  begin(): void {
    const { info } = this.options;
    if (!info.code) return this.stop("failed");
    const reason = this.runtime.admit(this);
    if (reason) return this.stop(reason);
    this.setState({ kind: "starting" });
    void this.launch();
  }

  private async launch(): Promise<void> {
    const { host, info } = this.options;
    try {
      const code = await host.loadAsset(info.code!);
      if (this.disposed) return;
      const channel = new MessageChannel();
      this.port = channel.port1;
      channel.port1.onmessage = (e: MessageEvent) => this.onPortMessage(e.data as PortMessage);
      this.id = `${this.baseId}.${Date.now().toString(36)}`;
      this.booted = false;
      this.startTimer = this.runtime.time.setTimeout(() => this.stop("startTimeout"), L.startTimeoutMs);
      const frame = await this.runtime.startFrame(this.id, this.options.container, assembleWorkerScript(code), channel.port2);
      // Stopped or disposed while the frame was starting: it must not stay behind.
      if (this.disposed || this.port !== channel.port1) frame.terminate();
      else this.frame = frame;
    } catch (e) {
      if (this.disposed) return;
      host.reportError(this.options.widgetId, `The widget could not start: ${(e as Error).message}`);
      this.stop("failed");
    }
  }

  // ---- what the worker says ----

  private onPortMessage(m: PortMessage): void {
    if (this.disposed || !m || m.v !== 1 || typeof m.type !== "string") return;
    const d = m.data ?? {};
    switch (m.type) {
      case "booted":
        this.onBooted();
        break;
      case "pong":
        this.lastPong = this.runtime.time.now();
        break;
      case "request":
        if (typeof m.id === "number") this.onRequest(m.id, d, false);
        break;
      case "run":
        if (typeof m.id === "number") this.onRequest(m.id, d, true);
        break;
      case "subscribe":
        this.onSubscribe(d);
        break;
      case "error":
        this.onWorkerError(String(d.message ?? ""));
        break;
      case "load":
        this.onLoad(Number(d.busy) || 0);
        break;
      case "asset":
        void this.onAsset(String(d.name ?? ""));
        break;
    }
  }

  private onBooted(): void {
    if (this.booted) return;
    this.booted = true;
    this.runtime.time.clearTimeout(this.startTimer);
    this.startTimer = null;
    this.lastPong = this.runtime.time.now();
    this.watchdog = this.runtime.time.setInterval(() => this.checkWatchdog(), L.pingEveryMs);
    const { host, widgetId, info, settings } = this.options;
    this.stopListening = host.listen(widgetId, {
      vars: (values) => this.post("vars", { values }),
      event: (name, data) => this.post("event", { name, data }),
    });
    this.post("init", {
      width: this.options.width,
      height: this.options.height,
      dpr: Math.min(L.maxDpr, this.options.dpr),
      fps: this.effectiveFps(),
      settings,
      bindings: bindingsOf(settings, info.variables),
      mode: host.mode,
      locale: host.locale ?? "en",
      theme: host.theme ?? "dark",
      options: info.options ?? [],
    });
    this.setState(this.paused ? { kind: "paused" } : { kind: "running" });
    if (this.paused) this.post("visibility", { paused: true });
    host.ready(widgetId);
  }

  private checkWatchdog(): void {
    if (this.disposed) return;
    if (this.runtime.time.now() - this.lastPong > L.frozenAfterMs) {
      this.options.host.reportError(this.options.widgetId, "The widget stopped answering and was stopped.");
      this.stop("frozen");
      return;
    }
    this.post("ping");
  }

  private onLoad(busy: number): void {
    const limit = this.verified ? L.busyLimit : L.busyLimitUnverified;
    if (busy <= limit) {
      this.busyStrikes = 0;
      return;
    }
    // Too much time in frame callbacks: halve its frame rate once, then stop it.
    this.busyStrikes++;
    if (this.busyStrikes === 1) {
      this.fpsScale = Math.max(0.1, this.fpsScale / 2);
      this.post("fps", { fps: this.effectiveFps() });
    } else {
      this.options.host.reportError(this.options.widgetId, "The widget used too much processor time and was stopped.");
      this.stop("tooBusy");
    }
  }

  private onRequest(id: number, d: Record<string, unknown>, isRun: boolean): void {
    const { host, widgetId } = this.options;
    const reply = (ok: boolean, data?: unknown, error?: string, message?: string) => this.post("reply", { ok, data, error, message }, id);
    if (this.jsonLength(d) > L.maxRequestChars) return reply(false, undefined, "too_large");
    const now = this.runtime.time.now();
    this.requestStamps = this.requestStamps.filter((t) => now - t < 1000);
    if (this.requestStamps.length >= L.requestsPerSecond) return reply(false, undefined, "rate_limited");
    this.requestStamps.push(now);

    if (isRun) {
      // In the editor's preview a run is refused: the preview must never press anything on the PC.
      if (host.mode === "edit") return reply(false, undefined, "editor");
      // A real touch grants one run, once, only to the widget that was touched.
      const gesture = now < this.gestureUntil;
      this.gestureUntil = 0;
      host.run(widgetId, String(d.action ?? ""), d.settings ?? {}, gesture).then(
        () => reply(true),
        (e) => reply(false, undefined, codeOf(e), messageOf(e)),
      );
    } else {
      host.request(widgetId, d.data ?? d).then(
        (data) => reply(true, data),
        (e) => reply(false, undefined, codeOf(e), messageOf(e)),
      );
    }
  }

  private onSubscribe(d: Record<string, unknown>): void {
    const names = Array.isArray(d.variables) ? d.variables.filter((n): n is string => typeof n === "string").slice(0, L.maxSubscribed) : [];
    this.options.host.subscribe(this.options.widgetId, names);
  }

  private onWorkerError(message: string): void {
    const now = this.runtime.time.now();
    this.errorStamps = this.errorStamps.filter((t) => now - t < 10000);
    if (this.errorStamps.length >= L.errorsPer10s) return;
    this.errorStamps.push(now);
    this.options.host.reportError(this.options.widgetId, message.slice(0, 500));
  }

  private async onAsset(name: string): Promise<void> {
    const reference = this.options.info.assets?.[name];
    if (!reference) return this.post("asset", { name, ok: false });
    try {
      let pending = this.assetCache.get(name);
      if (!pending) {
        pending = this.options.host.loadAsset(reference).then(dataUriToBytes);
        this.assetCache.set(name, pending);
      }
      const { bytes, mime } = await pending;
      // A copy per widget, transferred: the cached bytes stay ours.
      const copy = bytes.slice(0);
      this.post("asset", { name, ok: true, bytes: copy, mime }, undefined, [copy]);
    } catch {
      this.post("asset", { name, ok: false });
    }
  }

  // ---- what the renderer says to the worker ----

  private post(type: string, data?: unknown, id?: number, transfer: Transferable[] = []): void {
    const message: PortMessage = { v: 1, type };
    if (data !== undefined) message.data = data as Record<string, unknown>;
    if (id !== undefined) message.id = id;
    this.port?.postMessage(message, transfer);
  }

  private effectiveFps(): number {
    return Math.max(1, Math.round(this.fpsCap * this.fpsScale));
  }

  /** @internal */
  setFpsScale(scale: number): void {
    // The budget scale multiplies with a busy-strike reduction; keep the smaller of the two effects.
    const strikes = this.busyStrikes > 0 ? 0.5 : 1;
    this.fpsScale = Math.min(scale, strikes);
    if (this.booted) this.post("fps", { fps: this.effectiveFps() });
  }

  /** The cell was resized or the screen density changed. */
  resize(width: number, height: number, dpr: number): void {
    this.options = { ...this.options, width, height, dpr };
    this.post("resize", { width, height, dpr: Math.min(L.maxDpr, dpr) });
  }

  updateSettings(settings: Record<string, unknown>): void {
    this.options = { ...this.options, settings };
    this.post("settings", { settings, bindings: bindingsOf(settings, this.options.info.variables) });
  }

  /** Pauses (no frames, no timers) or resumes the worker without stopping it. */
  setPaused(paused: boolean): void {
    this.paused = paused;
    if (this.state.kind === "stopped" || !this.booted) return;
    this.post("visibility", { paused });
    this.setState(paused ? { kind: "paused" } : { kind: "running" });
  }

  /** Touch or mouse input on the canvas, in canvas pixels. `real` must be true only for a trusted browser event. */
  pointer(phase: "down" | "move" | "up" | "cancel", x: number, y: number, real: boolean): void {
    if (this.options.host.mode === "edit" || !this.options.info.interactive) return;
    if (phase === "up" && real) this.gestureUntil = this.runtime.time.now() + L.gestureMs;
    this.post("pointer", { phase, x, y });
  }

  /** Starts again after it was stopped (the "Restart" button, or the page was shown again). */
  restart(): void {
    if (this.disposed || this.state.kind !== "stopped") return;
    this.restarted = false;
    this.begin();
  }

  /** Stops the worker and removes everything of this widget. */
  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.teardown();
  }

  private stop(reason: PluginWidgetStopReason): void {
    if (this.disposed) return;
    this.teardown();
    this.setState({ kind: "stopped", reason });
    // One automatic restart after a while for a frozen or too busy widget; after that it waits to be started by hand.
    if ((reason === "frozen" || reason === "tooBusy") && !this.restarted) {
      this.restarted = true;
      this.restartTimer = this.runtime.time.setTimeout(() => {
        if (!this.disposed && this.state.kind === "stopped") this.begin();
      }, L.restartAfterMs);
    }
  }

  private teardown(): void {
    const time = this.runtime.time;
    if (this.watchdog !== null) time.clearInterval(this.watchdog);
    if (this.startTimer !== null) time.clearTimeout(this.startTimer);
    if (this.restartTimer !== null && this.disposed) time.clearTimeout(this.restartTimer);
    this.watchdog = this.startTimer = null;
    this.stopListening?.();
    this.stopListening = null;
    if (this.port) {
      this.port.onmessage = null;
      this.port.close();
      this.port = null;
    }
    // Removing the frame ends the worker at once and frees its canvas.
    this.frame?.terminate();
    this.frame = null;
    this.booted = false;
    this.runtime.release(this);
  }

  private setState(state: PluginWidgetState): void {
    this.state = state;
    this.options.onState?.(state);
  }

  private jsonLength(value: unknown): number {
    try {
      return JSON.stringify(value)?.length ?? 0;
    } catch {
      return Infinity;
    }
  }
}

function bindingsOf(settings: Record<string, unknown>, variableKeys: readonly string[] | undefined): Record<string, string> {
  const bindings: Record<string, string> = {};
  for (const key of variableKeys ?? []) if (typeof settings[key] === "string") bindings[key] = settings[key] as string;
  return bindings;
}

function codeOf(e: unknown): string {
  return e instanceof PluginWidgetError ? e.code : "failed";
}

function messageOf(e: unknown): string {
  return e instanceof Error ? e.message.slice(0, 200) : "";
}

/** Decodes a `data:` URI into its bytes and type. */
export function dataUriToBytes(uri: string): { bytes: ArrayBuffer; mime: string } {
  const match = /^data:([^;,]*)(;base64)?,(.*)$/s.exec(uri);
  if (!match) throw new Error("Not a data URI");
  const mime = match[1] || "application/octet-stream";
  const body = match[3] ?? "";
  if (match[2]) {
    const binary = atob(body);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return { bytes: bytes.buffer, mime };
  }
  return { bytes: new TextEncoder().encode(decodeURIComponent(body)).buffer as ArrayBuffer, mime };
}
