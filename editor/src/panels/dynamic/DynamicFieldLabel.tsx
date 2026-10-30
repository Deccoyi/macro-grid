import { useState } from "react";
import { Zap } from "lucide-react";
import type { Widget } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { DynamizeModal, type ResultKind } from "./DynamizeModal";

interface DynamicFieldLabelProps {
  label: string;
  /** Dotted path into the widget, e.g. "style.background" — matches server DynamizableProperties. */
  propertyKey: string;
  widget: Widget;
  variableCatalog: VariableInfo[];
  onChange: (fn: (widget: Widget) => void) => void;
  /** What kind of value each rule resolves to; defaults to a color picker. */
  resultKind?: ResultKind;
  /** For the "icon" result kind: the color the icon choices are baked with. */
  iconColor?: string;
}

/** The "make this depend on a variable" bolt of a field: the last action in the field's label row (pass it as `action` of Field). The label text itself is
 * drawn by Field; `label` only names the property in the modal. Lit up (accent color) once dynamized. */
export function DynamicFieldLabel({ label, propertyKey, widget, variableCatalog, onChange, resultKind, iconColor }: DynamicFieldLabelProps) {
  const { t } = useT();
  const [open, setOpen] = useState(false);
  const binding = widget.dynamic?.[propertyKey];
  const isDynamic = binding !== undefined;

  return (
    <>
      <button
        type="button"
        className="ghost"
        title={isDynamic ? t("dynamic.editTitle") : t("dynamic.addTitle")}
        aria-label={isDynamic ? t("dynamic.editTitle") : t("dynamic.addTitle")}
        onClick={() => setOpen(true)}
        style={{ color: isDynamic ? "var(--ms-accent)" : "var(--ms-text-secondary)" }}
      >
        <Zap size={12} fill={isDynamic ? "currentColor" : "none"} />
      </button>
      {open && (
        <DynamizeModal
          propertyLabel={label}
          binding={binding}
          resultKind={resultKind}
          iconColor={iconColor}
          variableCatalog={variableCatalog}
          onClose={() => setOpen(false)}
          onSave={(next) =>
            onChange((w) => {
              w.dynamic = w.dynamic ?? {};
              if (next) w.dynamic[propertyKey] = next;
              else delete w.dynamic[propertyKey];
            })
          }
        />
      )}
    </>
  );
}
