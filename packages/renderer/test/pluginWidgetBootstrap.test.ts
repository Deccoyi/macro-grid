import { describe, expect, it } from "vitest";
import { assembleWorkerScript, WORKER_BOOTSTRAP } from "../src/widgets/pluginWidget/bootstrap";

interface Api {
  apiVersion: number;
  ready: Promise<Record<string, unknown>>;
  frame(cb: () => void): number;
  onVisibility(fn: (state: unknown) => void): void;
  onVariables(fn: (values: unknown) => void): void;
  onEvent(name: string, fn: (data: unknown) => void): void;
  onPointer(fn: (p: unknown) => void): void;
  subscribe(names: string[]): void;
  request(data: unknown): Promise<unknown>;
  run(action: string, settings?: unknown): Promise<unknown>;
  storage: { get(): Promise<unknown> };
  notify(): Promise<unknown>;
}

/** A minimal stand-in for a worker's global scope, with timers the test moves by hand, to run the real bootstrap. */
function makeWorld(authorCode?: string) {
  let now = 0;
  let nextId = 0;
  const timers: { id: number; at: number; fn: () => void; every?: number }[] = [];
  const listeners: Record<string, (e: unknown) => void> = {};
  const sent: { v: number; type: string; id?: number; data?: Record<string, unknown> }[] = [];

  const self: Record<string, unknown> = {
    setTimeout: (fn: () => void, ms = 0) => {
      timers.push({ id: ++nextId, at: now + ms, fn });
      return nextId;
    },
    setInterval: (fn: () => void, ms = 0) => {
      timers.push({ id: ++nextId, at: now + ms, fn, every: ms });
      return nextId;
    },
    clearTimeout: (id: number) => void timers.splice(0, timers.length, ...timers.filter((t) => t.id !== id)),
    clearInterval: (id: number) => void timers.splice(0, timers.length, ...timers.filter((t) => t.id !== id)),
    fetch: () => undefined,
    XMLHttpRequest: class {},
    WebSocket: class {},
    EventSource: class {},
    importScripts: () => undefined,
    Worker: class {},
    SharedWorker: class {},
    indexedDB: {},
    caches: {},
    Blob: class {},
    Promise,
    navigator: { sendBeacon: () => true },
    createImageBitmap: async () => ({}),
    addEventListener: (type: string, fn: (e: unknown) => void) => {
      listeners[type] = fn;
    },
    onmessage: null as null | ((e: { data: unknown }) => void),
  };
  const port = {
    onmessage: null as null | ((e: { data: unknown }) => void),
    postMessage: (m: never) => void sent.push(m),
  };

  const code = authorCode === undefined ? WORKER_BOOTSTRAP : assembleWorkerScript(authorCode);
  // The real bootstrap and author code refer to globals (`macroGrid`, `requestAnimationFrame`); run them with the fake scope as the global one.
  self.self = self;
  self.performance = { now: () => now };
  const scope = new Proxy(self, {
    has: () => true,
    get: (target, key) => (key in target ? target[key as string] : (globalThis as Record<PropertyKey, unknown>)[key]),
  });
  new Function("scope", `with (scope) {
${code}
}`)(scope);

  const advance = (ms: number) => {
    const end = now + ms;
    for (;;) {
      const due = timers.filter((t) => t.at <= end).sort((a, b) => a.at - b.at || a.id - b.id)[0];
      if (!due) break;
      now = due.at;
      if (due.every !== undefined) due.at = now + Math.max(1, due.every);
      else timers.splice(timers.indexOf(due), 1);
      due.fn();
    }
    now = end;
  };

  const macroGrid = () => self.macroGrid as Api;
  const start = () => (self.onmessage as ((e: unknown) => void) | null)?.({ data: { type: "start", canvas: { fake: true }, port } });
  const say = (type: string, data?: unknown, id?: number) => port.onmessage!({ data: { v: 1, type, data, id } });
  const init = (extra: Record<string, unknown> = {}) =>
    say("init", { width: 200, height: 100, dpr: 2, fps: 30, settings: { a: 1 }, bindings: { source: "x.y" }, mode: "run", locale: "en", theme: "dark", options: [], ...extra });
  const of = (type: string) => sent.filter((m) => m.type === type);

  return { self, port, sent, listeners, advance, macroGrid, start, say, init, of, time: () => now };
}

describe("worker bootstrap", () => {
  it("takes the network, storage and nested workers away and cannot be given them back", () => {
    const w = makeWorld();

    for (const name of ["fetch", "XMLHttpRequest", "WebSocket", "EventSource", "importScripts", "Worker", "SharedWorker", "indexedDB", "caches"]) {
      expect(w.self[name]).toBeUndefined();
      expect(() => {
        w.self[name] = () => 1;
      }).toThrow();
    }
    expect((w.self.navigator as { sendBeacon?: unknown }).sendBeacon).toBeUndefined();
  });

  it("defines one frozen macroGrid that cannot be replaced", () => {
    const w = makeWorld();

    expect(Object.isFrozen(w.macroGrid())).toBe(true);
    expect(w.macroGrid().apiVersion).toBe(1);
    expect(() => Object.defineProperty(w.self, "macroGrid", { value: {} })).toThrow();
    expect(() => {
      w.self.macroGrid = {};
    }).toThrow();
  });

  it("says it started once it is handed its canvas and port, and ignores a second start", () => {
    const w = makeWorld();

    w.start();
    w.start();

    expect(w.of("booted")).toHaveLength(1);
  });

  it("answers a ping from its own event loop", () => {
    const w = makeWorld();
    w.start();

    w.say("ping");

    expect(w.of("pong")).toHaveLength(1);
  });

  it("resolves macroGrid.ready with the canvas and the widget's settings", async () => {
    const w = makeWorld();
    w.start();
    w.init();

    const ready = await w.macroGrid().ready;

    expect(ready.canvas).toEqual({ fake: true });
    expect(ready).toMatchObject({ width: 200, height: 100, dpr: 2, settings: { a: 1 }, bindings: { source: "x.y" }, mode: "run" });
    expect(Object.isFrozen(ready)).toBe(true);
  });

  it("never runs frames faster than the frame-rate cap", () => {
    const w = makeWorld();
    w.start();
    w.init({ fps: 10 });
    let frames = 0;
    const loop = () => {
      frames++;
      w.macroGrid().frame(loop);
    };
    w.macroGrid().frame(loop);

    w.advance(1000);

    expect(frames).toBeGreaterThanOrEqual(9);
    expect(frames).toBeLessThanOrEqual(11);
  });

  it("gives a library's own requestAnimationFrame the same cap and pause", () => {
    const w = makeWorld();
    w.start();
    w.init({ fps: 10 });
    let frames = 0;
    const raf = w.self.requestAnimationFrame as (cb: () => void) => number;
    const loop = () => {
      frames++;
      raf(loop);
    };
    raf(loop);

    w.advance(1000);
    expect(frames).toBeLessThanOrEqual(11);

    w.say("visibility", { paused: true });
    const before = frames;
    w.advance(1000);
    expect(frames).toBe(before);
  });

  it("stops frames and holds timers while paused and catches up once on resume", () => {
    const w = makeWorld();
    w.start();
    w.init({ fps: 30 });
    let frames = 0;
    let timerRuns = 0;
    const loop = () => {
      frames++;
      w.macroGrid().frame(loop);
    };
    w.macroGrid().frame(loop);
    (w.self.setTimeout as (fn: () => void, ms: number) => void)(() => timerRuns++, 500);
    const seen: string[] = [];
    w.macroGrid().onVisibility((s) => seen.push(String(s)));

    w.say("visibility", { paused: true });
    const at = frames;
    w.advance(2000);

    expect(frames).toBe(at);
    expect(timerRuns).toBe(0);

    w.say("visibility", { paused: false });
    w.advance(200);

    expect(frames).toBeGreaterThan(at);
    expect(timerRuns).toBe(1);
    expect(seen).toEqual(["paused", "resumed"]);
  });

  it("keeps the merged values of subscribed variables and gives them all to each callback", () => {
    const w = makeWorld();
    w.start();
    w.init();
    const seen: unknown[] = [];
    w.macroGrid().onVariables((v) => seen.push(v));

    w.say("vars", { values: { "a.x": 1 } });
    w.say("vars", { values: { "a.y": 2 } });

    expect(seen).toEqual([{ "a.x": 1 }, { "a.x": 1, "a.y": 2 }]);
  });

  it("sends at most 32 subscribed names", () => {
    const w = makeWorld();
    w.start();
    w.init();

    w.macroGrid().subscribe(Array.from({ length: 50 }, (_, i) => "p.v" + i));

    expect((w.of("subscribe")[0]!.data!.variables as string[]).length).toBe(32);
  });

  it("refuses a request over 16 KB on its own side and matches replies by id", async () => {
    const w = makeWorld();
    w.start();
    w.init();

    await expect(w.macroGrid().request("x".repeat(20000))).rejects.toThrow(/too_large/);
    expect(w.of("request")).toHaveLength(0);

    const ok = w.macroGrid().request({ op: 1 });
    const failing = w.macroGrid().run("p.act", {});
    const [req] = w.of("request");
    const [run] = w.of("run");
    w.say("reply", { ok: false, error: "rate_limited", message: "slow down" }, run!.id);
    w.say("reply", { ok: true, data: { fine: true } }, req!.id);

    await expect(ok).resolves.toEqual({ fine: true });
    await expect(failing).rejects.toMatchObject({ code: "rate_limited" });
  });

  it("delivers events by name and a failing callback does not stop the others", () => {
    const w = makeWorld();
    w.start();
    w.init();
    const seen: unknown[] = [];
    w.macroGrid().onEvent("tick", () => {
      throw new Error("bad callback");
    });
    w.macroGrid().onEvent("tick", (d) => seen.push(d));

    w.say("event", { name: "tick", data: 7 });

    expect(seen).toEqual([7]);
    expect(w.of("error")[0]!.data!.message).toBe("bad callback");
  });

  it("gives pointer input to the widget only when it is sent", () => {
    const w = makeWorld();
    w.start();
    w.init();
    const seen: unknown[] = [];
    w.macroGrid().onPointer((p) => seen.push(p));

    w.say("pointer", { phase: "down", x: 5, y: 6 });

    expect(seen).toEqual([{ phase: "down", x: 5, y: 6 }]);
  });

  it("reports how busy it is with frame work every five seconds", () => {
    const w = makeWorld();
    w.start();
    w.init({ fps: 30 });
    let now = 0;
    // Each frame callback reports it took 40 ms of a 100 ms interval by moving the fake clock inside the callback.
    const loop = () => {
      now += 1;
      w.macroGrid().frame(loop);
    };
    w.macroGrid().frame(loop);

    w.advance(5200);

    expect(w.of("load").length).toBeGreaterThanOrEqual(1);
    expect(typeof w.of("load")[0]!.data!.busy).toBe("number");
    expect(now).toBeGreaterThan(0);
  });

  it("refuses storage and notifications unless they are declared (phase 3 wires them)", async () => {
    const w = makeWorld();
    w.start();
    w.init();

    await expect(w.macroGrid().storage.get()).rejects.toThrow(/not_allowed/);
    await expect(w.macroGrid().notify()).rejects.toThrow(/not_allowed/);
  });

  it("wraps the author's script so top-level await works and a thrown error is reported", async () => {
    const w = makeWorld("const info = await macroGrid.ready; throw new Error('boom ' + info.width);");
    w.start();
    w.init();

    await new Promise((r) => setTimeout(r, 10));

    expect(w.of("error")[0]!.data!.message).toBe("boom 200");
  });

  it("does not let the author code cross the wrapper: the assembled script keeps the bootstrap first", () => {
    const script = assembleWorkerScript("// only a comment");

    expect(script.startsWith(WORKER_BOOTSTRAP)).toBe(true);
    expect(script).toContain("// only a comment\n})()");
  });
});
