# Macro Station: Windows Server + Android Client

> **Not (2026-09-22):** Proje **iki bağımsız repoya bölündü**: bu repo server + editör, client ayrı bir repoda (`https://github.com/Deccoyi/macro-station-client`), her ikisi kendi sürümüyle. Paylaşılan `client/packages/renderer` fikri terk edildi — her repo artık kendi bağımsız render motoru kopyasını taşıyor (bilerek senkronize tutulmuyor). Klasör yapısı bölümü aşağıda **bu repo'nun gerçek/güncel haliyle** güncellendi. Genel ürün vizyonu ve mimari kararlar (Mimari/Plugin/Uygulama sırası bölümleri) hâlâ geçerli; hangi maddenin tamamlandığı satır satır işaretlendi. Güncel durum ve gerçek bug/fix kaydı için [agent-notes.md](agent-notes.md)'ye bakın.

## Bağlam
Stream Deck benzeri bir sistem. Android telefon/tablet ekranında grid'e oturan, özelleştirilebilir **widget'lar** olacak: buton, slider, web penceresi (Kick/Twitch chat) ve plugin'lerin eklediği özel içerikler. Bu widget'lar PC'de aksiyon çalıştıracak (kısayol, ses, OBS) ve PC'den gelen canlı verileri gösterecek (saat, CPU, OBS yayın süresi). Haberleşme çift yönlü ve yalnızca yerel ağda (LAN). Plugin desteği olacak. Arayüz server'daki editörde tasarlanacak ve widget'lara özel CSS yazılabilecek. `macro-station/` klasörü boş, proje sıfırdan kuruluyor.

**Stack:** Server tarafında **.NET 10 LTS** (C#), client tarafında **Capacitor + React + TypeScript**. Editör yalnızca server penceresinde (WebView2) açılacak.
> .NET 8 yerine .NET 10: .NET 8'in desteği Kasım 2026'da bitiyor.

## Ön gereksinimler (bilgisayar tarandı)
| Araç | Durum | Yapılacak |
|---|---|---|
| .NET SDK | 8.0.406 var, **10 yok** | **Kullanıcı kuracak:** .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`) |
| Node.js / npm | v24.16 / 10.8 ✅ | Yok |
| Git | 2.54 ✅ | Yok. Proje başında `git init` ben yapacağım |
| Android Studio + SDK | ✅ (`%LOCALAPPDATA%\Android\Sdk`, platform android-35, build-tools 35.0.1) | Capacitor'ın son sürümü android-36 isterse: Android Studio → SDK Manager → **Android 16 (API 36)** kurulacak (kullanıcı) |
| JDK | PATH'te yok, ama Android Studio'nun JBR'si var ✅ | **Kullanıcı ayarlayacak:** `JAVA_HOME=C:\Program Files\Android\Android Studio\jbr` ve `ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk` ortam değişkenleri, yoksa terminalden `gradlew` ile build alınamaz |
| WebView2 Runtime | ✅ kurulu | Yok |
| Test cihazı | ? | **Kullanıcı:** Telefonda Geliştirici Seçenekleri → USB hata ayıklama açılır (ya da Android Studio'dan bir emülatör oluşturulur). PC ve telefon aynı Wi-Fi'da olmalı |
| Windows Güvenlik Duvarı | — | İlk çalıştırmada 9820 portu için "Özel ağ" izni verilecek (kullanıcı onaylar). Wi-Fi ağ profili "Özel" olmalı, "Genel" profilde mDNS ve bağlantı engellenir |
| OBS (plugin aşaması) | — | OBS 28+ → Araçlar → WebSocket Sunucu Ayarları → etkinleştir, şifreyi not al |

## Klasör yapısı (bu repo — server + editör)
```
macro-station/                      (.NET 10 solution, repo kökü)
├── src/
│   ├── MacroStation.Host/          Sistem tepsisi (NotifyIcon) + WebView2 editör penceresi, Kestrel başlatır
│   ├── MacroStation.Core/          Profil/layout modeli, persist, değişken motoru, aksiyon çalıştırıcı, cihaz/pairing yönetimi
│   ├── MacroStation.Protocol/      WebSocket mesaj DTO'ları
│   ├── MacroStation.Plugin.Abstractions/  C# plugin SDK
│   └── MacroStation.Windows/       SendInput, medya tuşları, CoreAudio, CPU/RAM, uygulama açma
├── editor/                         React + Vite editör (build çıktısı Host'un wwwroot'una gömülür)
├── packages/renderer/              Grid + widget renderer'ları + CSS sanitizer (editörün kendi kopyası)
├── plugins/                        (henüz yok) Obs/, Soundboard/, Audio/, IconPacks/, WebView/
└── tests/
```
Client repo'su (`https://github.com/Deccoyi/macro-station-client`) ayrı: `src/` (bağlantı, pairing, profil drawer'ı, önbellek), `packages/renderer/` (kendi bağımsız kopyası), `android/`. İki repo'nun renderer'ı **kasıtlı olarak senkronize tutulmuyor** — biri diğerinden bağımsız değişebilir.

## Veri modeli
`%AppData%/MacroStation/profiles/*.json`
```
Profile { id, name, pages[] }
Page    { id, name, cols, rows, widgets[] }        ← birden fazla layout/sayfa
Widget  { id, type, x, y, w, h, style{bg, fg, align, valign, fontSize, border, radius, icon},
          customCss, props{…türe özel…}, bindings }
Device  { id, name, token, assignedProfileId, orientation, kiosk }   ← her cihaza ayrı profil
```
- **Widget türleri (built-in):** `button`, `toggle`, `slider` (yatay/dikey), `knob`, `label/gauge` (sadece gösterge, KPI), `image`, `web` (URL penceresi), `plugin-html` (plugin'in özel HTML widget'ı).
- **Buton olayları:** `press`, `release`, `longPress` (süresi ayarlanabilir), `doubleTap`. Her birine ayrı aksiyon atanabilir. Dokunuşta titreşim (haptic) açılıp kapatılabilir, widget bazında ya da global.
- **Slider/knob:** `value` alanı çift yönlü. Kullanıcı sürükledikçe `widget.value{id, v}` mesajı gider (throttled). Server tarafındaki değer değişince (ör. Windows ses seviyesi başka yerden değiştiyse) değer bir değişkene bağlanarak (`{audio.master}`) client'a geri gelir.
- **Sayfalar arası geçiş:** `page.goto`, `page.next/prev`, `page.back` aksiyonları. Bir sayfaya sığmayan widget'lar başka sayfalara konur. İstenirse client'ta yatay swipe ile de sayfa geçişi yapılabilir (ayar).
- **Profil seçimi (drawer ve butondan aksiyon tamam):** Profil aktif pencereye göre otomatik değişmiyor, seçim client'tan yapılıyor. Kenardan swipe (+ her zaman görünen ince tutamaç) ile açılan **profil drawer'ı tamam** (`profiles.list`/`profile.change` mesajları, `SessionDeviceController.SwitchProfileAsync`). Bir butondan da `core.profile` aksiyonuyla (`ProfileAction`, bkz. `PageActions.cs`) değiştirilebiliyor.

## Mimari
- **Haberleşme:** Kestrel üzerinde düz WebSocket ve JSON mesajlar, port 9820.
  - Server → client: `welcome` (+ pairing token), `layout.full`, `layout.patch` (henüz yok), `widget.state` (render edilmiş text, değer, stil override'ı), `profiles.list`, `asset` (ikon, henüz yok)
  - Client → server: `hello{deviceId, token}`, `widget.down/up/longPress/doubleTap`, `widget.value`, `page.change`, `profile.change`
  - Canlı güncellemeler en fazla ~10Hz gönderilir, yalnızca değer değiştiğinde.
- **Eşleştirme ve keşif (PIN + token + cihaz listesi + QR tamam; mDNS henüz yok):** ilk bağlantıda editörde gösterilen 6 haneli PIN onayı (`DeviceStore`/`PairingService`, `hello.pin`/`hello.token`, `welcome.token`), ardından her cihaza kalıcı ayrı token veriliyor. Editörde cihaz listesi var: isim, son görülme, kaldır (revoke). Editör her açılışta kısa ömürlü bir QR kod + PIN üretiyor (`PairingWindow.tsx`, `macrostation://pair?host=…&port=…&pin=…`), client tarayıp otomatik bağlanıyor (`QrScan.tsx`, `@capacitor-mlkit/barcode-scanning`). **Henüz yok:** mDNS (`_macrostation._tcp`) otomatik keşif — client tarafında native Android NSD köprüsü gerektiren bir Capacitor plugin'i ister, ayrı ve büyük bir iş; şu an IP hâlâ elle girilebiliyor ya da QR ile taranıyor.
- **Dinamik text:** `"Live: {obs.stream.duration}"`, format filtreleri de var: `{system.cpu|0}%`, `{system.time|HH:mm}`. `VariableStore` hangi widget'ın hangi değişkene bağlı olduğunu indeksler, bir değer değişince yalnızca etkilenen widget'ları yeniden render edip ilgili cihazlara gönderir.
- **Custom CSS güvenliği:** Her widget kendi Shadow DOM'unda çizilir. CSS `postcss` ile parse edilir ve boyut/konum özellikleri silinir (`width, height, min/max-*, position, inset, top/left/…, margin, grid-*, transform, zoom, display`). Dışarıdan `url()` çekmek engellenir, yalnızca asset ve data: URI'lerine izin var. Dış kutu `overflow:hidden` ile ölçüyü korur. Gradient border, gölge ve animasyon serbest.
- **Web widget (Kick/Twitch chat):** Önce iframe ile denenir (Twitch embed chat `parent=` parametresi ister). Siteler iframe'i engellerse (X-Frame-Options), Android tarafında yazılacak küçük bir Capacitor plugin'i **native WebView'ı widget'ın grid hücresinin tam üstüne** konumlandırır ve sayfa kaydırılınca/değişince senkron tutar. Not: chat için telefonun internete erişimi olmalı, server tarafında ise internet gerekmez.
- **Client dayanıklılığı (keep-awake + reconnect + son-layout önbelleği tamam; kiosk/yön kilidi henüz yok):** Ekran açık tutuluyor (KeepAwake). Bağlantı koparsa exponential backoff ile otomatik yeniden bağlanıyor, son layout `localStorage`'da host'a özel önbelleklenip soğuk başlangıçta anında gösteriliyor, bağlantı yokken "Çevrimdışı" / "Çevrimdışı · önbellek" rozeti var. **Henüz yok:** tam ekran/kiosk modu (immersive, screen pinning), yön kilidi, asset (ikon) önbellekleme.
- **Built-in aksiyonlar:** kısayol (SendInput), metin yazdırma, medya tuşları, ses seviyesi (master/uygulama bazında, mute), uygulama/dosya/URL açma, sayfa/profil geçişi, gecikme, çoklu aksiyon (sıralı makro), toggle.
- **Profil dışa/içe aktarma:** `.msprofile` bir zip dosyası: `profile.json` + kullanılan ikonlar/sesler + gereken plugin listesi. İçe aktarırken eksik plugin varsa kullanıcı uyarılır.

## Plugin sistemi
- **C# plugin'leri (tam yetkili, güvenilir):** `plugins/<Ad>/` klasöründe `plugin.json` manifesti ve DLL bulunur, her plugin kendi `AssemblyLoadContext`'inde yüklenir. `IPlugin.Initialize(IPluginHost)` üzerinden şunları kaydedebilir:
  - `IActionProvider`: aksiyon tipleri. Ayar şeması JSON Schema olarak tanımlanır, editördeki form bundan otomatik oluşturulur.
  - Değişken sağlayıcılar: `host.Variables.Set("obs.stream.duration", …)`
  - **Widget türleri:** Plugin ya built-in bir primitive'i kendi presetiyle yeniden kullanır (ör. Audio plugin'i kendi "Volume Slider"ını `slider` primitive'i + CoreAudio binding ile sunar), ya da `plugin-html` ile kendi HTML/JS paketini getirir. Bu paket client'ta sandbox'lı iframe içinde çalışır (`sandbox="allow-scripts"`, CSP) ve server ile yalnızca `postMessage` köprüsü üzerinden konuşur.
  - İkon paketleri
- **JS plugin'leri (Jint, sandbox'lı):**
  - Jint engine'ine CLR erişimi verilmez (`AllowClr` kapalı). Engine'e yalnızca izin verilen host API nesneleri enjekte edilir.
  - Manifest'te izinler bildirilir (`permissions: ["variables", "actions", "http:localhost:4455", "input"]`) ve ilk yüklemede kullanıcı bu izinleri editörde onaylar. İzni olmayan bir API çağrılırsa exception fırlatılır.
  - Kaynak limitleri: `TimeoutInterval`, `LimitMemory`, `MaxStatements`, `LimitRecursion`. Her plugin ayrı bir engine instance'ında, ayrı bir thread'de çalışır. Sürekli hata veren plugin otomatik olarak devre dışı bırakılır.
  - Dosya sistemine, process başlatmaya veya keyfi ağ erişimine izin verilmez. Ağ erişimi yalnızca manifest'te beyan edilen host:port'lara, host'un sağladığı `fetch` üzerinden yapılabilir.
  - Plugin ayarları JSON Schema ile tanımlanır ve editörde her plugin için otomatik bir ayar sayfası oluşur.
- Built-in `system.*` değişkenleri (saat, CPU, RAM) de aynı sistemi kullanan dahili bir plugin olarak yazılır.

## Editör
Sayfa ve profil yönetimi (ekleme, sıralama, kopyalama). Grid boyutu ayarı. Widget paletinden sürükle-bırak, snap, kenarından boyutlandırma (w×h) ve çakışma kontrolü. Stil paneli: renk, hizalama, border, gradient, ikon seçici. Monaco ile CSS editörü ve sanitize uyarıları. Aksiyon atama (press/long/double). Değişken ekleme için otomatik tamamlama (`{` yazınca öneriler gelir). Canlı önizleme. Cihaz listesi, plugin listesi ve izin onayları. İçe/dışa aktarma.

## Uygulama sırası
1. **İskelet:** İki proje oluşturulur. WebSocket bağlantısı kurulur, sabit bir buton gösterilir ve basınca PC'de `ctrl+c` çalışır.
2. **Core:** Profil, sayfa ve widget modeli, kaydetme, built-in aksiyonlar, VariableStore ile `system.*` değişkenleri.
3. **Renderer:** Grid, Shadow DOM, CSS sanitizer, `button/toggle/label/image/slider/knob` widget'ları, long press/double tap/haptic.
4. **Editör:** Madde madde yukarıda sayılanlar.
5. **Client (Aşama 5 tamamen bitti, ayrı repo):** Eşleştirme — PIN ve QR tamam, mDNS henüz yok. Cihaz bazında profil ataması tamam (Eşleştirme penceresinde cihaz başına profil `<select>`'i, atanmamışsa ilk profile düşülüyor). Profil drawer'ı ve client'tan `profile.switch` tamam. Sayfa geçişi client'tan tetikleniyor (yatay swipe → `page.next`/`page.prev`). Keep-awake + yeniden bağlanma + son-layout önbelleği tamam. Kiosk modu (immersive/tam ekran) ve yön kilidi tamam, drawer içinden Ayarlar paneliyle açılıp kapanıyor. Tarayıcı client (`webclient/`, aynı grid mantığı, server'dan `/deck/` altında servis ediliyor) tamam. **Gerçek cihazda USB debug testi de yapıldı (2026-09-23):** `adb install` ile debug APK gerçek bir Samsung telefona kuruldu, önceden eşleştirilmiş cihaz LAN üzerinden yeniden bağlandı, canlı değişken push'ı ve dokunma girişi doğrulandı — ayrıntı için `macro-station-client/docs/plan.md`.
6. **Plugin loader'ı tamam; ilk gerçek plugin (OBS) yazıldı, editörde ayar arayüzü hariç.** `IPlugin`/`IPluginHost` (`MacroStation.Plugin.Abstractions`, SDK `0.2.0` — `IPluginHost` artık `DataDirectory`/`Log` da veriyor), her plugin kendi izole `AssemblyLoadContext`'inde (`MacroStation.Core/Plugins/PluginLoadContext.cs`), `%AppData%/MacroStation/plugins/` klasör taraması + `plugin.json` doğrulama (`sdkVersion` caret aralığı, `minServerVersion`) `PluginLoader.LoadAll` ile sunucu her açıldığında bir kez çalışıyor (DI container kurulmadan önce). Editörde "Eklentiler" penceresi gerçek listeyi (`GET /api/plugins`) gösteriyor ve "Klasörden Yükle…" ile native klasör seçme diyaloğu üzerinden bir plugin klasörünü `plugins/<id>/`'e kopyalıyor (devreye girmesi için sunucu yeniden başlatılmalı — sıcak yükleme yok). JS/Jint çalıştırma motoru yok (Aşama 7), `kind:"js"` manifestolar tanınıyor ama "Uyumsuz" gösteriliyor. Ayrıntı ve plugin yazma rehberi: `macro-station-plugins/docs/plugin-authoring.md`. Ses (master volume slider/mute) plan'da plugin olarak listelenmişti ama built-in aksiyon olarak Core'a taşındı (`core.setVolume`/`core.setMute`/`core.toggleMute`, `system.audio.master`/`system.audio.muted` değişkenleri) — bkz. Aşama 2. **OBS plugin'i** (`macro-station-plugins/OBS/`, önceki `_backup/obs-plugin-reference/`'daki taslak temel alındı) obs-websocket v5'e bağlanıyor, sahne/yayın/kayıt/ses aksiyonları ve `obs.*` değişkenlerini sağlıyor; bağlantı ayarları (host/port/şifre) editörde "Eklentiler" penceresinden OBS satırının yanındaki dişli düğmeyle düzenlenebiliyor (jenerik `GET`/`PUT /api/plugins/{id}/settings` ham-JSON köprüsü üzerinden plugin'in kendi `settings.json`'ını okuyup yazıyor — host'ta hâlâ genel bir ayar şeması/form üretici yok, bu form OBS'e özel yazıldı). Soundboard/IconPacks/WebView-chat plugin'leri hâlâ yazılmadı.
7. **JS plugin runtime:** Jint sandbox'ı, izin sistemi, ayar arayüzü. `plugin-html` widget köprüsü.
8. **Paketleme:** `.msprofile` içe/dışa aktarma. Server için tek dosya publish + Inno Setup installer (Windows ile başlama seçeneğiyle), client için imzalı APK.
9. **Widget metin/ikon dinamizasyonu:** Buton/toggle/label widget'larının metin (`widget.text`) ve ikon (`style.icon`/`style.iconName`) alanlarına, "Mantık kur" (Dynamize) mekanizmasıyla (`DynamizeModal.tsx`, `DynamicFieldLabel.tsx`) aynı şekilde state'e bağlı dinamizasyon eklenecek — bugün bu mekanizma yalnızca `AppearanceFields.tsx`'teki `style.background/foreground/borderColor/animation` alanlarında var. Kapsam: `TextFields.tsx`'teki metin `<textarea>`'sının yanına `DynamicFieldLabel` eklenip `widget.dynamic["text"]` ile koşullu string döndürülebilecek; ikon seçimi için `resultKind={{select:[...]}}` (mevcut ikon paketi seçenekleriyle, `animation` alanındaki select deseninin aynısı) kullanılacak — böylece belirli koşullarda farklı ikon gösterilebilecek. `button`, `toggle`, `label` widget türlerini kapsar (üçü de `TextFields.tsx`'i paylaşıyor).

## Doğrulama
- `dotnet test`: şablon ayrıştırma ve filtreler, layout çakışma kontrolü, plugin yükleme, Jint sandbox testleri (CLR erişimi, sonsuz döngü, bellek aşımı ve izinsiz API çağrısı reddedilmeli), `.msprofile` round-trip.
- Vitest (`packages/renderer`): CSS sanitizer (`width`/`position` silinir, `linear-gradient` korunur), long press ve double tap zamanlaması.
- Uçtan uca test: 4x3 grid'de 2x2 bir buton, bir ses slider'ı ve bir Twitch chat web widget'ı oluşturulur, ayrıca ikinci bir sayfa ve ona geçiş butonu eklenir. İki cihaz (emülatör + tarayıcı) eşleştirilir ve her birine farklı profil atanır. Kontrol edilecekler:
  - Butonla Notepad'de `ctrl+v` çalışıyor
  - Slider Windows ses seviyesini değiştiriyor, Windows'tan değiştirilen ses de slider'a geri yansıyor
  - CPU değeri canlı güncelleniyor
  - `Live:{obs.stream.duration}` metni canlı akıyor
  - Wi-Fi kapatılıp açılınca otomatik yeniden bağlanıyor
  - Drawer ile profil değişiyor
