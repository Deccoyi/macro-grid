import type { Widget, WidgetStyle } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { DynamicFieldLabel } from "../dynamic/DynamicFieldLabel";
import { ColorField, Field, FieldGrid, NumberInput, Seg } from "./controls";

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
  const bolt = (label: string, propertyKey: string, resultKind?: Parameters<typeof DynamicFieldLabel>[0]["resultKind"]) => (
    <DynamicFieldLabel label={label} propertyKey={propertyKey} widget={widget} variableCatalog={variableCatalog} onChange={onChange} resultKind={resultKind} />
  );

  const animationOptions = [
    { value: "none" as const, label: t("fields.appearance.animation.none") },
    { value: "blink" as const, label: t("fields.appearance.animation.blink") },
    { value: "pulse" as const, label: t("fields.appearance.animation.pulse") },
  ];

  return (
    <>
      <FieldGrid cols={2}>
        <Field single label={t("fields.appearance.background")} action={bolt(t("fields.appearance.background"), "style.background")}>
          <ColorField value={style.background} onChange={(v) => set((s) => { s.background = v; })} />
        </Field>
        <Field single label={t("fields.appearance.foreground")} action={bolt(t("fields.appearance.foreground"), "style.foreground")}>
          <ColorField value={style.foreground} onChange={(v) => set((s) => { s.foreground = v; })} />
        </Field>
      </FieldGrid>

      <FieldGrid cols={3}>
        <Field single label={t("fields.appearance.border")} action={bolt(t("fields.appearance.border"), "style.borderColor")}>
          <ColorField value={style.borderColor} onChange={(v) => set((s) => { s.borderColor = v; })} />
        </Field>
        <Field single label={t("fields.appearance.borderWidth")}>
          <NumberInput min={0} max={12} unit="px" value={style.borderWidth ?? 0} onChange={(v) => set((s) => { s.borderWidth = v; })} />
        </Field>
        <Field single label={t("fields.appearance.radius")}>
          <NumberInput min={0} max={48} unit="px" value={style.radius ?? 8} onChange={(v) => set((s) => { s.radius = v; })} />
        </Field>
      </FieldGrid>

      <Field
        single
        label={t("fields.appearance.animation")}
        action={bolt(
          t("fields.appearance.animation"),
          "style.animation",
          { select: animationOptions.map((o) => ({ value: o.value, label: o.value === "pulse" ? t("fields.appearance.animation.pulseLong") : o.label })) },
        )}
      >
        <Seg value={style.animation ?? "none"} onChange={(v) => set((s) => { s.animation = v; })} options={animationOptions} />
      </Field>
    </>
  );
}
