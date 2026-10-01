import { useEffect, useRef, useState } from "react";
import type { Profile } from "@macro/renderer";
import { api } from "../api/client";
import { commandItem } from "../commands/commandItem";
import type { Command } from "../commands/types";
import { alertAsync, choiceAsync, confirmAsync, confirmRichAsync, promptAsync } from "../dialogs/dialogStore";
import { LogExportContent } from "../dialogs/LogExportContent";
import { showStatusNotice } from "../state/statusNotice";
import { usePreferences } from "../preferences/PreferencesContext";
import { clearWebUrls, collectWebSites } from "../state/webUrls";
import { WebImportConsent } from "../windows/WebImportConsent";
import { useServerVersion } from "../state/useServerVersion";
import { useT, type Language } from "../i18n/I18nContext";
import { usePluginUpdateCount } from "../state/pluginUpdates";
import { TOOL_WINDOWS } from "../workspace/toolWindows";
import { useWorkspace } from "../workspace/WorkspaceContext";
import { ContextMenu, type ContextMenuEntry } from "./ContextMenu";

interface MenuBarProps {
  profile: Profile | null;
  onImportProfile: (data: Profile) => Promise<void>;
  /** The Edit commands (undo/redo/cut/copy/paste/duplicate/delete/select all) — built once in App.tsx
   * alongside their shortcuts, and turned into menu rows here with commandItem() so the Edit menu, the
   * shortcuts and every context menu item all run the exact same code. */
  editCommands: Command[];
}

// Access-key letters (Windows mnemonic convention: Alt+letter opens the menu, and the letter is
// underlined in the label while Alt is held) — one map per language since the underlined letter has
// to actually occur in that language's label.
const MNEMONICS: Record<Language, Record<string, string>> = {
  tr: { file: "D", edit: "Z", view: "G", settings: "A", plugins: "E", help: "Y" },
  en: { file: "F", edit: "E", view: "V", settings: "S", plugins: "P", help: "H" },
};

function mnemonicLabel(label: string, letter: string | undefined, show: boolean) {
  if (!letter || !show) return label;
  const idx = label.toUpperCase().indexOf(letter.toUpperCase());
  if (idx === -1) return label;
  return (
    <>
      {label.slice(0, idx)}
      <span style={{ textDecoration: "underline" }}>{label[idx]}</span>
      {label.slice(idx + 1)}
    </>
  );
}

/** The single top row above the toolbar: File / Settings / Plugins / Help — styled and behaving like a
 * real Windows desktop app menu bar (see docs/ui/ui-guidelines.md), not a website nav: flat, no accent tint
 * on the label itself, hovering a menu button switches to it while another menu is already open, and
 * holding Alt reveals mnemonic underlines (Alt+letter opens that menu directly). Settings/Plugins open
 * as real separate OS windows (ToolWindow.cs) rather than in-page modals; only File's import/export use
 * native Open/Save dialogs on the server's desktop instead of browser download/upload. */
export function MenuBar({ profile, onImportProfile, editCommands }: MenuBarProps) {
  const { t, lang } = useT();
  const serverVersion = useServerVersion();
  const pluginUpdates = usePluginUpdateCount();
  const workspace = useWorkspace();
  const prefs = usePreferences();
  const [openMenu, setOpenMenu] = useState<{ id: string; x: number; y: number } | null>(null);
  const [mnemonicsVisible, setMnemonicsVisible] = useState(false);
  const refs = useRef<Record<string, HTMLButtonElement | null>>({});

  const open = (id: string) => {
    const el = refs.current[id];
    if (!el) return;
    const rect = el.getBoundingClientRect();
    setOpenMenu({ id, x: rect.left, y: rect.bottom + 2 });
  };

  const exportProfile = async () => {
    if (!profile) return;
    try {
      await api.exportProfileDialog(profile);
    } catch (err) {
      await alertAsync(err instanceof Error ? err.message : String(err), { title: t("profile.exportFailedTitle") });
    }
  };

  const importProfile = async () => {
    let result;
    try {
      result = await api.importProfileDialog();
    } catch (err) {
      // The server says why the file is unusable (damaged, empty, made by a newer version...).
      await alertAsync(err instanceof Error ? err.message : String(err), { title: t("profile.importFailedTitle") });
      return;
    }
    if (!result.profile) return;

    // A profile from someone else can point a phone at any site the moment it is used: name the sites and ask once.
    const sites = collectWebSites(result.profile);
    if (sites.length > 0) {
      const choice = await choiceAsync("", [
        { value: "clear", label: t("profile.importWebClear") },
        { value: "keep", label: t("profile.importWebKeep"), primary: true },
      ], { title: t("profile.importWebTitle"), content: <WebImportConsent sites={sites} /> });
      if (choice === null) return;
      if (choice === "clear") clearWebUrls(result.profile);
    }

    await onImportProfile(result.profile);

    const missing = result.missingPlugins?.map((p) => p.name) ?? [];
    const covered = new Set(result.missingPlugins?.flatMap((p) => p.actionTypes) ?? []);
    const unknown = (result.unknownActionTypes ?? []).filter((type) => !covered.has(type));
    if (missing.length > 0 || unknown.length > 0) {
      await alertAsync(t("profile.importMissing", missing.join(", "), unknown.join(", ")), { title: t("profile.importMissingTitle") });
    }
  };

  const findCommand = (id: string) => editCommands.find((c) => c.id === id)!;
  const fileItems: ContextMenuEntry[] = [
    commandItem(findCommand("file.save"), t),
    { divider: true },
    { label: t("menu.file.exportProfile"), onSelect: exportProfile, disabled: !profile },
    { label: t("menu.file.importProfile"), onSelect: importProfile },
  ];
  const editItems: ContextMenuEntry[] = [
    commandItem(findCommand("edit.undo"), t),
    commandItem(findCommand("edit.redo"), t),
    { divider: true },
    commandItem(findCommand("edit.cut"), t),
    commandItem(findCommand("edit.copy"), t),
    commandItem(findCommand("edit.paste"), t),
    commandItem(findCommand("edit.duplicate"), t),
    commandItem(findCommand("edit.delete"), t),
    { divider: true },
    commandItem(findCommand("edit.selectAll"), t),
  ];

  const saveCurrentLayout = async () => {
    const name = await promptAsync(t("layoutProfiles.namePlaceholder"), "", { title: t("layoutProfiles.save") });
    if (!name || !name.trim()) return;
    const json = workspace.serializeLayout();
    if (json) prefs.saveDockLayoutProfile(name.trim(), json);
  };

  // "Delete" is itself a submenu listing each deletable saved layout — Default never appears here, since
  // it isn't one of prefs.dockLayoutProfiles at all (it's rebuilt from code, see defaultLayout.ts).
  const deleteLayoutItems: ContextMenuEntry[] = prefs.dockLayoutProfiles.length === 0
    ? [{ label: t("layoutProfiles.empty"), disabled: true }]
    : prefs.dockLayoutProfiles.map((p) => ({
      label: p.name,
      danger: true,
      onSelect: async () => {
        if (await confirmAsync(t("layoutProfiles.deleteConfirm", p.name), { title: t("layoutProfiles.delete"), danger: true })) {
          prefs.deleteDockLayoutProfile(p.id);
        }
      },
    }));

  const layoutsItems: ContextMenuEntry[] = [
    { label: t("layoutProfiles.save"), onSelect: saveCurrentLayout },
    { label: t("layoutProfiles.delete"), submenu: deleteLayoutItems },
    { divider: true },
    // Default always first and never deletable — clicking any of these switches the layout immediately,
    // no confirmation window, matching how the tool-window toggles above it behave.
    { label: t("layoutProfiles.default"), onSelect: () => workspace.resetToDefaultLayout() },
    ...prefs.dockLayoutProfiles.map((p) => ({ label: p.name, onSelect: () => workspace.applyLayout(p.layoutJson) })),
  ];

  const viewItems: ContextMenuEntry[] = [
    ...TOOL_WINDOWS.map((tw) => ({
      label: t(tw.titleKey),
      checked: workspace.isOpen(tw.id),
      onSelect: () => workspace.toggleFromMenu(tw.id),
    })),
    { divider: true },
    { label: t("menu.view.layouts"), submenu: layoutsItems },
  ];
  const settingsItems: ContextMenuEntry[] = [{ label: t("menu.settings.open"), onSelect: () => api.openToolWindow("preferences") }];
  const pluginsItems: ContextMenuEntry[] = [{ label: t("menu.plugins.manage"), onSelect: () => api.openToolWindow("plugins") }];
  const helpItems: ContextMenuEntry[] = [
    { label: t("menu.help.version", serverVersion), disabled: true },
    { label: t("menu.help.checkForUpdates"), onSelect: () => api.openToolWindow("update", "check") },
    { label: t("menu.help.about"), onSelect: () => api.openToolWindow("help", "about") },
    { label: t("menu.help.agreement"), onSelect: () => api.openToolWindow("help", "agreement") },
    { label: t("menu.help.licenses"), onSelect: () => api.openToolWindow("help", "licenses") },
    {
      label: t("menu.help.exportLogs"),
      onSelect: async () => {
        if (!(await confirmRichAsync({ title: t("logExport.title"), content: <LogExportContent />, confirmLabel: t("logExport.save") }))) return;
        try {
          const result = await api.exportLogs();
          if (result.path) showStatusNotice("logExport.saved");
        } catch {
          showStatusNotice("logExport.saveFailed");
        }
      },
    },
  ];

  const menus: { id: string; label: string; items: ContextMenuEntry[] }[] = [
    { id: "file", label: t("menu.file"), items: fileItems },
    { id: "edit", label: t("menu.edit"), items: editItems },
    { id: "view", label: t("menu.view"), items: viewItems },
    { id: "settings", label: t("menu.settings"), items: settingsItems },
    { id: "plugins", label: t("menu.plugins"), items: pluginsItems },
    { id: "help", label: t("menu.help"), items: helpItems },
  ];
  const mnemonics = MNEMONICS[lang];

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Alt") {
        setMnemonicsVisible(true);
        return;
      }
      if (!e.altKey || e.ctrlKey || e.metaKey) return;
      const match = menus.find((m) => mnemonics[m.id]?.toUpperCase() === e.key.toUpperCase());
      if (match) {
        e.preventDefault();
        setMnemonicsVisible(true);
        open(match.id);
      }
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.key === "Alt" && !openMenu) setMnemonicsVisible(false);
    };
    window.addEventListener("keydown", handleKeyDown);
    window.addEventListener("keyup", handleKeyUp);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("keyup", handleKeyUp);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lang, openMenu]);

  return (
    <div className="menu-bar">
      {menus.map((m) => (
        <button
          key={m.id}
          ref={(el) => { refs.current[m.id] = el; }}
          className={openMenu?.id === m.id ? "menu-bar-item open" : "menu-bar-item"}
          onClick={() => (openMenu?.id === m.id ? setOpenMenu(null) : open(m.id))}
          onMouseEnter={() => { if (openMenu && openMenu.id !== m.id) open(m.id); }}
        >
          {mnemonicLabel(m.label, mnemonics[m.id], mnemonicsVisible)}
          {m.id === "plugins" && pluginUpdates > 0 && <span className="menu-bar-dot" title={t("status.pluginUpdates", String(pluginUpdates))} />}
        </button>
      ))}

      {openMenu && (
        <ContextMenu
          x={openMenu.x}
          y={openMenu.y}
          items={menus.find((m) => m.id === openMenu.id)!.items}
          onClose={() => { setOpenMenu(null); setMnemonicsVisible(false); }}
        />
      )}
    </div>
  );
}
