import type { ActionBinding, Widget, WidgetEventName, Page } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { ActionEditor } from "./ActionEditor";
import { CssEditor } from "./CssEditor";
import { AppearanceFields } from "./fields/AppearanceFields";
import { CollapsibleSection, Seg, SectionLabel } from "./fields/controls";
import { ImageFields } from "./fields/ImageFields";
import { RangeFields } from "./fields/RangeFields";
import { TextFields } from "./fields/TextFields";
import { WebFields } from "./fields/WebFields";

export interface InspectorProps {
  selectedWidgets: Widget[];
  page: Page;
  pages: Page[];
  profiles: ProfileSummary[];
  actions: ActionInfo[];
  variableCatalog: VariableInfo[];
  onChange: (fn: (widget: Widget) => void) => void;
  onDelete: () => void;
  onDeleteSelected: () => void;
  onDuplicateSelected: () => void;
  onMoveCopySelected: () => void;
  onRenamePage: (pageId: string, name: string) => void;
  onSetPageGrid: (pageId: string, cols: number, rows: number) => void;
  onSetPageGap: (pageId: string, gap: number) => void;
  onSetPagePadding: (pageId: string, padding: number) => void;
  onSetPageAlignment: (pageId: string, alignment: "start" | "center" | "end") => void;
  onDuplicatePage: (pageId: string) => void;
  onDeletePage: (pageId: string) => void;
  canDeletePage: boolean;
}

export function Inspector({
  selectedWidgets,
  page,
  pages,
  profiles,
  actions,
  variableCatalog,
  onChange,
  onDelete,
  onDeleteSelected,
  onDuplicateSelected,
  onMoveCopySelected,
  onRenamePage,
  onSetPageGrid,
  onSetPageGap,
  onSetPagePadding,
  onSetPageAlignment,
  onDuplicatePage,
  onDeletePage,
  canDeletePage,
}: InspectorProps) {
  const { t } = useT();

  if (selectedWidgets.length === 0) {
    return (
      <PageProperties
        page={page}
        onRename={onRenamePage}
        onSetGrid={onSetPageGrid}
        onSetGap={onSetPageGap}
        onSetPadding={onSetPagePadding}
        onSetAlignment={onSetPageAlignment}
        onDuplicate={onDuplicatePage}
        onDelete={onDeletePage}
        canDelete={canDeletePage}
      />
    );
  }

  if (selectedWidgets.length > 1) {
    return (
      <div style={{ padding: 12, display: "flex", flexDirection: "column", gap: 14 }}>
        <SectionLabel>{t("widget.selected.count", String(selectedWidgets.length))}</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <button className="ghost" onClick={onDuplicateSelected}>{t("widget.selected.duplicate")}</button>
          <button className="ghost" onClick={onMoveCopySelected}>{t("widget.selected.moveCopy")}</button>
          <button className="ghost" style={{ color: "var(--ms-danger)" }} onClick={onDeleteSelected}>{t("widget.selected.delete")}</button>
        </div>
      </div>
    );
  }

  const widget = selectedWidgets[0]!;

  return (
    <div style={{ padding: 12, display: "flex", flexDirection: "column", gap: 14, overflowY: "auto", height: "100%" }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ fontSize: 11, color: "var(--ms-text-secondary)", textTransform: "uppercase" }}>{typeLabel(widget.type, t)}</span>
        <button className="ghost" onClick={onDelete} style={{ color: "var(--ms-danger)" }}>{t("widget.delete")}</button>
      </div>

      <CollapsibleSection id="appearance" label={t("fields.appearance.title")}>
        <AppearanceFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />
      </CollapsibleSection>

      <hr className="sep" />

      <CollapsibleSection id="typeFields" label={t("fields.content.title")}>
        {renderTypeFields(widget, onChange, variableCatalog)}
      </CollapsibleSection>

      {widget.type !== "label" && (
        <>
          <hr className="sep" />
          <CollapsibleSection id="actions" label={t("widget.actions")}>
            <ActionEditor
              widget={widget}
              actions={actions}
              pages={pages}
              profiles={profiles}
              variableCatalog={variableCatalog}
              onChange={(event: WidgetEventName, bindings: ActionBinding[]) =>
                onChange((w) => {
                  if (bindings.length === 0) delete w.actions[event];
                  else w.actions[event] = bindings;
                })
              }
            />
          </CollapsibleSection>
        </>
      )}

      <hr className="sep" />

      {/* Collapsed by default (unlike the sections above) — advanced/rarely-needed, kept out of the
         way at the very bottom (see docs/ui-guidelines.md); whoever wants it clicks to open it. */}
      <CollapsibleSection id="css" label={t("css.label")} defaultCollapsed>
        <CssEditor value={widget.customCss} onChange={(css) => onChange((w) => { w.customCss = css; })} />
      </CollapsibleSection>
    </div>
  );
}

interface PagePropertiesProps {
  page: Page;
  onRename: (pageId: string, name: string) => void;
  onSetGrid: (pageId: string, cols: number, rows: number) => void;
  onSetGap: (pageId: string, gap: number) => void;
  onSetPadding: (pageId: string, padding: number) => void;
  onSetAlignment: (pageId: string, alignment: "start" | "center" | "end") => void;
  onDuplicate: (pageId: string) => void;
  onDelete: (pageId: string) => void;
  canDelete: boolean;
}

function PageProperties({ page, onRename, onSetGrid, onSetGap, onSetPadding, onSetAlignment, onDuplicate, onDelete, canDelete }: PagePropertiesProps) {
  const { t } = useT();

  return (
    <div style={{ padding: 12, display: "flex", flexDirection: "column", gap: 14, overflowY: "auto", height: "100%" }}>
      <SectionLabel>{t("page.properties.title")}</SectionLabel>

      <label className="field">
        {t("page.properties.name")}
        <input
          type="text"
          defaultValue={page.name}
          key={page.id}
          onBlur={(e) => { if (e.target.value.trim() && e.target.value.trim() !== page.name) onRename(page.id, e.target.value.trim()); }}
          onKeyDown={(e) => { if (e.key === "Enter") (e.target as HTMLInputElement).blur(); }}
        />
      </label>

      <label className="field">
        {t("page.properties.grid")}
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <input
            type="number" min={1} max={24} value={page.cols}
            onChange={(e) => onSetGrid(page.id, Number(e.target.value), page.rows)}
          />
          ×
          <input
            type="number" min={1} max={24} value={page.rows}
            onChange={(e) => onSetGrid(page.id, page.cols, Number(e.target.value))}
          />
        </div>
      </label>

      <label className="field">
        {t("page.properties.gap")}
        <input
          type="number" min={0} max={60} value={page.gap ?? 10}
          onChange={(e) => onSetGap(page.id, Number(e.target.value))}
        />
      </label>

      <label className="field">
        {t("page.properties.padding")}
        <input
          type="number" min={0} max={200} value={page.padding ?? 0}
          onChange={(e) => onSetPadding(page.id, Number(e.target.value))}
        />
      </label>

      <label className="field">
        {t("page.properties.alignment")}
        <Seg
          value={page.alignment ?? "center"}
          onChange={(v) => onSetAlignment(page.id, v)}
          options={[
            { value: "start", label: t("page.properties.alignment.start") },
            { value: "center", label: t("page.properties.alignment.center") },
            { value: "end", label: t("page.properties.alignment.end") },
          ]}
        />
      </label>

      <hr className="sep" />

      <div style={{ display: "flex", gap: 6 }}>
        <button className="ghost" style={{ flex: 1 }} onClick={() => onDuplicate(page.id)}>{t("page.properties.duplicate")}</button>
        <button
          className="ghost"
          style={{ flex: 1, color: "var(--ms-danger)" }}
          disabled={!canDelete}
          onClick={async () => { if (await confirmAsync(t("page.deleteConfirm", page.name), { title: t("page.delete"), danger: true })) onDelete(page.id); }}
        >
          {t("page.properties.delete")}
        </button>
      </div>
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
      return <RangeFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />;
    case "button":
    case "toggle":
    case "label":
    default:
      return <TextFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />;
  }
}

function typeLabel(type: Widget["type"], t: ReturnType<typeof useT>["t"]): string {
  switch (type) {
    case "button": return t("widget.type.button");
    case "toggle": return t("widget.type.toggle");
    case "label": return t("widget.type.label");
    case "image": return t("widget.type.image");
    case "slider": return t("widget.type.slider");
    case "knob": return t("widget.type.knob");
    case "web": return t("widget.type.web");
    case "plugin-html": return t("widget.type.plugin-html");
    default: return type;
  }
}
