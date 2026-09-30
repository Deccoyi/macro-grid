import { afterEach, describe, expect, it, vi } from "vitest";
import { WidgetDataStore } from "../src/widgets/pluginWidget/storage";
import { FRAME_CSP, FRAME_SANDBOX, frameSrcdoc, type FrameStarter } from "../src/widgets/pluginWidget/launcher";
import { dataUriToBytes, PluginWidgetRuntime, type PluginWidgetInstance, type RuntimeClock } from "../src/widgets/pluginWidget/runtime";
import { PLUGIN_WIDGET_LIMITS as L, PluginWidgetError, type PluginWidgetHost, type PluginWidgetListener, type PluginWidgetRuntimeInfo, type PluginWidgetState } from "../src/widgets/pluginWidget/types";

/** Lets the message ports deliver: a few turns of the event loop. */
const settle = async () => {
  for (let i = 0; i < 4; i++) await new Promise<void>((r) => setTimeout(r, 0));
};

/** A clock the test moves by hand. */
function fakeClock() {
  let now = 0;
  let next = 0;
  const timers: { id: number; at: number; fn: () => void; every?: number }[] = [];
  const clock: RuntimeClock = {
    now: () => now,
    setInterval: (fn, ms) => (timers.push({ id: ++next, at: now + ms, fn, every: ms }), next),
    clearInterval: (h) => void timers.splice(0, timers.length, ...timers.filter((t) => t.id !== h)),
    setTimeout: (fn, ms) => (timers.push({ id: ++next, at: now + ms, fn }), next),
    clearTimeout: (h) => void timers.splice(0, timers.length, ...timers.filter((t) => t.id !== h)),
  };
  const advance = (ms: number) => {
    const end = now + ms;
    for (;;) {
      const due = timers.filter((t) => t.at <= end).sort((a, b) => a.at - b.at || a.id - b.id)[0];
      if (!due) break;
      now = due.at;
      if (due.every !== undefined) due.at = now + due.every;
      else timers.splice(timers.indexOf(due), 1);
      due.fn();
    }
    now = end;
  };
  return { clock, advance };
}

interface Started {
  id: string;
  script: string;
  port: MessagePort;
  /** What the renderer has said to this worker. */
  said: { v: number; type: string; id?: number; data?: Record<string, unknown> }[];
}

const cleanups: (() => void)[] = [];
afterEach(() => {
  for (const c of cleanups.splice(0)) c();
});

function setup(hostOverrides: Partial<PluginWidgetHost> = {}, runtimeOptions: { onLiveChange?: (plugins: string[]) => void } = {}) {
  const { clock, advance } = fakeClock();
  const started: Started[] = [];
  const terminated: string[] = [];
  // A fake frame: it keeps the worker's end of the port and puts a canvas in the container, as the real frame would draw one.
  const frameStarter: FrameStarter = async (id, container, script, port) => {
    const entry: Started = { id, script, port, said: [] };
    port.onmessage = (e) => entry.said.push(e.data);
    started.push(entry);
    const canvas = container.ownerDocument.createElement("canvas");
    container.appendChild(canvas);
    return {
      terminate: () => {
        terminated.push(id);
        canvas.remove();
      },
    };
  };
  const runtime = new PluginWidgetRuntime({ frameStarter, clock, ...runtimeOptions });
  const listeners = new Map<string, PluginWidgetListener>();
  const host = {
    mode: "run",
    locale: "tr",
    loadAsset: vi.fn(async (ref: string) => (ref === "asset:code" ? "// widget code" : "data:image/png;base64,AQID")),
    request: vi.fn(async (_id: string, data: unknown) => ({ echo: data })),
    run: vi.fn(async () => undefined),
    subscribe: vi.fn(),
    ready: vi.fn(),
    reportError: vi.fn(),
    listen: vi.fn((id: string, l: PluginWidgetListener) => {
      listeners.set(id, l);
      return () => void listeners.delete(id);
    }),
    ...hostOverrides,
  } as unknown as PluginWidgetHost & { [K in keyof PluginWidgetHost]: PluginWidgetHost[K] & ReturnType<typeof vi.fn> };
  const container = document.createElement("div");
  document.body.appendChild(container);
  cleanups.push(() => {
    runtime.dispose();
    container.remove();
  });

  const infoOf = (extra: Partial<PluginWidgetRuntimeInfo> = {}): PluginWidgetRuntimeInfo => ({
    name: "Gauge", code: "asset:code", fps: 30, interactive: true, verified: true, options: [], variables: ["source"], assets: { "a.png": "asset:img" }, ...extra,
  });

  let n = 0;
  const mount = async (extra: Partial<PluginWidgetRuntimeInfo> = {}, mountExtra: { states?: PluginWidgetState[]; widgetId?: string; plugin?: string } = {}) => {
    const widgetId = mountExtra.widgetId ?? `w${++n}`;
    const instance = runtime.mount({
      widgetId, plugin: mountExtra.plugin, container, info: infoOf(extra), settings: { source: "sys.cpu", max: 5 }, host, width: 200, height: 100, dpr: 3,
      onState: (s) => mountExtra.states?.push(s),
    });
    await settle();
    const entry = started[started.length - 1]!;
    return { instance, entry, widgetId, said: (type: string) => entry.said.filter((m) => m.type === type), send: (type: string, data?: unknown, id?: number) => entry.port.postMessage({ v: 1, type, data, id }) };
  };
  /** Mounts, and has the worker say it is up. */
  const mountBooted = async (extra: Partial<PluginWidgetRuntimeInfo> = {}, mountExtra: { states?: PluginWidgetState[]; widgetId?: string } = {}) => {
    const m = await mount(extra, mountExtra);
    m.send("booted");
    await settle();
    return m;
  };
  return { runtime, host, started, terminated, advance, mount, mountBooted, container, listeners };
}

describe("plugin widget runtime", () => {
  it("starts a worker with the assembled script and the canvas, and sends the init once the worker is up", async () => {
    const { mountBooted, host } = setup();

    const w = await mountBooted();

    expect(w.entry.script).toContain("macroGrid");
    expect(w.entry.script).toContain("// widget code");
    const init = w.said("init")[0]!.data!;
    expect(init).toMatchObject({ width: 200, height: 100, dpr: 2, fps: 30, mode: "run", locale: "tr", bindings: { source: "sys.cpu" }, settings: { source: "sys.cpu", max: 5 } });
    expect(host.ready).toHaveBeenCalledWith(w.widgetId);
    expect(w.instance.current.kind).toBe("running");
  });

  it("caps the pixel density at 2", async () => {
    const { mountBooted } = setup();

    const w = await mountBooted();
    w.instance.resize(300, 150, 4);
    await settle();

    expect(w.said("resize")[0]!.data).toMatchObject({ width: 300, height: 150, dpr: 2 });
  });

  it("stops a widget that never says it started", async () => {
    const { mount, advance } = setup();
    const states: PluginWidgetState[] = [];
    await mount({}, { states });

    advance(L.startTimeoutMs + 1);

    expect(states.at(-1)).toEqual({ kind: "stopped", reason: "startTimeout" });
  });

  it("pings every second, stops a worker that stops answering, and restarts it once after ten seconds", async () => {
    const { mountBooted, advance, started, terminated } = setup();
    const states: PluginWidgetState[] = [];
    const w = await mountBooted({}, { states });

    advance(1000);
    await settle();
    expect(w.said("ping")).toHaveLength(1);
    w.send("pong");
    await settle();
    advance(4000);
    await settle();
    expect(states.at(-1)).toEqual({ kind: "stopped", reason: "frozen" });
    expect(terminated).toContain(w.entry.id);

    advance(L.restartAfterMs);
    await settle();
    expect(started).toHaveLength(2); // one automatic restart

    started[1]!.port.postMessage({ v: 1, type: "booted" });
    await settle();
    advance(L.frozenAfterMs + 1000);
    expect(states.at(-1)).toEqual({ kind: "stopped", reason: "frozen" });
    advance(60000);
    await settle();
    expect(started).toHaveLength(2); // stays stopped until asked
  });

  it("restarts a stopped widget by hand", async () => {
    const { mount, advance, started } = setup();
    const w = await mount();
    advance(L.startTimeoutMs + 1);

    w.instance.restart();
    await settle();

    expect(started).toHaveLength(2);
  });

  it("halves the frame rate of a busy widget once and stops it the second time", async () => {
    const { mountBooted } = setup();
    const states: PluginWidgetState[] = [];
    const w = await mountBooted({}, { states });

    w.send("load", { busy: 0.9 });
    await settle();
    expect(w.said("fps").at(-1)!.data).toEqual({ fps: 15 });
    w.send("load", { busy: 0.9 });
    await settle();

    expect(states.at(-1)).toEqual({ kind: "stopped", reason: "tooBusy" });
  });

  it("holds a plugin that is not verified to a lower busy limit", async () => {
    const { mountBooted } = setup();
    const w = await mountBooted({ verified: false });

    w.send("load", { busy: 0.3 });
    await settle();

    expect(w.said("fps")).toHaveLength(1);
  });

  it("allows 8 live widgets and only 2 of an unverified plugin, and reports the rest as too many", async () => {
    const { mount } = setup();
    const unverified: PluginWidgetInstance[] = [];
    for (let i = 0; i < 3; i++) unverified.push((await mount({ verified: false })).instance);
    expect(unverified.map((i) => i.current.kind)).toEqual(["starting", "starting", "stopped"]);
    expect((unverified[2]!.current as { reason: string }).reason).toBe("tooMany");

    const signed: PluginWidgetInstance[] = [];
    for (let i = 0; i < 8; i++) signed.push((await mount()).instance);

    expect(signed.filter((i) => i.current.kind === "stopped")).toHaveLength(2); // 8 live in total: 2 unverified + 6 signed
  });

  it("takes the limits from the device and starts the refused widgets when they are raised or a place frees up", async () => {
    const { runtime, mount } = setup();
    runtime.setLiveLimits(2, 1);
    const a = (await mount({ verified: false })).instance;
    const b = (await mount({ verified: false })).instance;
    const c = (await mount()).instance;
    const d = (await mount()).instance;
    expect([a, b, c, d].map((i) => i.current.kind)).toEqual(["starting", "stopped", "starting", "stopped"]);

    runtime.setLiveLimits(4, 4);
    await settle();
    expect([a, b, c, d].map((i) => i.current.kind)).not.toContain("stopped");

    runtime.setLiveLimits(1, 1);
    const e = (await mount()).instance;
    expect(e.current).toEqual({ kind: "stopped", reason: "tooMany" });
    a.dispose();
    b.dispose();
    c.dispose();
    d.dispose();
    await settle();
    expect(e.current.kind).not.toBe("stopped");
  });

  it("tells which plugins have live widgets, before a worker starts and after the last one of a plugin ends", async () => {
    const seen: string[][] = [];
    const { mount } = setup({}, { onLiveChange: (plugins) => seen.push(plugins) });

    const a = (await mount({}, { plugin: "gauges" })).instance;
    const b = (await mount({}, { plugin: "clock" })).instance;
    const c = (await mount({}, { plugin: "gauges" })).instance;
    a.dispose();
    c.dispose();
    b.dispose();

    expect(seen).toEqual([["gauges"], ["clock", "gauges"], ["clock", "gauges"], ["clock", "gauges"], ["clock"], []]);
  });

  it("does not let widgets that stopped drawing take budget from the ones that still draw", async () => {
    const { mountBooted, advance } = setup();
    const workers = [];
    for (let i = 0; i < 6; i++) workers.push(await mountBooted({ fps: 60 }));

    // Time passes with the workers answering the watchdog but drawing nothing.
    for (let t = 0; t < L.activeForMs + 1000; t += 1000) {
      advance(1000);
      for (const w of workers) w.send("pong");
      await settle();
    }
    // Only the first widget reports frames; the others drew nothing since they started.
    workers[0]!.send("load", { busy: 0.01, frames: 30 });
    await settle();

    expect(workers[0]!.said("fps").at(-1)!.data).toEqual({ fps: 60 });
  });

  it("gives a widget that declared storage its own store, and refuses one that did not", async () => {
    const map = new Map<string, string>();
    const store = new WidgetDataStore({ getItem: (k) => map.get(k) ?? null, setItem: (k, v) => void map.set(k, v), removeItem: (k) => void map.delete(k) }, "t.");
    const { mountBooted } = setup({ storage: store });
    const allowed = await mountBooted({ options: ["storage"] });
    const refused = await mountBooted({ options: [] });

    allowed.send("storage", { op: "set", key: "n", value: 7 }, 1);
    allowed.send("storage", { op: "get", key: "n" }, 2);
    refused.send("storage", { op: "get", key: "n" }, 1);
    await settle();

    expect(allowed.said("reply").map((m) => m.data)).toEqual([expect.objectContaining({ ok: true }), expect.objectContaining({ ok: true, data: 7 })]);
    expect(refused.said("reply")[0]!.data).toMatchObject({ ok: false, error: "not_allowed" });
    expect(store.get(allowed.widgetId, "n")).toBe(7);
    expect(store.get(refused.widgetId, "n")).toBeNull();
  });

  it("scales every widget down when the sum of their frame caps passes the budget", async () => {
    const { mountBooted } = setup();
    const workers = [];
    for (let i = 0; i < 6; i++) workers.push(await mountBooted({ fps: 60 })); // 360 asked, 120 allowed

    await settle();

    // A worker that was already up is told; the last one starts with the scaled rate in its init.
    for (const w of workers) expect((w.said("fps").at(-1)?.data ?? w.said("init")[0]!.data) as { fps: number }).toMatchObject({ fps: 20 });
  });

  it("forwards a widget's request to the host and returns the answer under the same id", async () => {
    const { mountBooted, host } = setup();
    const w = await mountBooted();

    w.send("request", { data: { op: "x" } }, 7);
    await settle();
    await settle();

    expect(host.request).toHaveBeenCalledWith(w.widgetId, { op: "x" });
    expect(w.said("reply").at(-1)).toMatchObject({ id: 7, data: { ok: true, data: { echo: { op: "x" } } } });
  });

  it("answers with the host's error code and refuses too large and too many requests itself", async () => {
    const { mountBooted, host } = setup();
    host.request.mockRejectedValueOnce(new PluginWidgetError("timeout", "slow"));
    const w = await mountBooted();

    w.send("request", { data: 1 }, 1);
    await settle();
    await settle();
    expect(w.said("reply").at(-1)!.data).toMatchObject({ ok: false, error: "timeout" });

    w.send("request", { data: "x".repeat(L.maxRequestChars + 1) }, 2);
    await settle();
    expect(w.said("reply").at(-1)!.data).toMatchObject({ ok: false, error: "too_large" });

    for (let i = 0; i < 12; i++) w.send("request", { data: i }, 10 + i);
    await settle();
    await settle();
    expect(w.said("reply").filter((m) => (m.data as { error?: string }).error === "rate_limited").length).toBeGreaterThan(0);
  });

  it("marks a run as a real gesture only once after a real pointer up, and only within a second", async () => {
    const { mountBooted, host, advance } = setup();
    const w = await mountBooted();
    const run = async (id: number) => {
      w.send("run", { action: "p.act", settings: {} }, id);
      await settle();
      await settle();
    };

    await run(1); // no touch yet
    expect(host.run).toHaveBeenLastCalledWith(w.widgetId, "p.act", {}, false);

    advance(1100); // rate bucket window
    w.instance.pointer("down", 1, 1, true);
    w.instance.pointer("up", 1, 1, true);
    await run(2);
    expect(host.run).toHaveBeenLastCalledWith(w.widgetId, "p.act", {}, true);

    await run(3); // the token was used up
    expect(host.run).toHaveBeenLastCalledWith(w.widgetId, "p.act", {}, false);

    advance(1100);
    w.instance.pointer("up", 1, 1, true);
    advance(1500); // too late
    await run(4);
    expect(host.run).toHaveBeenLastCalledWith(w.widgetId, "p.act", {}, false);
  });

  it("does not turn a synthetic pointer event into a gesture, and one widget's touch does not help another", async () => {
    const { mountBooted, host, advance } = setup();
    const a = await mountBooted();
    const b = await mountBooted();

    a.instance.pointer("up", 1, 1, false);
    b.send("run", { action: "p.act" }, 1);
    await settle();
    await settle();
    expect(host.run).toHaveBeenLastCalledWith(b.widgetId, "p.act", {}, false);

    advance(1100);
    a.instance.pointer("up", 1, 1, true);
    b.send("run", { action: "p.act" }, 2);
    await settle();
    await settle();
    expect(host.run).toHaveBeenLastCalledWith(b.widgetId, "p.act", {}, false);
  });

  it("never runs an action from the editor's preview and gives it no pointer input", async () => {
    const { mountBooted, host } = setup({ mode: "edit" });
    const w = await mountBooted();

    w.send("run", { action: "p.act" }, 1);
    await settle();
    w.instance.pointer("down", 1, 1, true);
    await settle();

    expect(host.run).not.toHaveBeenCalled();
    expect(w.said("reply").at(-1)!.data).toMatchObject({ ok: false, error: "editor" });
    expect(w.said("pointer")).toHaveLength(0);
  });

  it("sends pointer input only to a widget whose manifest asked for it", async () => {
    const { mountBooted } = setup();
    const quiet = await mountBooted({ interactive: false });
    const touchy = await mountBooted({ interactive: true });

    quiet.instance.pointer("down", 1, 2, true);
    touchy.instance.pointer("down", 3, 4, true);
    await settle();

    expect(quiet.said("pointer")).toHaveLength(0);
    expect(touchy.said("pointer")[0]!.data).toEqual({ phase: "down", x: 3, y: 4 });
  });

  it("subscribes with at most 32 names and passes pushed values and events on", async () => {
    const { mountBooted, host, listeners } = setup();
    const w = await mountBooted();

    w.send("subscribe", { variables: Array.from({ length: 40 }, (_, i) => "p.v" + i) });
    await settle();
    listeners.get(w.widgetId)!.vars({ "p.v1": 5 });
    listeners.get(w.widgetId)!.event("hello", { a: 1 });
    await settle();

    expect((host.subscribe as unknown as { mock: { calls: [string, string[]][] } }).mock.calls[0]![1]).toHaveLength(32);
    expect(w.said("vars")[0]!.data).toEqual({ values: { "p.v1": 5 } });
    expect(w.said("event")[0]!.data).toEqual({ name: "hello", data: { a: 1 } });
  });

  it("reports a widget's errors to the host, at most five per ten seconds", async () => {
    const { mountBooted, host, advance } = setup();
    const w = await mountBooted();

    for (let i = 0; i < 9; i++) w.send("error", { message: "oops " + i });
    await settle();
    expect(host.reportError).toHaveBeenCalledTimes(L.errorsPer10s);

    advance(11000);
    w.send("error", { message: "later" });
    await settle();
    expect(host.reportError).toHaveBeenCalledTimes(L.errorsPer10s + 1);
  });

  it("hands a package image to the worker as bytes and refuses a name that is not in the package", async () => {
    const { mountBooted } = setup();
    const w = await mountBooted();

    w.send("asset", { name: "a.png" });
    w.send("asset", { name: "../../etc/passwd" });
    await settle();
    await settle();

    const answers = w.said("asset");
    const good = answers.find((m) => m.data!.name === "a.png")!.data as { ok: boolean; mime: string; bytes: ArrayBuffer };
    expect(good.ok).toBe(true);
    expect(good.mime).toBe("image/png");
    expect([...new Uint8Array(good.bytes)]).toEqual([1, 2, 3]);
    expect(answers.find((m) => m.data!.name === "../../etc/passwd")!.data).toMatchObject({ ok: false });
  });

  it("pauses and resumes without stopping the worker", async () => {
    const { mountBooted, terminated } = setup();
    const states: PluginWidgetState[] = [];
    const w = await mountBooted({}, { states });

    w.instance.setPaused(true);
    await settle();
    expect(w.said("visibility").at(-1)!.data).toEqual({ paused: true });
    expect(states.at(-1)).toEqual({ kind: "paused" });
    w.instance.setPaused(false);
    await settle();

    expect(states.at(-1)).toEqual({ kind: "running" });
    expect(terminated).toHaveLength(0);
  });

  it("stops the worker, removes the canvas and the listener when disposed", async () => {
    const { mountBooted, terminated, container, listeners, runtime } = setup();
    const w = await mountBooted();
    expect(container.querySelector("canvas")).not.toBeNull();

    w.instance.dispose();
    await settle();

    expect(terminated).toContain(w.entry.id);
    expect(container.querySelector("canvas")).toBeNull();
    expect(listeners.size).toBe(0);
    expect(runtime.liveCount).toBe(0);
  });

  it("reports a failed start with the reason and does not throw", async () => {
    const { mount, host } = setup({ loadAsset: vi.fn(async () => { throw new Error("no code"); }) });
    const states: PluginWidgetState[] = [];

    await mount({}, { states });

    expect(states.at(-1)).toEqual({ kind: "stopped", reason: "failed" });
    expect(host.reportError).toHaveBeenCalled();
  });
});

describe("widget frame", () => {
  it("is sandboxed without allow-same-origin and its content policy blocks every network request", () => {
    expect(FRAME_SANDBOX).toBe("allow-scripts");
    expect(FRAME_SANDBOX).not.toContain("allow-same-origin");
    expect(FRAME_CSP).toContain("connect-src 'none'");
    expect(FRAME_CSP).toContain("default-src 'none'");
    expect(FRAME_CSP).toContain("worker-src blob:");
    expect(FRAME_CSP).not.toContain("'unsafe-eval'");
    expect(frameSrcdoc()).toContain(FRAME_CSP);
  });

  it("makes the canvas inside the frame, so canvas and worker share a process", () => {
    const doc = frameSrcdoc();

    expect(doc).toContain('<canvas id="c">');
    expect(doc).toContain("transferControlToOffscreen");
  });
});

describe("dataUriToBytes", () => {
  it("decodes base64 and plain data URIs", () => {
    expect([...new Uint8Array(dataUriToBytes("data:image/png;base64,AQID").bytes)]).toEqual([1, 2, 3]);
    expect(new TextDecoder().decode(dataUriToBytes("data:text/plain,hi%20there").bytes)).toBe("hi there");
    expect(() => dataUriToBytes("nope")).toThrow();
  });
});
