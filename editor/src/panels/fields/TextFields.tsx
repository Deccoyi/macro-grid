import { useRef } from "react";
import { AlignCenter, AlignLeft, AlignRight, AlignVerticalJustifyCenter, AlignVerticalJustifyEnd, AlignVerticalJustifyStart } from "lucide-react";
import type { Align, IconPosition, VAlign, WidgetStyle } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { IconPicker, iconToDataUri } from "../IconPicker";
import { VariablePicker } from "../VariablePicker";
import type { FieldGroupProps } from "./AppearanceFields";
import { Seg, SectionLabel } from "./controls";

export interface TextFieldsProps extends FieldGroupProps {
  variableCatalog: VariableInfo[];
  /** Set false to hide the icon picker (image widgets show their own picture instead). */
  showIcon?: boolean;
}

/** Text content + how it's laid out: for button/toggle/label, whose whole point is showing text. */
export function TextFields({ widget, onChange, variableCatalog, showIcon = true }: TextFieldsProps) {
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
      <SectionLabel>İçerik</SectionLabel>
      <label className="field">
        Metin
        <textarea
          ref={textRef}
          rows={2}
          value={widget.text ?? ""}
          onChange={(e) => onChange((w) => { w.text = e.target.value; })}
          placeholder="Sabit metin, veya sağdan bir değişken ekleyin"
        />
      </label>
      <VariablePicker catalog={variableCatalog} onInsert={insertVariable} />

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
        <label className="field">
          Yatay
          <Seg<Align>
            value={style.align ?? "center"}
            onChange={(v) => set((s) => { s.align = v; })}
            options={[
              { value: "left", label: <AlignLeft size={14} />, title: "Sol" },
              { value: "center", label: <AlignCenter size={14} />, title: "Orta" },
              { value: "right", label: <AlignRight size={14} />, title: "Sağ" },
            ]}
          />
        </label>
        <label className="field">
          Dikey
          <Seg<VAlign>
            value={style.vAlign ?? "middle"}
            onChange={(v) => set((s) => { s.vAlign = v; })}
            options={[
              { value: "top", label: <AlignVerticalJustifyStart size={14} />, title: "Üst" },
              { value: "middle", label: <AlignVerticalJustifyCenter size={14} />, title: "Orta" },
              { value: "bottom", label: <AlignVerticalJustifyEnd size={14} />, title: "Alt" },
            ]}
          />
        </label>
      </div>

      <label className="field">
        Font boyutu (px)
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
            İkon
            <IconPicker
              value={style.icon}
              color={style.foreground}
              onChange={(icon, iconName) => set((s) => { s.icon = icon; s.iconName = iconName; })}
            />
          </label>
          {style.icon && (
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
              <label className="field">
                İkon boyutu (px)
                <input type="number" min={12} max={96} value={style.iconSize ?? 28} onChange={(e) => set((s) => { s.iconSize = Number(e.target.value); })} />
              </label>
              <label className="field">
                İkon konumu
                <select value={style.iconPosition ?? "top"} onChange={(e) => set((s) => { s.iconPosition = e.target.value as IconPosition; })}>
                  <option value="top">Metnin üstünde</option>
                  <option value="bottom">Metnin altında</option>
                  <option value="left">Metnin solunda</option>
                  <option value="right">Metnin sağında</option>
                </select>
              </label>
              <label className="field">
                İkon rengi
                <input
                  type="color"
                  disabled={!style.iconName}
                  title={style.iconName ? undefined : "Bu ikon eski bir sürümde eklenmiş, rengini değiştirmek için yeniden seçin"}
                  value={/^#([0-9a-f]{6})$/i.test(style.foreground ?? "") ? style.foreground : "#e6e7ea"}
                  onChange={async (e) => {
                    if (!style.iconName) return;
                    const uri = await iconToDataUri(style.iconName, e.target.value);
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
