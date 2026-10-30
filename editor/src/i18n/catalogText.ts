import { useCallback, useMemo } from "react";
import type { ActionInfo, VariableInfo } from "../api/types";
import { usePreferences } from "../preferences/PreferencesContext";

import type { Language } from "./language";

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
  "category:Widgets": "Widget'lar",
  "category:Other": "Diğer",
  "category:Plugins": "Eklentiler",
  "category:Variables": "Değişkenler",
  "category:Variable": "Değişken",
  "category:Global Variable List": "Genel Değişken Listesi",
  "category:This button": "Bu düğme",
  "category:Logic": "Mantık",

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
  "action:core.web": "Web sayfasını değiştir",
  "actionDesc:core.web": "Bir web widget'ında başka bir adres gösterir veya sayfayı yeniden yükler (yalnızca butona basan telefonda)",
  "action:core.open": "Uygulama aç",
  "actionDesc:core.open": "Bir uygulama veya dosya açar",
  "action:core.openUrl": "URL aç",
  "actionDesc:core.openUrl": "Varsayılan tarayıcıda bir adres açar",
  "action:core.delay": "Bekle",
  "actionDesc:core.delay": "Çoklu eylem içinde bekler",
  "action:core.if": "If",
  "actionDesc:core.if": "Bir koşul sağlanırsa (veya bir önceki adım başarısız olduysa) bitişine kadar olan adımları çalıştırır",
  "action:core.else": "Else",
  "actionDesc:core.else": "If sağlanmadıysa çalışacak adımlar",
  "action:core.endIf": "End",
  "actionDesc:core.endIf": "Bir If bloğunu bitirir",
  "action:core.stop": "Durdur",
  "actionDesc:core.stop": "Eylem listesini burada bitirir",
  "action:core.setVariable": "Değişken ayarla (eski)",
  "actionDesc:core.setVariable": "Eski, hepsi bir arada eylem: Genel Değişken Listesi'ndeki bir değişkeni ayarlar, ters çevirir, sayar veya sıfırlar",
  "action:core.variable.set": "Değişkene değer ata",
  "actionDesc:core.variable.set": "Bir değişkene değer koyar",
  "action:core.variable.add": "Sayıyı artır / azalt",
  "actionDesc:core.variable.add": "Bir Sayı değişkenine bir sayı ekler veya çıkarır (örneğin +1 ya da -5)",
  "action:core.variable.toggle": "Değişkeni ters çevir",
  "actionDesc:core.variable.toggle": "Bir Doğru / Yanlış değişkenini diğer değerine çevirir",
  "action:core.variable.reset": "Başlangıç değerine döndür",
  "actionDesc:core.variable.reset": "Bir değişkeni başlangıç değerine döndürür",


  // ---- Built-in variables ----
  "variable:system.time": "Şu anki saat ve tarih",
  "variable:system.uptime": "Bilgisayarın açık kalma süresi",
  "variable:system.cpu": "İşlemci kullanımı (%)",
  "variable:system.ram": "RAM kullanımı (%)",
  "variable:system.ram.used": "Kullanılan RAM (GB)",
  "variable:system.ram.total": "Toplam RAM (GB)",
  "variable:system.audio.master": "Ana ses seviyesi (%)",
  "variable:self.toggled": "Bu açma/kapama düğmesi açıkken doğru. Her cihazda aynıdır.",
  "variable:self.busy": "Bu düğmenin eylemleri çalışırken doğru (kısa bir an sonra). Yalnızca düğmeye basan cihazda.",
  "variable:self.pressed": "Bu düğme basılıyken ve kısa bir süre sonrasında doğru. Yalnızca düğmeye basan cihazda.",
  "variable:self.lastResult": "Bu düğmenin eylemlerinin son çalışmasının sonucu. Hiç çalışmadıysa boş. Her cihazda aynıdır.",
  "variable:self.lastError": "Son çalışma başarısız olduysa nedeni (hata kodu). Başarıdan sonra boş. Her cihazda aynıdır.",
  "variable:system.audio.muted": "Ses sessize alınmış mı",

};

const NONE: Record<string, string> = {};
const TEXTS: Record<Language, Record<string, string>> = { tr: TR, en: NONE };

export function useCatalogText() {
  const { language } = usePreferences();
  const table = TEXTS[language] ?? NONE;

  const lookup = useCallback((key: string, fallback: string) => table[key] ?? fallback, [table]);

  return useMemo(() => ({
    categoryLabel: (id: string) => lookup(`category:${id}`, id),
    actionName: (a: ActionInfo) => lookup(`action:${a.type}`, a.displayName),
    actionDescription: (a: ActionInfo) => lookup(`actionDesc:${a.type}`, a.description ?? ""),
    variableDescription: (v: VariableInfo) => lookup(`variable:${v.name}`, v.description),
  }), [lookup]);
}
