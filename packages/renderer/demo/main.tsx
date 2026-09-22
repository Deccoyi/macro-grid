import { useState } from "react";
import { createRoot } from "react-dom/client";
import { Grid, WidgetView, type Page, type Widget } from "@macro/renderer";

const page: Page = {
  id: "p1",
  name: "Demo",
  cols: 4,
  rows: 3,
  widgets: [
    { id: "btn", actions: {}, type: "button", x: 0, y: 0, w: 1, h: 1, text: "Kopyala", style: { background: "#1d4ed8", foreground: "#fff" } },
    { id: "gradient", actions: {}, type: "button", x: 1, y: 0, w: 1, h: 1, text: "Özel CSS", style: { foreground: "#fff", radius: 12 },
      customCss: ":host { background: linear-gradient(135deg, #f59e0b, #dc2626); border: 2px solid gold; width: 999px; }" },
    { id: "toggle", actions: {}, type: "toggle", x: 2, y: 0, w: 1, h: 1, text: "Sessiz", style: { background: "#374151", foreground: "#fff" } },
    { id: "label", actions: {}, type: "label", x: 3, y: 0, w: 1, h: 1, text: "CPU\n42%", style: { background: "#1f2937", foreground: "#38bdf8" } },
    { id: "slider", actions: {}, type: "slider", x: 0, y: 1, w: 2, h: 1, text: "Ses", props: { min: 0, max: 100 }, style: { background: "#232529", foreground: "#e6e7ea" } },
    { id: "knob", actions: {}, type: "knob", x: 2, y: 1, w: 1, h: 2, text: "Gain", props: { min: 0, max: 100 }, style: { background: "#232529", foreground: "#e6e7ea" } },
    { id: "image", actions: {}, type: "image", x: 3, y: 1, w: 1, h: 1, text: "Logo", props: { src: "" }, style: { background: "#111827", foreground: "#9a9ea6" } },
    { id: "web", actions: {}, type: "web", x: 0, y: 2, w: 2, h: 1, text: "Twitch Chat", style: { background: "#232529", foreground: "#9a9ea6" } },
  ],
};

function Demo() {
  const [presses, setPresses] = useState<Record<string, number>>({});
  const [values, setValues] = useState<Record<string, number>>({ slider: 30, knob: 50 });
  const [active, setActive] = useState<Record<string, boolean>>({});

  return (
    <div style={{ padding: 16, height: "calc(100vh - 32px)" }}>
      <h3 style={{ marginTop: 0 }}>Renderer demo — Aşama 3</h3>
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
        Toggle'a tıkla, slider/knob'u sürükle. "Özel CSS" butonunda width:999px sanitize edilmeli, gradient/border kalmalı.
      </p>
      <button onClick={() => setActive((a) => ({ ...a, toggle: !a.toggle }))}>Toggle'ı çevir</button>
      <pre style={{ fontSize: 11 }}>{JSON.stringify({ presses, values }, null, 2)}</pre>
    </div>
  );
}

createRoot(document.getElementById("root")!).render(<Demo />);
