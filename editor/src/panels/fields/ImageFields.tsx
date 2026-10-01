import { useT } from "../../i18n/I18nContext";
import type { FieldGroupProps } from "./AppearanceFields";
import { Field, TextInput } from "./controls";

/** For "image" widgets: a picture (props.src) with an optional caption underneath. */
export function ImageFields({ widget, onChange }: FieldGroupProps) {
  const { t } = useT();
  const src = typeof widget.props?.src === "string" ? widget.props.src : "";

  return (
    <>
      <Field label={t("fields.image.url")}>
        <TextInput
          value={src}
          onChange={(v) => onChange((w) => { w.props = { ...(w.props ?? {}), src: v }; })}
          placeholder={t("fields.image.urlPlaceholder")}
        />
      </Field>
      <Field label={t("fields.image.caption")}>
        <TextInput value={widget.text ?? ""} onChange={(v) => onChange((w) => { w.text = v; })} />
      </Field>
    </>
  );
}
