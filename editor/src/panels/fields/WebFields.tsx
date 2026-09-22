import type { FieldGroupProps } from "./AppearanceFields";
import { SectionLabel } from "./controls";

/** For "web" (and future "plugin-html") widgets: a URL to embed, not text — this is why it looked identical to a label before. */
export function WebFields({ widget, onChange }: FieldGroupProps) {
  const url = typeof widget.props?.url === "string" ? widget.props.url : "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <SectionLabel>İçerik</SectionLabel>
      <label className="field">
        Sayfa URL'si
        <input
          type="text"
          value={url}
          onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), url: e.target.value }; })}
          placeholder="https://twitch.tv/örnek/chat?parent=..."
        />
      </label>
      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>
        Bu widget şu an düzenleyicide ve telefonda yer tutucu olarak görünür — gerçek gömülü sayfa (iframe/WebView) Aşama 5-6'da eklenecek.
      </p>
    </div>
  );
}
