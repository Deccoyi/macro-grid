import { useEffect, useRef, useState } from "react";
import type { Profile } from "@macro/renderer";
import { api } from "../api/client";
import { useT, type Language } from "../i18n/I18nContext";
import { ContextMenu, type ContextMenuItem } from "./ContextMenu";

export interface MenuBarProps {
  profile: Profile | null;
  onImportProfile: (data: Profile) => void;
}

const APP_VERSION = "0.1.0";

// Access-key letters (Windows mnemonic convention: Alt+letter opens the menu, and the letter is
// underlined in the label while Alt is held) — one map per language since the underlined letter has
// to actually occur in that language's label.
const MNEMONICS: Record<Language, Record<string, string>> = {
  tr: { file: "D", settings: "A", plugins: "E", help: "Y" },
  en: { file: "F", settings: "S", plugins: "P", help: "H" },
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
 * real Windows desktop app menu bar (see docs/ui-guidelines.md), not a website nav: flat, no accent tint
 * on the label itself, hovering a menu button switches to it while another menu is already open, and
 * holding Alt reveals mnemonic underlines (Alt+letter opens that menu directly). Settings/Plugins open
 * as real separate OS windows (ToolWindow.cs) rather than in-page modals; only File's import/export use
 * native Open/Save dialogs on the server's desktop instead of browser download/upload. */
export function MenuBar({ profile, onImportProfile }: MenuBarProps) {
  const { t, lang } = useT();
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
    await api.exportProfileDialog(`${profile.name || "profile"}.json`, JSON.stringify(profile, null, 2));
  };

  const importProfile = async () => {
    const { content } = await api.importProfileDialog();
    if (!content) return;
    try {
      onImportProfile(JSON.parse(content));
    } catch {
      // Malformed file — silently ignored rather than showing a raw parser error to the user.
    }
  };

  const fileItems: ContextMenuItem[] = [
    { label: t("menu.file.exportProfile"), onSelect: exportProfile, disabled: !profile },
    { label: t("menu.file.importProfile"), onSelect: importProfile },
  ];
  const settingsItems: ContextMenuItem[] = [{ label: t("menu.settings.open"), onSelect: () => api.openToolWindow("preferences") }];
  const pluginsItems: ContextMenuItem[] = [{ label: t("menu.plugins.manage"), onSelect: () => api.openToolWindow("plugins") }];
  const helpItems: ContextMenuItem[] = [
    { label: t("menu.help.version", APP_VERSION), disabled: true, onSelect: () => {} },
    { label: t("menu.help.licenses"), onSelect: () => api.openToolWindow("help") },
    { label: t("menu.help.agreement"), onSelect: () => api.openToolWindow("help") },
  ];

  const menus: { id: string; label: string; items: ContextMenuItem[] }[] = [
    { id: "file", label: t("menu.file"), items: fileItems },
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
