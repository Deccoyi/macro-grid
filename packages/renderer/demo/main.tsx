import { useState } from "react";
import { createRoot } from "react-dom/client";
import {
  Grid,
  PluginWidgetContext,
  PluginWidgetRuntime,
  WidgetView,
  type Page,
  type PluginWidgetHost,
  type PluginWidgetListener,
  type Widget,
} from "@macro/renderer";

const page: Page = {
  id: "p1",
  name: "Demo",
  cols: 4,
  rows: 3,
  widgets: [
    { id: "btn", actions: {}, type: "button", x: 0, y: 0, w: 1, h: 1, text: "Kopyala", style: { background: "#1d4ed8", foreground: "#fff" } },
    { id: "gradient", actions: {}, type: "button", x: 1, y: 0, w: 1, h: 1, text: "Custom CSS", style: { foreground: "#fff", radius: 12 },
      customCss: ":host { background: linear-gradient(135deg, #f59e0b, #dc2626); border: 2px solid gold; width: 999px; }" },
    { id: "toggle", actions: {}, type: "toggle", x: 2, y: 0, w: 1, h: 1, text: "Sessiz", style: { background: "#374151", foreground: "#fff" } },
    { id: "label", actions: {}, type: "label", x: 3, y: 0, w: 1, h: 1, text: "CPU\n42%", style: { background: "#1f2937", foreground: "#38bdf8" } },
    { id: "slider", actions: {}, type: "slider", x: 0, y: 1, w: 2, h: 1, text: "Ses", props: { min: 0, max: 100 }, style: { background: "#232529", foreground: "#e6e7ea" } },
    { id: "knob", actions: {}, type: "knob", x: 2, y: 1, w: 1, h: 2, text: "Gain", props: { min: 0, max: 100 }, style: { background: "#232529", foreground: "#e6e7ea" } },
    { id: "image", actions: {}, type: "image", x: 3, y: 1, w: 1, h: 1, text: "Logo", props: { src: "" }, style: { background: "#111827", foreground: "#9a9ea6" } },
    { id: "web", actions: {}, type: "web", x: 0, y: 2, w: 2, h: 1, text: "Web Chat", style: { background: "#232529", foreground: "#9a9ea6" } },
  ],
};

// ---- plugin widgets: three real workers behind the real sandboxed launcher frame, with a stand-in for the server ----
const GAUGE = `
const { canvas, settings, bindings } = await macroGrid.ready;
const ctx = canvas.getContext("2d");
let target = 0, shown = 0, pressed = 0;
macroGrid.subscribe([bindings.source]);
macroGrid.onVariables((v) => { target = Number(v[bindings.source]) || 0; macroGrid.frame(draw); });
macroGrid.onResize(({ width, height, dpr }) => { canvas.width = width * dpr; canvas.height = height * dpr; macroGrid.frame(draw); });
macroGrid.onPointer((p) => { if (p.phase === "down") { pressed++; macroGrid.frame(draw); } });
canvas.width = 200; canvas.height = 200;
function draw() {
  shown += (target - shown) * 0.2;
  const w = canvas.width, h = canvas.height, r = Math.min(w, h) * 0.38;
  ctx.clearRect(0, 0, w, h);
  ctx.lineWidth = r * 0.2; ctx.lineCap = "round";
  ctx.strokeStyle = "#333"; ctx.beginPath(); ctx.arc(w / 2, h / 2, r, 0.75 * Math.PI, 2.25 * Math.PI); ctx.stroke();
  const end = 0.75 * Math.PI + 1.5 * Math.PI * Math.min(1, Math.max(0, shown / settings.max));
  ctx.strokeStyle = settings.color; ctx.beginPath(); ctx.arc(w / 2, h / 2, r, 0.75 * Math.PI, end); ctx.stroke();
  ctx.fillStyle = "#fff"; ctx.font = (r * 0.5) + "px sans-serif"; ctx.textAlign = "center";
  ctx.fillText(Math.round(shown) + settings.unit, w / 2, h / 2 + r * 0.2);
  ctx.font = (r * 0.25) + "px sans-serif"; ctx.fillText("taps " + pressed, w / 2, h - 8);
  if (Math.abs(target - shown) > 0.1) macroGrid.frame(draw);
}
`;

// Tries everything a widget must not be able to do and draws the outcome.
const PROBE = `
const { canvas } = await macroGrid.ready;
const ctx = canvas.getContext("2d");
canvas.width = 300; canvas.height = 200;
const results = [];
const attempt = async (name, fn) => { try { await fn(); results.push(name + ": REACHED"); } catch (e) { results.push(name + ": blocked"); } };
await attempt("fetch", () => fetch("/index.html"));
await attempt("XMLHttpRequest", () => { const x = new XMLHttpRequest(); x.open("GET", "/index.html"); x.send(); });
await attempt("WebSocket", () => new WebSocket("ws://127.0.0.1:1"));
await attempt("indexedDB", () => indexedDB.open("x"));
await attempt("Worker", () => new Worker("data:,"));
await attempt("importScripts", () => importScripts("http://127.0.0.1/x.js"));
await attempt("eval", () => eval("1+1"));
ctx.fillStyle = "#111"; ctx.fillRect(0, 0, 300, 200);
ctx.fillStyle = "#9fe"; ctx.font = "16px monospace";
results.forEach((line, i) => ctx.fillText(line, 8, 24 + i * 24));
macroGrid.frame(() => {});
`;

const FREEZE = `
const { canvas } = await macroGrid.ready;
const ctx = canvas.getContext("2d");
canvas.width = 200; canvas.height = 100;
ctx.fillStyle = "#622"; ctx.fillRect(0, 0, 200, 100);
ctx.fillStyle = "#fff"; ctx.font = "16px sans-serif"; ctx.fillText("about to freeze...", 10, 50);
setTimeout(() => { for (;;) {} }, 1500);
`;

const SCRIPTS: Record<string, string> = { "asset:gauge": GAUGE, "asset:probe": PROBE, "asset:freeze": FREEZE };

class DemoHost implements PluginWidgetHost {
  readonly mode = "run" as const;
  private listeners = new Map<string, PluginWidgetListener>();
  loadAsset = async (ref: string) => SCRIPTS[ref] ?? "";
  request = async (_id: string, data: unknown) => ({ echo: data });
  run = async (id: string, action: string, _s: unknown, gesture: boolean) => console.log("widget run", id, action, "userGesture:", gesture);
  subscribe = (id: string, names: string[]) => { console.log("subscribe", id, names); this.push(id, this.value); };
  ready = () => undefined;
  reportError = (id: string, message: string) => console.log("widget error", id, message);
  listen = (id: string, l: PluginWidgetListener) => { this.listeners.set(id, l); return () => void this.listeners.delete(id); };
  value = 0;
  push(id: string, v: number) { this.listeners.get(id)?.vars({ "demo.value": v }); }
  pushAll(v: number) { this.value = v; for (const id of this.listeners.keys()) this.push(id, v); }
}

const runtime = new PluginWidgetRuntime();
const host = new DemoHost();
const pluginPage: Page = {
  id: "p2",
  name: "Plugin widgets",
  cols: 4,
  rows: 2,
  widgets: [
    { id: "gauge", actions: {}, type: "plugin-widget", x: 0, y: 0, w: 2, h: 2, style: { background: "#0f172a" },
      props: { plugin: "demo", widget: "gauge", settings: { source: "demo.value", max: 100, color: "#38bdf8", unit: "%" },
        runtime: { name: "Gauge", code: "asset:gauge", fps: 30, interactive: true, verified: true, variables: ["source"] } } },
    { id: "probe", actions: {}, type: "plugin-widget", x: 2, y: 0, w: 2, h: 1, style: { background: "#111" },
      props: { plugin: "demo", widget: "probe", runtime: { name: "Probe", code: "asset:probe", fps: 5, verified: false } } },
    { id: "freeze", actions: {}, type: "plugin-widget", x: 2, y: 1, w: 1, h: 1, style: { background: "#311" },
      props: { plugin: "demo", widget: "freeze", runtime: { name: "Freeze", code: "asset:freeze", fps: 5, verified: true } } },
    { id: "gone", actions: {}, type: "plugin-widget", x: 3, y: 1, w: 1, h: 1, style: { background: "#1c1917", foreground: "#fbbf24" },
      props: { plugin: "gone", widget: "x", runtime: { unavailable: "missing" } } },
  ],
};

function Demo() {
  const [presses, setPresses] = useState<Record<string, number>>({});
  const [values, setValues] = useState<Record<string, number>>({ slider: 30, knob: 50 });
  const [active, setActive] = useState<Record<string, boolean>>({});

  return (
    <div style={{ padding: 16, height: "calc(100vh - 32px)" }}>
      <h3 style={{ marginTop: 0 }}>Renderer demo — stage 3</h3>
      <div style={{ height: "80%", border: "1px solid #35383e", borderRadius: 4 }}>
        <Grid
          page={page}
          renderWidget={(w: Widget) => (
            <WidgetView
              widget={w}
              liveActive={active[w.id]}
              liveValue={values[w.id]}
              onPress={() => setPresses((p) => ({ ...p, [w.id]: (p[w.id] ?? 0) + 1 }))}
              onLongPress={() => console.log("longPress", w.id)}
              onDoubleTap={() => console.log("doubleTap", w.id)}
              onValueChange={(v) => setValues((s) => ({ ...s, [w.id]: v }))}
            />
          )}
        />
      </div>
      <p style={{ fontSize: 12, color: "#9a9ea6" }}>
        Click the toggle, drag the slider/knob. On the "Custom CSS" button width:999px must be sanitized while the gradient/border stay.
      </p>
      <button onClick={() => setActive((a) => ({ ...a, toggle: !a.toggle }))}>Flip the toggle</button>
      <h3>Plugin widgets (real worker, sandboxed launcher frame)</h3>
      <div style={{ height: 260, border: "1px solid #35383e", borderRadius: 4 }}>
        <PluginWidgetContext.Provider value={{ runtime, host }}>
          <Grid page={pluginPage} renderWidget={(w: Widget) => <WidgetView widget={w} />} />
        </PluginWidgetContext.Provider>
      </div>
      <input type="range" min={0} max={100} defaultValue={0} onChange={(e) => host.pushAll(Number(e.target.value))} style={{ width: 300 }} />
      <pre style={{ fontSize: 11 }}>{JSON.stringify({ presses, values }, null, 2)}</pre>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<Demo />);
