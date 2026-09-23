import { useEffect, useState } from "react";
import type { Profile } from "@macro/renderer";
import { Copy, Smartphone, Trash2 } from "lucide-react";
import { api } from "./api/client";
import { confirmAsync, promptAsync } from "./dialogs/dialogStore";
import { DialogHost } from "./dialogs/DialogHost";
import { DevicePreviewFrame, type DeviceSize } from "./grid/DevicePreviewFrame";
import { EditorCanvas } from "./grid/EditorCanvas";
import { useT } from "./i18n/I18nContext";
import type { DictKey } from "./i18n/tr";
import { ContextMenu, type ContextMenuItem } from "./panels/ContextMenu";
import { Inspector } from "./panels/Inspector";
import { MenuBar } from "./panels/MenuBar";
import { MoveCopyDialog } from "./panels/MoveCopyDialog";
import { ProfilePagesPanel } from "./panels/ProfilePagesPanel";
import { WidgetPalette } from "./panels/WidgetPalette";
import { usePreferences } from "./preferences/PreferencesContext";
import { useEditorState } from "./state/useEditorState";

/** Common phone/tablet CSS-px viewport sizes (device-independent px, same units widget fontSize uses)
 * for the "Önizleme" picker. User-defined sizes from Preferences are appended after these. "Özel"
 * reveals free-form width/height inputs. */
const BUILTIN_DEVICE_PRESETS: { id: string; key: DictKey; size: DeviceSize | null }[] = [
  { id: "free", key: "device.free", size: null },
  { id: "phone-portrait", key: "device.phonePortrait", size: { width: 390, height: 844 } },
  { id: "phone-landscape", key: "device.phoneLandscape", size: { width: 844, height: 390 } },
  { id: "tablet-portrait", key: "device.tabletPortrait", size: { width: 834, height: 1194 } },
  { id: "tablet-landscape", key: "device.tabletLandscape", size: { width: 1194, height: 834 } },
];

export function App() {
  const { t } = useT();
  const { previewProfiles } = usePreferences();
  const state = useEditorState();
  const { profile, currentPage } = state;
  const [devicePresetId, setDevicePresetId] = useState("free");
  const [customSize, setCustomSize] = useState<DeviceSize>({ width: 390, height: 844 });
  const devicePresets = [
    ...BUILTIN_DEVICE_PRESETS.map((p) => ({ id: p.id, label: t(p.key), size: p.size })),
    ...previewProfiles.map((p) => ({ id: p.id, label: p.name, size: { width: p.width, height: p.height } })),
    { id: "custom", label: t("device.custom"), size: null },
  ];
  const deviceSize = devicePresetId === "custom" ? customSize : (devicePresets.find((p) => p.id === devicePresetId)?.size ?? null);
  const [widgetMenu, setWidgetMenu] = useState<{ x: number; y: number } | null>(null);
  const [pageMenu, setPageMenu] = useState<{ x: number; y: number; pageId: string } | null>(null);
  const [moveCopyOpen, setMoveCopyOpen] = useState(false);
  const [copyPageTarget, setCopyPageTarget] = useState<string | null>(null);

  // Each profile remembers its own "Önizleme" preset (a tablet profile reopens in tablet preview, a
  // phone profile in phone preview, ...) — switch to it whenever a *different* profile is loaded, but
  // never on every in-place edit (profile is a fresh object on every mutate(), so this keys off the id).
  useEffect(() => {
    if (!profile) return;
    const wanted = profile.previewDeviceId;
    setDevicePresetId(wanted && devicePresets.some((p) => p.id === wanted) ? wanted : "free");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [profile?.id]);

  // If the currently-selected preview profile is deleted in Tercihler, fall back to "free" on its own
  // instead of pointing at a size that no longer exists.
  useEffect(() => {
    if (devicePresetId === "free" || devicePresetId === "custom") return;
    if (!devicePresets.some((p) => p.id === devicePresetId)) setDevicePresetId("free");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [devicePresets, devicePresetId]);

  if (!profile || !currentPage) {
    return <div style={{ padding: 20, color: "var(--ms-text-secondary)" }}>{t("app.loading")}</div>;
  }

  return (
    <div style={{ display: "grid", gridTemplateRows: "auto auto 1fr", height: "100%" }}>
      <MenuBar
        profile={profile}
        onImportProfile={(data: Profile) => state.importProfileFromJson(data)}
      />

      <header style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 12px", borderBottom: "1px solid var(--ms-border)" }}>
        <div style={{ flex: 1 }} />

        <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
          {t("header.preview")}
          <select
            value={devicePresetId}
            onChange={(e) => { setDevicePresetId(e.target.value); state.setPreviewDevice(e.target.value); }}
            style={{ width: "auto" }}
          >
            {devicePresets.map((p) => <option key={p.id} value={p.id}>{p.label}</option>)}
          </select>
        </label>
        {devicePresetId === "custom" && (
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "var(--ms-text-secondary)" }}>
            <input
              type="number" min={100} max={4000} value={customSize.width} style={{ width: 56 }}
              onChange={(e) => setCustomSize((s) => ({ ...s, width: Number(e.target.value) }))}
            />
            ×
            <input
              type="number" min={100} max={4000} value={customSize.height} style={{ width: 56 }}
              onChange={(e) => setCustomSize((s) => ({ ...s, height: Number(e.target.value) }))}
            />
          </label>
        )}

        <button className="ghost" onClick={state.refreshVariables}>{t("header.refreshVariables")}</button>
        <button className="ghost" onClick={() => api.openToolWindow("pairing")} style={{ display: "flex", alignItems: "center", gap: 5 }}>
          <Smartphone size={13} /> {t("header.pairing")}
        </button>
        <button className="primary save-btn" onClick={state.save} disabled={!state.dirty || state.saving}>
          {state.saving ? t("header.saving") : state.dirty ? t("header.save") : t("header.saved")}
        </button>
      </header>

      <DialogHost />

      {state.error && (
        <div style={{ padding: "6px 12px", background: "rgba(192,57,43,.15)", color: "var(--ms-danger)", fontSize: 12, display: "flex", justifyContent: "space-between" }}>
          <span>{state.error}</span>
          <button className="ghost" onClick={state.clearError}>{t("header.close")}</button>
        </div>
      )}

      <div style={{ display: "grid", gridTemplateColumns: "200px 1fr 300px", minHeight: 0 }}>
        <div style={{ borderRight: "1px solid var(--ms-border)", display: "flex", flexDirection: "column", minHeight: 0 }}>
          <div style={{ flex: "0 0 45%", minHeight: 0, borderBottom: "1px solid var(--ms-border)" }}>
            <ProfilePagesPanel
              profile={profile}
              profiles={state.profiles}
              pages={profile.pages}
              currentPageId={state.currentPageId}
              onSelectProfile={state.selectProfile}
              onCreateProfile={state.createProfile}
              onRenameProfile={state.renameProfile}
              onDeleteProfile={() => state.deleteProfile(profile.id)}
              canDeleteProfile={state.profiles.length > 1}
              onSelectPage={(pageId) => { state.setCurrentPageId(pageId); state.setSelectedIds([]); }}
              onAddPage={state.addPage}
              onRenamePage={state.renamePage}
              onDuplicatePage={state.duplicatePage}
              onDeletePage={state.deletePage}
              onPageContextMenu={(x, y, pageId) => setPageMenu({ x, y, pageId })}
            />
          </div>
          <div style={{ flex: 1, minHeight: 0, overflowY: "auto" }}>
            <WidgetPalette onAdd={state.addWidget} />
          </div>
        </div>

        <div style={{ padding: 16, minWidth: 0, minHeight: 0 }}>
          <div style={{ width: "100%", height: "100%", border: "1px solid var(--ms-border)", borderRadius: 4, background: "var(--ms-bg-surface)" }}>
            <DevicePreviewFrame size={deviceSize}>
              <EditorCanvas
                page={currentPage}
                selectedIds={state.selectedIds}
                onSelect={state.setSelectedIds}
                onToggleSelect={state.toggleSelected}
                onRectChange={state.setWidgetRect}
                onContextMenu={(x, y) => setWidgetMenu({ x, y })}
                variables={state.variables}
              />
            </DevicePreviewFrame>
          </div>
        </div>

        <div style={{ borderLeft: "1px solid var(--ms-border)", overflowY: "auto" }}>
          <Inspector
            selectedWidgets={state.selectedWidgets}
            page={currentPage}
            pages={profile.pages}
            profiles={state.profiles}
            actions={state.actions}
            variableCatalog={state.variableCatalog}
            onChange={(fn) => state.selectedWidget && state.updateWidget(state.selectedWidget.id, fn)}
            onDelete={() => state.selectedWidget && state.deleteWidget(state.selectedWidget.id)}
            onDeleteSelected={state.deleteSelectedWidgets}
            onDuplicateSelected={state.duplicateSelectedWidgets}
            onMoveCopySelected={() => setMoveCopyOpen(true)}
            onRenamePage={state.renamePage}
            onSetPageGrid={state.setPageGrid}
            onSetPageGap={state.setPageGap}
            onSetPagePadding={state.setPagePadding}
            onSetPageAlignment={state.setPageAlignment}
            onDuplicatePage={state.duplicatePage}
            onDeletePage={state.deletePage}
            canDeletePage={profile.pages.length > 1}
          />
        </div>
      </div>

      {widgetMenu && (
        <ContextMenu
          x={widgetMenu.x}
          y={widgetMenu.y}
          onClose={() => setWidgetMenu(null)}
          items={widgetContextItems(t, state.selectedIds.length, {
            onDuplicate: state.duplicateSelectedWidgets,
            onMoveCopy: () => setMoveCopyOpen(true),
            onDelete: state.deleteSelectedWidgets,
          })}
        />
      )}

      {pageMenu && (
        <ContextMenu
          x={pageMenu.x}
          y={pageMenu.y}
          onClose={() => setPageMenu(null)}
          items={pageContextItems(t, pageMenu.pageId, profile.pages.length > 1, {
            onRename: async (pageId) => {
              const page = profile.pages.find((p) => p.id === pageId);
              const name = await promptRename(t, page?.name ?? "");
              if (name) state.renamePage(pageId, name);
            },
            onDuplicate: state.duplicatePage,
            onCopyToProfile: (pageId) => setCopyPageTarget(pageId),
            onDelete: async (pageId) => {
              const page = profile.pages.find((p) => p.id === pageId);
              if (await confirmDeletePage(t, page?.name ?? "")) state.deletePage(pageId);
            },
          })}
        />
      )}

      {moveCopyOpen && currentPage && (
        <MoveCopyDialog
          title={t("moveCopy.widgetsTitle", String(state.selectedIds.length))}
          profiles={state.profiles}
          currentProfileId={profile.id}
          currentProfilePages={profile.pages}
          onClose={() => setMoveCopyOpen(false)}
          onConfirm={(targetProfileId, targetPageId, mode) => {
            state.moveOrCopyWidgets(state.selectedIds, targetProfileId, targetPageId, mode);
            setMoveCopyOpen(false);
          }}
        />
      )}

      {copyPageTarget && (
        <MoveCopyDialog
          title={t("moveCopy.pageTitle")}
          profiles={state.profiles}
          currentProfileId={profile.id}
          currentProfilePages={profile.pages}
          wholePage
          onClose={() => setCopyPageTarget(null)}
          onConfirm={(targetProfileId) => {
            state.duplicatePageToProfile(copyPageTarget, targetProfileId);
            setCopyPageTarget(null);
          }}
        />
      )}
    </div>
  );
}

type T = ReturnType<typeof useT>["t"];

async function promptRename(t: T, current: string): Promise<string | null> {
  const name = await promptAsync(t("page.renamePrompt"), current, { title: t("page.rename") });
  return name && name.trim() ? name.trim() : null;
}
async function confirmDeletePage(t: T, name: string): Promise<boolean> {
  return confirmAsync(t("page.deleteConfirm", name), { title: t("page.delete"), danger: true });
}

function widgetContextItems(
  t: T,
  selectedCount: number,
  handlers: { onDuplicate: () => void; onMoveCopy: () => void; onDelete: () => void },
): ContextMenuItem[] {
  const label = selectedCount > 1 ? t("ctx.widget.labelMulti", String(selectedCount)) : t("ctx.widget.labelSingle");
  return [
    { label: t("ctx.widget.duplicate", label), icon: <Copy size={13} />, onSelect: handlers.onDuplicate },
    { label: t("ctx.widget.moveCopy", label), onSelect: handlers.onMoveCopy },
    { label: t("ctx.widget.delete", label), icon: <Trash2 size={13} />, danger: true, onSelect: handlers.onDelete },
  ];
}

function pageContextItems(
  t: T,
  pageId: string,
  canDelete: boolean,
  handlers: {
    onRename: (pageId: string) => void;
    onDuplicate: (pageId: string) => void;
    onCopyToProfile: (pageId: string) => void;
    onDelete: (pageId: string) => void;
  },
): ContextMenuItem[] {
  return [
    { label: t("ctx.page.rename"), onSelect: () => handlers.onRename(pageId) },
    { label: t("page.duplicate"), icon: <Copy size={13} />, onSelect: () => handlers.onDuplicate(pageId) },
    { label: t("page.copyToProfile"), onSelect: () => handlers.onCopyToProfile(pageId) },
    { label: t("page.delete"), icon: <Trash2 size={13} />, danger: true, disabled: !canDelete, onSelect: () => handlers.onDelete(pageId) },
  ];
}
