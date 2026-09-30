/**
 * The sandboxed frame that holds one plugin widget: its canvas and its worker (docs/design/plugin-widgets.md).
 *
 * A worker made by the app page would have the app's origin: its IndexedDB, its caches and same-origin requests. A worker made inside an
 * opaque-origin frame inherits that frame's opaque origin and its strict content policy instead. So the app only ever creates this frame;
 * the frame makes its own canvas, creates the worker from a `blob:` URL and hands it the canvas and one MessagePort. After that the worker
 * talks to the renderer directly and the frame does nothing more.
 *
 * The canvas is made inside the frame, not in the app page, on purpose: desktop browsers run a sandboxed frame in its own process, and a canvas
 * handed from another process to a worker stops that worker at its first drawing call. Canvas, worker and frame share one process this way, and
 * removing the frame ends the worker at once (nothing has to answer a stop message first).
 *
 * `allow-same-origin` must never be added to the sandbox: it would give the frame (and its worker) the app's origin back.
 */
export const FRAME_SANDBOX = "allow-scripts";

/** No network at all (`connect-src 'none'`, inherited by the blob worker), scripts only from blobs the frame makes itself, WebAssembly allowed. */
export const FRAME_CSP = "default-src 'none'; script-src blob: 'unsafe-inline' 'wasm-unsafe-eval'; worker-src blob:; connect-src 'none'; style-src 'unsafe-inline'";

/** One running widget frame. */
export interface WidgetFrame {
  /** Removes the frame, which ends the worker and frees the canvas. */
  terminate(): void;
}

/** Starts a widget: makes the frame inside `container`, runs `script` in a worker in it and gives the worker `port`. Tests use a fake. */
export type FrameStarter = (id: string, container: HTMLElement, script: string, port: MessagePort) => Promise<WidgetFrame>;

const FRAME_SCRIPT = `
var worker = null;
addEventListener("message", function (e) {
  var m = e.data;
  if (!m || m.type !== "start" || worker) return;
  try {
    var canvas = document.getElementById("c");
    var offscreen = canvas.transferControlToOffscreen();
    var url = URL.createObjectURL(new Blob([m.script], { type: "text/javascript" }));
    worker = new Worker(url);
    URL.revokeObjectURL(url);
    worker.onerror = function (ev) { parent.postMessage({ type: "failed", message: String(ev.message || "The widget script could not start") }, "*"); };
    worker.postMessage({ type: "start", canvas: offscreen, port: m.port }, [offscreen, m.port]);
    parent.postMessage({ type: "started" }, "*");
  } catch (err) {
    parent.postMessage({ type: "failed", message: String(err && err.message) }, "*");
  }
});
parent.postMessage({ type: "frame-ready" }, "*");
`;

export function frameSrcdoc(): string {
  return `<meta http-equiv="Content-Security-Policy" content="${FRAME_CSP}"><meta name="color-scheme" content="light"><style>html,body{margin:0;height:100%;background:transparent;overflow:hidden}canvas{display:block;width:100%;height:100%}</style><canvas id="c"></canvas><script>${FRAME_SCRIPT}<\/script>`;
}

/**
 * The real starter: an iframe filling the container, with pointer events off so the container (and the renderer) receives every touch. Rejects if
 * the frame or the worker does not report in within 3 seconds.
 */
export const startWidgetFrame: FrameStarter = (id, container, script, port) => {
  const doc = container.ownerDocument;
  const win = doc.defaultView!;
  const frame = doc.createElement("iframe");
  frame.setAttribute("sandbox", FRAME_SANDBOX);
  frame.setAttribute("aria-hidden", "true");
  frame.setAttribute("data-widget-frame", id);
  frame.tabIndex = -1;
  frame.style.cssText = "position:absolute;inset:0;width:100%;height:100%;border:0;background:transparent;pointer-events:none;color-scheme:light";

  return new Promise<WidgetFrame>((resolve, reject) => {
    let settled = false;
    const finish = (error?: Error) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      win.removeEventListener("message", onMessage);
      if (error) {
        frame.remove();
        reject(error);
      } else {
        resolve({
          terminate() {
            frame.remove();
          },
        });
      }
    };

    const onMessage = (e: MessageEvent) => {
      // Only the frame we made, and only the three shapes it sends.
      if (e.source !== frame.contentWindow || !e.data || typeof e.data !== "object") return;
      const { type, message } = e.data as { type?: string; message?: string };
      if (type === "frame-ready") frame.contentWindow?.postMessage({ type: "start", script, port }, "*", [port]);
      else if (type === "started") finish();
      else if (type === "failed") finish(new Error(message ?? "The widget could not start"));
    };
    win.addEventListener("message", onMessage);
    const timer = setTimeout(() => finish(new Error("The widget did not start in time")), 3000);

    frame.srcdoc = frameSrcdoc();
    container.appendChild(frame);
  });
};
