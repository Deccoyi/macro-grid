import { useCallback, useMemo } from "react";
import type { ActionInfo, VariableInfo } from "../api/types";
import { usePreferences } from "../preferences/PreferencesContext";

type Language = "tr" | "en";

/** Localized text for what the server (and plugins) describe: action names/descriptions, variable
 * descriptions and category names. The server only sends one language-neutral (English) text; this is
 * looked up by the stable id (action type, variable name, category id) and falls back to the server's own
 * text, so a plugin that ships no entry here still shows its own description. English needs no entries
 * because the server text already is English. */
const TR: Record<string, string> = {
  // ---- Categories ----
  "category:System": "Sistem",
  "category:Audio": "Ses",
  "category:Keyboard": "Klavye",
  "category:Page & Profile": "Sayfa & Profil",
  "category:Other": "Diğer",
  "category:Plugins": "Eklentiler",

  // ---- Built-in actions ----
  "action:core.setVolume": "Ana ses seviyesi",
  "actionDesc:core.setVolume": "Slider/knob ile ana ses seviyesini ayarlar",
  "action:core.setMute": "Sesi kapat/aç",
  "actionDesc:core.setMute": "Ana sesi belirli bir duruma getirir",
  "action:core.toggleMute": "Sesi sessize al/aç",
  "actionDesc:core.toggleMute": "Ana sesi mute/unmute arasında değiştirir",
  "action:core.hotkey": "Kısayol tuşu",
  "actionDesc:core.hotkey": "Bir tuş kombinasyonu gönderir",
  "action:core.typeText": "Metin yaz",
  "actionDesc:core.typeText": "Sabit bir metni yazar",
  "action:core.page": "Sayfa değiştir",
  "actionDesc:core.page": "Cihazın gösterdiği sayfayı değiştirir",
  "action:core.profile": "Profil değiştir",
  "actionDesc:core.profile": "Cihazın profilini değiştirir",
  "action:core.open": "Uygulama aç",
  "actionDesc:core.open": "Bir uygulama veya dosya açar",
  "action:core.openUrl": "URL aç",
  "actionDesc:core.openUrl": "Varsayılan tarayıcıda bir adres açar",
  "action:core.delay": "Bekle",
  "actionDesc:core.delay": "Çoklu aksiyon içinde bekler",


  // ---- Built-in variables ----
  "variable:system.time": "Şu anki saat ve tarih",
  "variable:system.uptime": "Bilgisayarın açık kalma süresi",
  "variable:system.cpu": "İşlemci kullanımı (%)",
  "variable:system.ram": "RAM kullanımı (%)",
  "variable:system.ram.used": "Kullanılan RAM (GB)",
  "variable:system.ram.total": "Toplam RAM (GB)",
  "variable:system.audio.master": "Ana ses seviyesi (%)",
  "variable:system.audio.muted": "Ses sessize alınmış mı",

};

const TEXTS: Record<Language, Record<string, string>> = { tr: TR, en: {} };

export function useCatalogText() {
  const { language } = usePreferences();
  const table = TEXTS[language];

  const lookup = useCallback((key: string, fallback: string) => table[key] ?? fallback, [table]);

  return useMemo(() => ({
    categoryLabel: (id: string) => lookup(`category:${id}`, id),
    actionName: (a: ActionInfo) => lookup(`action:${a.type}`, a.displayName),
    actionDescription: (a: ActionInfo) => lookup(`actionDesc:${a.type}`, a.description ?? ""),
    variableDescription: (v: VariableInfo) => lookup(`variable:${v.name}`, v.description),
  }), [lookup]);
}
