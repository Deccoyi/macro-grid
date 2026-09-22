import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";

/** For "web" (and future "plugin-html") widgets: a URL to embed, not text — this is why it looked identical to a label before. */
export function WebFields({ widget, onChange }: FieldGroupProps) {
  const { t } = useT();
  const url = typeof widget.props?.url === "string" ? widget.props.url : "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <label className="field">
        {t("fields.web.url")}
        <input
          type="text"
          value={url}
          onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), url: e.target.value }; })}
          placeholder={t("fields.web.urlPlaceholder")}
        />
      </label>
      <p style={{ fontSize: 11, color: "var(--ms-text-secondary)", margin: 0 }}>
        {t("fields.web.note")}
      </p>
    </div>
  );
}
