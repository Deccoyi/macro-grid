import type { ActionBinding, Widget, WidgetEventName, Page } from "@macro/renderer";
import type { ActionInfo, ProfileSummary, VariableInfo } from "../api/types";
import { confirmAsync } from "../dialogs/dialogStore";
import { useT } from "../i18n/I18nContext";
import { ActionEditor } from "./ActionEditor";
import { CssEditor } from "./CssEditor";
import { AppearanceFields } from "./fields/AppearanceFields";
import { CollapsibleSection, CommitTextInput, Field, FieldGrid, NumberInput, Seg } from "./fields/controls";
import { ImageFields } from "./fields/ImageFields";
import { RangeFields } from "./fields/RangeFields";
import { TextFields } from "./fields/TextFields";
import { PluginWidgetFields, PluginWidgetIdentity } from "./fields/PluginWidgetFields";
import { NameField } from "./fields/NameField";
import { SelfPreviewField } from "./fields/SelfPreviewField";
import { WebFields } from "./fields/WebFields";

interface InspectorProps {
  selectedWidgets: Widget[];
  page: Page;
  pages: Page[];
  profiles: ProfileSummary[];
  actions: ActionInfo[];
  variableCatalog: VariableInfo[];
  onChange: (fn: (widget: Widget) => void) => void;
  onRename: (name: string) => void;
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
  onRename,
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
      <div className="pf-section">
        <div className="pf-body">
          <div className="pf-group-label section-label">{t("widget.selected.count", String(selectedWidgets.length))}</div>
          <button className="pf-btn" onClick={onDuplicateSelected}>{t("widget.selected.duplicate")}</button>
          <button className="pf-btn" onClick={onMoveCopySelected}>{t("widget.selected.moveCopy")}</button>
          <button className="pf-btn pf-danger" onClick={onDeleteSelected}>{t("widget.selected.delete")}</button>
        </div>
      </div>
    );
  }

  const widget = selectedWidgets[0]!;

  return (
    <>
      <div className="pf-section head">
        <span className="section-label">{typeLabel(widget.type, t)}</span>
        <button className="ghost pf-btn small pf-danger" onClick={onDelete}>{t("widget.delete")}</button>
      </div>

      <div className="pf-section">
        <div className="pf-body">
          <NameField widget={widget} siblings={page.widgets} onRename={onRename} />
          <SelfPreviewField widget={widget} />
        </div>
      </div>

      {widget.type === "plugin-widget" && (
        <div className="pf-section">
          <div className="pf-body">
            <PluginWidgetIdentity widget={widget} />
          </div>
        </div>
      )}

      <div className="pf-section">
        <CollapsibleSection id="appearance" label={t("fields.appearance.title")}>
          <AppearanceFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />
        </CollapsibleSection>
      </div>

      <div className="pf-section">
        <CollapsibleSection id="typeFields" label={t("fields.content.title")}>
          {renderTypeFields(widget, onChange, variableCatalog, page.widgets.filter((w) => w.type === "web").length)}
        </CollapsibleSection>
      </div>

      {widget.type !== "label" && (
        <div className="pf-section">
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
        </div>
      )}

      {/* Collapsed by default (unlike the sections above) — advanced/rarely-needed, kept out of the
         way at the very bottom (see docs/ui/ui-guidelines.md); whoever wants it clicks to open it. */}
      <div className="pf-section">
        <CollapsibleSection id="css" label={t("css.label")} defaultCollapsed>
          <CssEditor value={widget.customCss} onChange={(css) => onChange((w) => { w.customCss = css; })} />
        </CollapsibleSection>
      </div>
    </>
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
    <>
      <div className="pf-section head">
        <span className="section-label">{t("page.properties.title")}</span>
      </div>

      <div className="pf-section">
        <div className="pf-body">
          <Field label={t("page.properties.name")}>
            <CommitTextInput key={page.id} value={page.name} onCommit={(v) => { if (v && v !== page.name) onRename(page.id, v); }} />
          </Field>

          <Field single label={t("page.properties.grid")}>
            <div className="pf-row">
              <div className="grow"><NumberInput min={1} max={24} value={page.cols} onChange={(v) => onSetGrid(page.id, v, page.rows)} /></div>
              <span aria-hidden="true">×</span>
              <div className="grow"><NumberInput secondary min={1} max={24} value={page.rows} onChange={(v) => onSetGrid(page.id, page.cols, v)} /></div>
            </div>
          </Field>

          <FieldGrid cols={2}>
            <Field single label={t("page.properties.gap")}>
              <NumberInput min={0} max={60} unit="px" value={page.gap ?? 10} onChange={(v) => onSetGap(page.id, v)} />
            </Field>
            <Field single label={t("page.properties.padding")}>
              <NumberInput min={0} max={200} unit="px" value={page.padding ?? 0} onChange={(v) => onSetPadding(page.id, v)} />
            </Field>
          </FieldGrid>

          <Field single label={t("page.properties.alignment")}>
            <Seg
              value={page.alignment ?? "center"}
              onChange={(v) => onSetAlignment(page.id, v)}
              options={[
                { value: "start", label: t("page.properties.alignment.start") },
                { value: "center", label: t("page.properties.alignment.center") },
                { value: "end", label: t("page.properties.alignment.end") },
              ]}
            />
          </Field>
        </div>
      </div>

      <div className="pf-section">
        <FieldGrid cols={2}>
          <button className="pf-btn" onClick={() => onDuplicate(page.id)}>{t("page.properties.duplicate")}</button>
          <button
            className="pf-btn pf-danger"
            disabled={!canDelete}
            onClick={async () => { if (await confirmAsync(t("page.deleteConfirm", page.name), { title: t("page.delete"), danger: true })) onDelete(page.id); }}
          >
            {t("page.properties.delete")}
          </button>
        </FieldGrid>
      </div>
    </>
  );
}

function renderTypeFields(widget: Widget, onChange: InspectorProps["onChange"], variableCatalog: VariableInfo[], webWidgetsOnPage: number) {
  switch (widget.type) {
    case "image":
      return <ImageFields widget={widget} onChange={onChange} />;
    case "web":
    case "plugin-html":
      return <WebFields widget={widget} onChange={onChange} webWidgetsOnPage={webWidgetsOnPage} />;
    case "plugin-widget":
      return <PluginWidgetFields widget={widget} onChange={onChange} variableCatalog={variableCatalog} />;
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
    case "plugin-widget": return t("widget.type.plugin-widget");
    default: return type;
  }
}
