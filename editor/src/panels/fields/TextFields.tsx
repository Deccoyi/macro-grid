import { forwardRef, useRef } from "react";
import { AlignCenter, AlignLeft, AlignRight, AlignVerticalJustifyCenter, AlignVerticalJustifyEnd, AlignVerticalJustifyStart, Variable } from "lucide-react";
import type { Align, IconPosition, VAlign, WidgetStyle } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { IconPicker, iconToDataUri } from "../IconPicker";
import { DynamicFieldLabel } from "../dynamic/DynamicFieldLabel";
import { VariablePicker } from "../VariablePicker";
import type { FieldGroupProps } from "./AppearanceFields";
import { ColorField, Field, FieldGrid, NumberInput, SelectInput, Seg, useFieldId } from "./controls";

interface TextFieldsProps extends FieldGroupProps {
  variableCatalog: VariableInfo[];
  /** Set false to hide the icon picker (image widgets show their own picture instead). */
  showIcon?: boolean;
}

/** The multi-line text of a button, labelled by the nearest Field. */
const TextArea = forwardRef<HTMLTextAreaElement, { value: string; onChange: (v: string) => void; placeholder?: string }>(function TextArea({ value, onChange, placeholder }, ref) {
  const id = useFieldId();
  return <textarea ref={ref} id={id} rows={2} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} />;
});

/** Text content + how it's laid out: for button/toggle/label, whose whole point is showing text. */
export function TextFields({ widget, onChange, variableCatalog, showIcon = true }: TextFieldsProps) {
  const { t } = useT();
  const style = widget.style ?? {};
  const textRef = useRef<HTMLTextAreaElement | null>(null);
  const set = (fn: (s: WidgetStyle) => void) =>
    onChange((w) => {
      w.style = w.style ?? {};
      fn(w.style);
    });

  const insertVariable = (token: string) => {
    const el = textRef.current;
    const current = widget.text ?? "";
    if (!el) {
      onChange((w) => { w.text = current + token; });
      return;
    }
    const start = el.selectionStart ?? current.length;
    const end = el.selectionEnd ?? current.length;
    const next = current.slice(0, start) + token + current.slice(end);
    onChange((w) => { w.text = next; });
    requestAnimationFrame(() => {
      el.focus();
      el.setSelectionRange(start + token.length, start + token.length);
    });
  };

  return (
    <>
      <Field
        label={t("fields.text.label")}
        action={
          <>
            <VariablePicker
              catalog={variableCatalog}
              onInsert={insertVariable}
              renderTrigger={(open) => (
                <button type="button" className="ghost" onClick={open}>
                  <Variable size={12} />
                  {t("variable.add")}
                </button>
              )}
            />
            <DynamicFieldLabel label={t("fields.text.label")} propertyKey="text" widget={widget} variableCatalog={variableCatalog} onChange={onChange} resultKind="text" />
          </>
        }
      >
        <TextArea ref={textRef} value={widget.text ?? ""} onChange={(v) => onChange((w) => { w.text = v; })} placeholder={t("fields.text.placeholder")} />
      </Field>

      <FieldGrid cols={2}>
        <Field single label={t("fields.text.horizontal")}>
          <Seg<Align>
            value={style.align ?? "center"}
            onChange={(v) => set((s) => { s.align = v; })}
            options={[
              { value: "left", label: <AlignLeft size={14} />, title: t("fields.text.align.left") },
              { value: "center", label: <AlignCenter size={14} />, title: t("fields.text.align.center") },
              { value: "right", label: <AlignRight size={14} />, title: t("fields.text.align.right") },
            ]}
          />
        </Field>
        <Field single label={t("fields.text.vertical")}>
          <Seg<VAlign>
            value={style.vAlign ?? "middle"}
            onChange={(v) => set((s) => { s.vAlign = v; })}
            options={[
              { value: "top", label: <AlignVerticalJustifyStart size={14} />, title: t("fields.text.valign.top") },
              { value: "middle", label: <AlignVerticalJustifyCenter size={14} />, title: t("fields.text.valign.middle") },
              { value: "bottom", label: <AlignVerticalJustifyEnd size={14} />, title: t("fields.text.valign.bottom") },
            ]}
          />
        </Field>
      </FieldGrid>

      <Field single label={t("fields.text.fontSize")}>
        <NumberInput min={8} max={72} unit="px" value={style.fontSize ?? 16} onChange={(v) => set((s) => { s.fontSize = v; })} />
      </Field>

      {showIcon && (
        <>
          <Field
            label={t("fields.text.icon")}
            action={<DynamicFieldLabel label={t("fields.text.icon")} propertyKey="style.icon" widget={widget} variableCatalog={variableCatalog} onChange={onChange} resultKind="icon" iconColor={style.foreground} />}
          >
            <IconPicker
              value={style.icon}
              color={style.foreground}
              onChange={(icon, iconName) => set((s) => { s.icon = icon; s.iconName = iconName; })}
            />
          </Field>
          {(style.icon || widget.dynamic?.["style.icon"]) && (
            <>
              <FieldGrid cols={2}>
                <Field single label={t("fields.text.iconSize")}>
                  <NumberInput min={12} max={96} unit="px" value={style.iconSize ?? 28} onChange={(v) => set((s) => { s.iconSize = v; })} />
                </Field>
                <Field single label={t("fields.text.iconPosition")}>
                  <SelectInput value={style.iconPosition ?? "top"} onChange={(v) => set((s) => { s.iconPosition = v as IconPosition; })}>
                    <option value="top">{t("fields.text.iconPosition.top")}</option>
                    <option value="bottom">{t("fields.text.iconPosition.bottom")}</option>
                    <option value="left">{t("fields.text.iconPosition.left")}</option>
                    <option value="right">{t("fields.text.iconPosition.right")}</option>
                  </SelectInput>
                </Field>
              </FieldGrid>
              <Field single label={t("fields.text.iconColor")} hint={style.iconName ? undefined : t("fields.text.iconColorHint")}>
                <ColorField
                  disabled={!style.iconName}
                  value={/^#([0-9a-f]{6})$/i.test(style.foreground ?? "") ? style.foreground : "#e6e7ea"}
                  onChange={async (hex) => {
                    if (!style.iconName) return;
                    const uri = await iconToDataUri(style.iconName, hex);
                    if (uri) set((s) => { s.icon = uri; });
                  }}
                />
              </Field>
            </>
          )}
        </>
      )}
    </>
  );
}
