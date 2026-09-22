import type { ActionBinding, Widget, WidgetEventName, Page } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { ActionEditor } from "./ActionEditor";
import { CssEditor } from "./CssEditor";
import { AppearanceFields } from "./fields/AppearanceFields";
import { SectionLabel } from "./fields/controls";
import { ImageFields } from "./fields/ImageFields";
import { RangeFields } from "./fields/RangeFields";
import { TextFields } from "./fields/TextFields";
import { WebFields } from "./fields/WebFields";

export interface InspectorProps {
  widget: Widget | null;
  pages: Page[];
  profiles: ProfileSummary[];
  actions: ActionInfo[];
  variableCatalog: VariableInfo[];
  onChange: (fn: (widget: Widget) => void) => void;
  onDelete: () => void;
}

export function Inspector({ widget, pages, profiles, actions, variableCatalog, onChange, onDelete }: InspectorProps) {
  if (!widget) {
    return (
      <div style={{ padding: 14, color: "var(--ms-text-secondary)", fontSize: 12 }}>
        Düzenlemek için bir widget seçin.
      </div>
    );
  }

  return (
    <div style={{ padding: 12, display: "flex", flexDirection: "column", gap: 14, overflowY: "auto", height: "100%" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase" }}>{typeLabel(widget.type)}</span>
        <button className="ghost" onClick={onDelete} style={{ color: "var(--ms-danger)" }}>Sil</button>
      </div>

      <AppearanceFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />

      <hr className="sep" />

      {renderTypeFields(widget, onChange, variableCatalog)}

      <hr className="sep" />

      <CssEditor value={widget.customCss} onChange={(css) => onChange((w) => { w.customCss = css; })} />

      {widget.type !== "label" && (
        <>
          <hr className="sep" />
          <SectionLabel>Aksiyonlar</SectionLabel>
          <ActionEditor
            widget={widget}
            actions={actions}
            pages={pages}
            profiles={profiles}
            onChange={(event: WidgetEventName, bindings: ActionBinding[]) =>
              onChange((w) => {
                if (bindings.length === 0) delete w.actions[event];
                else w.actions[event] = bindings;
              })
            }
          />
        </>
      )}
    </div>
  );
}

function renderTypeFields(widget: Widget, onChange: InspectorProps["onChange"], variableCatalog: VariableInfo[]) {
  switch (widget.type) {
    case "image":
      return <ImageFields widget={widget} onChange={onChange} />;
    case "web":
    case "plugin-html":
      return <WebFields widget={widget} onChange={onChange} />;
    case "slider":
    case "knob":
      return <RangeFields widget={widget} onChange={onChange} />;
    case "button":
    case "toggle":
    case "label":
    default:
      return <TextFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />;
  }
}

function typeLabel(type: Widget["type"]): string {
  switch (type) {
    case "button": return "Buton";
    case "toggle": return "Toggle";
    case "label": return "Etiket";
    case "image": return "Görsel";
    case "slider": return "Slider";
    case "knob": return "Knob";
    case "web": return "Web";
    case "plugin-html": return "Plugin";
    default: return type;
  }
}
