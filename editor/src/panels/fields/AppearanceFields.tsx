import type { Widget, WidgetStyle } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { DynamicFieldLabel } from "../dynamic/DynamicFieldLabel";
import { ColorField, Seg } from "./controls";

export interface FieldGroupProps {
  widget: Widget;
  onChange: (fn: (widget: Widget) => void) => void;
}

interface AppearanceFieldsProps extends FieldGroupProps {
  variableCatalog: VariableInfo[];
}

/** Background/foreground/border/radius/animation — every widget type has a box, so these always apply
 * and always come first, in this order, for every widget type (the panel's "common language"). */
export function AppearanceFields({ widget, onChange, variableCatalog }: AppearanceFieldsProps) {
  const { t } = useT();
  const style = widget.style ?? {};
  const set = (fn: (s: WidgetStyle) => void) =>
    onChange((w) => {
      w.style = w.style ?? {};
      fn(w.style);
    });

  const animationOptions = [
    { value: "none" as const, label: t("fields.appearance.animation.none") },
    { value: "blink" as const, label: t("fields.appearance.animation.blink") },
    { value: "pulse" as const, label: t("fields.appearance.animation.pulse") },
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
        <label className="field">
          <DynamicFieldLabel label={t("fields.appearance.background")} propertyKey="style.background" widget={widget} variableCatalog={variableCatalog} onChange={onChange} />
          <ColorField value={style.background} onChange={(v) => set((s) => { s.background = v; })} />
        </label>
        <label className="field">
          <DynamicFieldLabel label={t("fields.appearance.foreground")} propertyKey="style.foreground" widget={widget} variableCatalog={variableCatalog} onChange={onChange} />
          <ColorField value={style.foreground} onChange={(v) => set((s) => { s.foreground = v; })} />
        </label>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 8 }}>
        <label className="field">
          <DynamicFieldLabel label={t("fields.appearance.border")} propertyKey="style.borderColor" widget={widget} variableCatalog={variableCatalog} onChange={onChange} />
          <ColorField value={style.borderColor} onChange={(v) => set((s) => { s.borderColor = v; })} />
        </label>
        <label className="field">
          {t("fields.appearance.borderWidth")}
          <input type="number" min={0} max={12} value={style.borderWidth ?? 0} onChange={(e) => set((s) => { s.borderWidth = Number(e.target.value); })} />
        </label>
        <label className="field">
          {t("fields.appearance.radius")}
          <input type="number" min={0} max={48} value={style.radius ?? 8} onChange={(e) => set((s) => { s.radius = Number(e.target.value); })} />
        </label>
      </div>

      <label className="field">
        <DynamicFieldLabel
          label={t("fields.appearance.animation")}
          propertyKey="style.animation"
          widget={widget}
          variableCatalog={variableCatalog}
          onChange={onChange}
          resultKind={{ select: animationOptions.map((o) => ({ value: o.value, label: o.value === "pulse" ? t("fields.appearance.animation.pulseLong") : o.label })) }}
        />
        <Seg
          value={style.animation ?? "none"}
          onChange={(v) => set((s) => { s.animation = v; })}
          options={animationOptions}
        />
      </label>
    </div>
  );
}
