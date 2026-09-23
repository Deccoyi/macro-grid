import { useRef } from "react";
import { AlignCenter, AlignLeft, AlignRight, AlignVerticalJustifyCenter, AlignVerticalJustifyEnd, AlignVerticalJustifyStart } from "lucide-react";
import type { Align, IconPosition, VAlign, WidgetStyle } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { IconPicker, iconToDataUri } from "../IconPicker";
import { VariablePicker } from "../VariablePicker";
import type { FieldGroupProps } from "./AppearanceFields";
import { ColorField, Seg } from "./controls";

export interface TextFieldsProps extends FieldGroupProps {
  variableCatalog: VariableInfo[];
  /** Set false to hide the icon picker (image widgets show their own picture instead). */
  showIcon?: boolean;
}

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
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div className="field">
        <div style={{ display: "flex", alignItems: "center" }}>
          <span style={{ flex: 1 }}>{t("fields.text.label")}</span>
          <VariablePicker
            catalog={variableCatalog}
            onInsert={insertVariable}
            renderTrigger={(open) => (
              <button type="button" className="ghost" onClick={open} style={{ padding: "2px 6px", fontSize: 11 }}>
                {t("variable.add")}
              </button>
            )}
          />
        </div>
        <textarea
          ref={textRef}
          rows={2}
          value={widget.text ?? ""}
          onChange={(e) => onChange((w) => { w.text = e.target.value; })}
          placeholder={t("fields.text.placeholder")}
        />
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
        <label className="field">
          {t("fields.text.horizontal")}
          <Seg<Align>
            value={style.align ?? "center"}
            onChange={(v) => set((s) => { s.align = v; })}
            options={[
              { value: "left", label: <AlignLeft size={14} />, title: t("fields.text.align.left") },
              { value: "center", label: <AlignCenter size={14} />, title: t("fields.text.align.center") },
              { value: "right", label: <AlignRight size={14} />, title: t("fields.text.align.right") },
            ]}
          />
        </label>
        <label className="field">
          {t("fields.text.vertical")}
          <Seg<VAlign>
            value={style.vAlign ?? "middle"}
            onChange={(v) => set((s) => { s.vAlign = v; })}
            options={[
              { value: "top", label: <AlignVerticalJustifyStart size={14} />, title: t("fields.text.valign.top") },
              { value: "middle", label: <AlignVerticalJustifyCenter size={14} />, title: t("fields.text.valign.middle") },
              { value: "bottom", label: <AlignVerticalJustifyEnd size={14} />, title: t("fields.text.valign.bottom") },
            ]}
          />
        </label>
      </div>

      <label className="field">
        {t("fields.text.fontSize")}
        <input
          type="number"
          min={8}
          max={72}
          value={style.fontSize ?? 16}
          onChange={(e) => set((s) => { s.fontSize = Number(e.target.value); })}
        />
      </label>

      {showIcon && (
        <>
          <label className="field">
            {t("fields.text.icon")}
            <IconPicker
              value={style.icon}
              color={style.foreground}
              onChange={(icon, iconName) => set((s) => { s.icon = icon; s.iconName = iconName; })}
            />
          </label>
          {style.icon && (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
                <label className="field">
                  {t("fields.text.iconSize")}
                  <div style={{ display: "flex", alignItems: "center", gap: 5 }}>
                    <input type="number" min={12} max={96} value={style.iconSize ?? 28} onChange={(e) => set((s) => { s.iconSize = Number(e.target.value); })} />
                    <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", flexShrink: 0 }}>px</span>
                  </div>
                </label>
                <label className="field">
                  {t("fields.text.iconPosition")}
                  <select value={style.iconPosition ?? "top"} onChange={(e) => set((s) => { s.iconPosition = e.target.value as IconPosition; })}>
                    <option value="top">{t("fields.text.iconPosition.top")}</option>
                    <option value="bottom">{t("fields.text.iconPosition.bottom")}</option>
                    <option value="left">{t("fields.text.iconPosition.left")}</option>
                    <option value="right">{t("fields.text.iconPosition.right")}</option>
                  </select>
                </label>
              </div>
              <label className="field">
                {t("fields.text.iconColor")}
                <ColorField
                  disabled={!style.iconName}
                  title={style.iconName ? undefined : t("fields.text.iconColorHint")}
                  value={/^#([0-9a-f]{6})$/i.test(style.foreground ?? "") ? style.foreground : "#e6e7ea"}
                  onChange={async (hex) => {
                    if (!style.iconName) return;
                    const uri = await iconToDataUri(style.iconName, hex);
                    if (uri) set((s) => { s.icon = uri; });
                  }}
                />
              </label>
            </div>
          )}
        </>
      )}
    </div>
  );
}
