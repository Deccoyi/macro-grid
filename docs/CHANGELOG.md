# Changelog

Bu dosya [Keep a Changelog](https://keepachangelog.com/) formatını takip eder. Sürümleme kuralları için [versioning.md](versioning.md)'ye bakın.

## [Unreleased]
### Added
- Aşama 5 başladı: `client/` artık Capacitor + React + TS uygulaması (Capacitor 8.5.2). IP girip bağlanma ekranı, `@macro/renderer` ile aynı grid'i çizen ana ekran, WebSocket reconnect (exponential backoff). `android/` platformu eklendi.
- Server iskeleti: WebSocket protokolü, JSON profil deposu, tray uygulaması, dosya logları, geçici test client sayfası (Aşama 1).
- Canlı değişkenler (`system.time`, `system.cpu`, `system.ram`, ...), sayfa/profil geçişi, toggle widget'ları, `core.page`/`core.profile`/`core.open`/`core.delay` aksiyonları (Aşama 2).
- `client/packages/renderer`: paylaşılan grid/widget render motoru — `Grid`, `WidgetView`, `ShadowHost`, `sanitizeWidgetCss`, `usePressGesture` (press/longPress/doubleTap/haptic) (Aşama 3).
- `docs/color-bible.md`: editör chrome renk token'ları ve widget swatch seti.
- `server/editor`: profil/sayfa/widget editörü (sürükle-boyutlandır-çakışma engelleme, stil paneli, CSS sanitize uyarıları, aksiyon editörü), `/api/profiles`+`/api/actions`+`/api/variables/snapshot`, tray'de WebView2 penceresinde açılıyor (Aşama 4).

- Koşullu (dinamik) widget stili: `Widget.Dynamic` + `DynamicRuleEvaluator` (AND/OR/XOR/NOT ile birleşen karşılaştırmalar, salt veri — kod/eval yok), `widget.state.style` ile canlı push.
- Kategorili/aranabilir seçici kalıbı (`PickerShell`), hem değişken hem ikon seçicide; lucide-react ikon kütüphanesi (1539 ikon) gömüldü.
- İkon boyutu/konumu/rengi ayarlanabiliyor; `core.open` URL/Uygulama olarak ikiye ayrıldı, uygulama seçimi native dosya dialogu ile.
- Kısayol girişi artık gerçek tuş yakalama ile (yazma yok).
- Widget'a "Animasyon" (Yok/Yanıp sönme/Nabız) eklendi; diğer stil alanları gibi dinamize edilebiliyor (örn. cpu>80 iken yanıp sönsün). Salt CSS `@keyframes` — kod/eval yok, aynı güvenlik sınırı korunuyor.
- Dinamizasyon penceresi ("Mantık kur") baştan tasarlandı: her koşul artık renkli pill'ler (değişken/operatör/VE-VEYA-XOR/sonuç) ile "cümle" gibi okunuyor, `docs/color-bible.md` token'larıyla tutarlı.
- Widget özellik paneli (Inspector) `docs/ui-guidelines.md`'e göre baştan düzenlendi: kart-içinde-kart yerine düz yüzey + ince ayırıcı + küçük başlık dili (Görünüm/İçerik/Aksiyonlar), her widget tipinde aynı sıra ve görünüm. Renk alanları artık tek satırlık swatch+hex; hizalama artık dropdown değil ikonlu segmented control (lucide `AlignLeft/Center/Right`, `AlignVerticalJustify*`). Ortak kontroller `panels/fields/controls.tsx`'te (`SectionLabel`, `ColorField`, `Seg`).

### Fixed
- Editör API'sinde `PUT`/`DELETE` başarı yanıtları artık `204 No Content` (önceden boş gövdeli `200`, client'ta yanlış "JSON ayrıştırılamadı" hatasına yol açıyordu).
- Test client'ta (`wwwroot/index.html`) uzun basma/çift dokunma hiç uygulanmamıştı; eklendi.
- Stil verilmemiş widget'lar artık tuvalle karışmıyor (varsayılan buton rengi eklendi).
- Editör, değişken anlık görüntüsünü (`/api/variables/snapshot`) yalnızca açılışta bir kez çekiyordu; dinamik stil önizlemesi bu yüzden "sabit" kalıyordu (kayıtlı kural aslında doğru çalışıyordu, sadece canlı CPU/RAM değişimini yansıtmıyordu). Artık 2 saniyede bir otomatik yenileniyor.
- `/api/*` yanıtlarına da `Cache-Control: no-cache` eklendi — aksi halde bir kayıttan hemen sonraki GET, tarayıcı önbelleğinden eski veri dönebiliyordu ("bazen kaydediyor bazen etmiyor" hissi buradan geliyordu).
- Test client'ta (`wwwroot/index.html`) `widget.state.style`/`animation` hiç uygulanmıyordu — sunucudan gelen dinamik renk/animasyon push'ları sessizce yok sayılıyordu.
- Editör HTML dosyaları artık `Cache-Control: no-cache` ile sunuluyor — önceden WebView2/tarayıcı eski `index.html`'i (ve onun referans verdiği eski JS bundle'ını) önbellekten göstermeye devam ediyordu, editör yeniden derlenip sunucu yeniden başlatılsa bile. Hash'li `assets/*.js`/`*.css` dosyaları hâlâ önbelleklenebilir.
- Arayüzdeki tüm emoji/sembol ikonlar (✎, ⚡, ↑/↓, ×, →) kaldırılıp `lucide-react` ikonlarıyla değiştirildi (Pencil, Zap, ChevronUp/Down, X, ArrowRight).
