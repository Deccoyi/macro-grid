/**
 * The code that runs first inside every plugin widget worker, before the author's script (docs/design/plugin-widgets.md).
 *
 * It is a string because it becomes part of a `blob:` worker script. It defines the one frozen `macroGrid` object the author sees,
 * takes the network and storage APIs away from the worker (defence in depth: the frame's CSP already blocks them), owns the frame
 * loop (frame-rate cap, pause) and answers the renderer's watchdog pings from the worker's own event loop.
 *
 * Everything the worker says or hears goes over one MessagePort as `{ v: 1, type, ... }`. The bootstrap takes no other input.
 * It has no DOM, so it must not use one. Keep it free of backticks and of the two-character sequence dollar plus brace: it is embedded in a template.
 */
export const WORKER_BOOTSTRAP = String.raw`
(function () {
  "use strict";
  var g = self;
  var V = 1;
  var MAX_REQUEST_CHARS = 16 * 1024;

  // Keep what the bootstrap needs, then take the risky APIs away from the author (a shadowing property cannot be deleted or replaced).
  var nativeSetTimeout = g.setTimeout.bind(g);
  var nativeClearTimeout = g.clearTimeout.bind(g);
  var nativeRaf = typeof g.requestAnimationFrame === "function" ? g.requestAnimationFrame.bind(g) : null;
  var nativeCancelRaf = typeof g.cancelAnimationFrame === "function" ? g.cancelAnimationFrame.bind(g) : null;
  var nativeCreateImageBitmap = typeof g.createImageBitmap === "function" ? g.createImageBitmap.bind(g) : null;
  var nativeNow = performance.now.bind(performance);
  var NativeBlob = g.Blob;
  var NativePromise = g.Promise;
  var removed = ["fetch", "XMLHttpRequest", "WebSocket", "WebSocketStream", "EventSource", "importScripts", "Worker", "SharedWorker",
    "BroadcastChannel", "WebTransport", "indexedDB", "IDBFactory", "caches", "CacheStorage", "localStorage", "sessionStorage"];
  for (var i = 0; i < removed.length; i++) {
    try { Object.defineProperty(g, removed[i], { value: undefined, writable: false, configurable: false }); } catch (e) { /* already fixed */ }
  }
  try { Object.defineProperty(g.navigator, "sendBeacon", { value: undefined, writable: false, configurable: false }); } catch (e) { /* not there */ }

  var port = null;
  var paused = false;
  var fps = 15;
  var frameQueue = [];
  var frameSeq = 0;
  var frameTimer = null;
  var lastFrameAt = 0;
  var busyMs = 0;
  var framesRun = 0;
  var busySince = nativeNow();
  var pendingResume = [];
  var canvas = null;
  var initInfo = null;
  var readyResolve = null;
  var pending = Object.create(null);
  var nextId = 1;
  var handlers = { variables: [], event: Object.create(null), settings: [], pointer: [], visibility: [], resize: [] };
  var values = Object.create(null);
  var assetWaiters = Object.create(null);

  function send(type, data, id) {
    if (!port) return;
    var m = { v: V, type: type };
    if (data !== undefined) m.data = data;
    if (id !== undefined) m.id = id;
    port.postMessage(m);
  }

  function report(error) {
    var text = error && error.message ? String(error.message) : String(error);
    send("error", { message: text.slice(0, 500) });
  }

  function guard(fn, arg) {
    try { return fn(arg); } catch (e) { report(e); }
  }

  // ---- frame loop: one loop for macroGrid.frame and for requestAnimationFrame (so libraries obey the same cap and pause) ----
  function scheduleFrame(cb) {
    if (typeof cb !== "function") throw new TypeError("frame needs a function");
    var id = ++frameSeq;
    frameQueue.push({ id: id, cb: cb });
    kick();
    return id;
  }

  function cancelFrame(id) {
    for (var j = 0; j < frameQueue.length; j++) if (frameQueue[j].id === id) { frameQueue.splice(j, 1); return; }
  }

  function kick() {
    if (frameTimer !== null || paused || frameQueue.length === 0) return;
    var wait = Math.max(0, lastFrameAt + 1000 / fps - nativeNow());
    frameTimer = nativeSetTimeout(runFrames, wait);
  }

  function runFrames() {
    frameTimer = null;
    if (paused) return;
    var batch = frameQueue;
    frameQueue = [];
    var start = nativeNow();
    lastFrameAt = start;
    for (var j = 0; j < batch.length; j++) guard(batch[j].cb, start);
    busyMs += nativeNow() - start;
    framesRun++;
    var span = nativeNow() - busySince;
    if (span >= 5000) {
      send("load", { busy: Math.min(1, busyMs / span), frames: framesRun });
      busyMs = 0;
      framesRun = 0;
      busySince = nativeNow();
    }
    kick();
  }

  // Library timers follow the same pause: while paused their callbacks wait and run once on resume.
  function wrapTimer(native, repeat) {
    return function (fn, ms) {
      var args = Array.prototype.slice.call(arguments, 2);
      if (typeof fn !== "function") throw new TypeError("The timer needs a function.");
      var held = false;
      var handle = native(function () {
        if (paused) {
          if (!held) { held = true; pendingResume.push(function () { held = false; guard(function () { fn.apply(null, args); }); }); }
          return;
        }
        guard(function () { fn.apply(null, args); });
      }, ms);
      return handle;
    };
  }
  var nativeSetInterval = g.setInterval.bind(g);
  var nativeClearInterval = g.clearInterval.bind(g);
  g.setTimeout = wrapTimer(nativeSetTimeout, false);
  g.setInterval = wrapTimer(nativeSetInterval, true);
  g.clearTimeout = nativeClearTimeout;
  g.clearInterval = nativeClearInterval;
  g.requestAnimationFrame = scheduleFrame;
  g.cancelAnimationFrame = cancelFrame;

  function setPaused(value) {
    if (paused === value) return;
    paused = value;
    if (!paused) {
      var held = pendingResume;
      pendingResume = [];
      for (var j = 0; j < held.length; j++) held[j]();
      lastFrameAt = 0;
      kick();
    }
    call(handlers.visibility, value ? "paused" : "resumed");
  }

  function call(list, arg) {
    for (var j = 0; j < list.length; j++) guard(list[j], arg);
  }

  // ---- requests to the plugin (over the renderer) ----
  function ask(type, data) {
    var text = JSON.stringify(data === undefined ? null : data);
    if (text.length > MAX_REQUEST_CHARS) return NativePromise.reject(new Error("too_large: a request can be at most 16 KB"));
    var id = nextId++;
    return new NativePromise(function (resolve, reject) {
      pending[id] = { resolve: resolve, reject: reject };
      send(type, data, id);
    });
  }

  function onMessage(e) {
    var m = e.data;
    if (!m || m.v !== V || typeof m.type !== "string") return;
    var d = m.data;
    switch (m.type) {
      case "ping": send("pong"); break;
      case "init":
        initInfo = d;
        fps = d.fps || fps;
        if (readyResolve) readyResolve(freezeInit());
        break;
      case "settings": if (initInfo) { initInfo.settings = d.settings; initInfo.bindings = d.bindings; } call(handlers.settings, d); break;
      case "vars":
        for (var k in d.values) values[k] = d.values[k];
        call(handlers.variables, copy(values));
        break;
      case "event": call(handlers.event[d.name] || [], d.data); break;
      case "reply":
        var p = pending[m.id];
        if (!p) break;
        delete pending[m.id];
        if (d.ok) p.resolve(d.data);
        else { var err = new Error((d.error || "failed") + (d.message ? ": " + d.message : "")); err.code = d.error || "failed"; p.reject(err); }
        break;
      case "visibility": setPaused(d.paused === true); break;
      case "resize":
        if (initInfo) { initInfo.width = d.width; initInfo.height = d.height; initInfo.dpr = d.dpr; }
        call(handlers.resize, { width: d.width, height: d.height, dpr: d.dpr });
        break;
      case "fps": fps = Math.max(1, Math.min(60, d.fps | 0)); break;
      case "pointer": call(handlers.pointer, d); break;
      case "asset":
        var w = assetWaiters[d.name];
        if (!w) break;
        delete assetWaiters[d.name];
        if (d.ok) w.resolve(d); else w.reject(new Error("The asset " + d.name + " is not available."));
        break;
    }
  }

  function copy(o) { var c = {}; for (var k in o) c[k] = o[k]; return c; }

  function freezeInit() {
    return Object.freeze({
      canvas: canvas, width: initInfo.width, height: initInfo.height, dpr: initInfo.dpr, settings: initInfo.settings, bindings: initInfo.bindings,
      mode: initInfo.mode, locale: initInfo.locale, theme: initInfo.theme, options: initInfo.options
    });
  }

  function register(list, fn) {
    if (typeof fn !== "function") throw new TypeError("A function is needed.");
    list.push(fn);
  }

  var readyPromise = new NativePromise(function (resolve) { readyResolve = resolve; });

  var api = {
    apiVersion: 1,
    ready: readyPromise,
    frame: scheduleFrame,
    onResize: function (fn) { register(handlers.resize, fn); },
    subscribe: function (names) {
      if (!Array.isArray(names)) throw new TypeError("subscribe needs a list of variable names.");
      send("subscribe", { variables: names.map(String).slice(0, 32) });
    },
    onVariables: function (fn) { register(handlers.variables, fn); },
    onEvent: function (name, fn) {
      var key = String(name);
      (handlers.event[key] = handlers.event[key] || []).push(fn);
    },
    request: function (data) { return ask("request", data); },
    run: function (action, settings) { return ask("run", { action: String(action), settings: settings || {} }); },
    onSettings: function (fn) { register(handlers.settings, fn); },
    onPointer: function (fn) { register(handlers.pointer, fn); },
    onVisibility: function (fn) { register(handlers.visibility, fn); },
    image: function (name) {
      var key = String(name);
      return new NativePromise(function (resolve, reject) {
        assetWaiters[key] = { resolve: resolve, reject: reject };
        send("asset", { name: key });
      }).then(function (a) { return nativeCreateImageBitmap(new NativeBlob([a.bytes], { type: a.mime })); });
    },
    font: function (name, family) {
      var key = String(name);
      return new NativePromise(function (resolve, reject) {
        assetWaiters[key] = { resolve: resolve, reject: reject };
        send("asset", { name: key });
      }).then(function (a) {
        var face = new g.FontFace(String(family || key), a.bytes);
        return face.load().then(function () { g.fonts.add(face); return face; });
      });
    },
    error: function (message) { report(new Error(String(message))); },
    storage: Object.freeze({
      get: function () { return NativePromise.reject(new Error("not_allowed: this widget did not declare the storage option")); },
      set: function () { return NativePromise.reject(new Error("not_allowed: this widget did not declare the storage option")); },
      remove: function () { return NativePromise.reject(new Error("not_allowed: this widget did not declare the storage option")); }
    }),
    notify: function () { return NativePromise.reject(new Error("not_allowed: this widget did not declare the notifications option")); }
  };
  Object.freeze(api);
  Object.defineProperty(g, "macroGrid", { value: api, writable: false, configurable: false });

  g.addEventListener("error", function (e) { report(e.error || e.message); e.preventDefault(); });
  g.addEventListener("unhandledrejection", function (e) { report(e.reason); e.preventDefault(); });

  // The launcher hands over the canvas and the one port; after that the worker talks to the renderer only through it.
  g.onmessage = function (e) {
    var m = e.data;
    if (!m || m.type !== "start" || port) return;
    canvas = m.canvas || null;
    port = m.port;
    port.onmessage = onMessage;
    g.onmessage = null;
    send("booted");
  };
})();
`;

/**
 * The author's script is wrapped so that top-level await and return work (a worker script is a classic script). An error the script throws
 * (also asynchronously) is reported to the Error List; it never escapes into the worker's global handler twice.
 */
export function assembleWorkerScript(authorCode: string): string {
  return `${WORKER_BOOTSTRAP}\n;(async function () {\n${authorCode}\n})().catch(function (e) { macroGrid.error(e && e.message ? e.message : e); });\n`;
}
