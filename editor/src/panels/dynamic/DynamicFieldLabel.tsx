import { useState } from "react";
import { Zap } from "lucide-react";
import type { Widget } from "@macro/renderer";
import type { VariableInfo } from "../../api/types";
import { useT } from "../../i18n/I18nContext";
import { DynamizeModal, type ResultKind } from "./DynamizeModal";

export interface DynamicFieldLabelProps {
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

/** A field label with a small lightning-bolt button that opens the "make this depend on a variable" modal; lit up (accent color) once dynamized. */
export function DynamicFieldLabel({ label, propertyKey, widget, variableCatalog, onChange, resultKind, iconColor }: DynamicFieldLabelProps) {
  const { t } = useT();
  const [open, setOpen] = useState(false);
  const binding = widget.dynamic?.[propertyKey];
  const isDynamic = binding !== undefined;

  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
      {label}
      <button
        type="button"
        className="ghost"
        title={isDynamic ? t("dynamic.editTitle") : t("dynamic.addTitle")}
        onClick={() => setOpen(true)}
        style={{ padding: "0 4px", lineHeight: 1, display: "inline-flex", color: isDynamic ? "var(--ms-accent)" : "var(--ms-text-secondary)" }}
      >
        <Zap size={12} />
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
    </span>
  );
}
