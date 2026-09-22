import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";

/** For "image" widgets: a picture (props.src) with an optional caption underneath. */
export function ImageFields({ widget, onChange }: FieldGroupProps) {
  const { t } = useT();
  const src = typeof widget.props?.src === "string" ? widget.props.src : "";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <label className="field">
        {t("fields.image.url")}
        <input
          type="text"
          value={src}
          onChange={(e) => onChange((w) => { w.props = { ...(w.props ?? {}), src: e.target.value }; })}
          placeholder={t("fields.image.urlPlaceholder")}
        />
      </label>
      <label className="field">
        {t("fields.image.caption")}
        <input
          type="text"
          value={widget.text ?? ""}
          onChange={(e) => onChange((w) => { w.text = e.target.value; })}
        />
      </label>
    </div>
  );
}
