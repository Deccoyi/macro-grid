# Macro Station: Windows Server + Android Client

> **Not (2026-09-22):** Bu belge orijinal, tek-repo halindeki plandır. Proje sonradan **iki bağımsız repoya bölündü** (server+editör burada; client https://github.com/Deccoyi/macro-station-client'ta, kendi sürümüyle) ve paylaşılan `client/packages/renderer` fikri terk edildi — her repo artık kendi bağımsız render motoru kopyasını taşıyor. Aşağıdaki `client/`, `server/` klasör yolları ve "ortak renderer" bölümü bu bölünmeden ÖNCEki mimariyi anlatıyor; genel ürün vizyonu hâlâ geçerli, sadece repo/klasör sınırları değişti. Güncel durum için [agent-notes.md](agent-notes.md)'ye bakın.

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

## Klasör yapısı
```
macro-station/
├── server/                         (.NET 10 solution)
│   ├── src/
│   │   ├── MacroStation.Host/      Sistem tepsisi (NotifyIcon) + WebView2 editör penceresi, Kestrel başlatır
│   │   ├── MacroStation.Core/      Profil/layout modeli, persist, değişken motoru, aksiyon çalıştırıcı, cihaz yönetimi
│   │   ├── MacroStation.Protocol/  WebSocket mesaj DTO'ları
│   │   ├── MacroStation.Plugin.Abstractions/  C# plugin SDK
│   │   ├── MacroStation.Scripting/ Jint tabanlı sandbox'lı JS plugin runtime
│   │   └── MacroStation.Windows/   SendInput, medya tuşları, CoreAudio, CPU/RAM, uygulama açma
│   ├── plugins/                    Obs/, Soundboard/, Audio/ (ses slider'ları), IconPacks/, WebView/ (chat)
│   ├── editor/                     React + Vite editör (build çıktısı Host'a gömülür)
│   └── tests/
└── client/                         (Capacitor + React + Vite + TS)
    ├── packages/renderer/          Grid + widget renderer'ları + CSS sanitizer + protocol tipleri
    ├── src/                        bağlantı, pairing, profil drawer'ı, sayfa gezinme, ayarlar, önbellek
    └── android/                    Capacitor Android projesi (+ native WebView overlay plugin'i)
```
**Ortak renderer:** `client/packages/renderer` hem telefonda hem editörde kullanılır. Editör onu `"@macro/renderer": "file:../../client/packages/renderer"` ile alır, böylece editörde ne görünüyorsa telefonda da birebir o görünür. Tarayıcı client da bu paketten çıkar: server `http://pc-ip:9820/` adresinde aynı client'ın web build'ini sunar, aynı ağdaki başka bir PC'nin tarayıcısı client olarak kullanılabilir.

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
- **Profil seçimi:** Profil aktif pencereye göre otomatik değişmeyecek, seçim client'tan yapılacak. Ekranın kenarından swipe ile açılan bir **profil drawer'ı** olacak, ayrıca `profile.switch` aksiyonu ile bir butondan da profil değiştirilebilecek.

## Mimari
- **Haberleşme:** Kestrel üzerinde düz WebSocket ve JSON mesajlar, port 9820.
  - Server → client: `layout.full`, `layout.patch`, `widget.state` (render edilmiş text, değer, stil override'ı), `asset` (ikon)
  - Client → server: `hello{deviceId, token}`, `widget.down/up/longPress/doubleTap`, `widget.value`, `page.change`, `profile.change`
  - Canlı güncellemeler en fazla ~10Hz gönderilir, yalnızca değer değiştiğinde.
- **Eşleştirme ve keşif:** mDNS (`_macrostation._tcp`), editörde QR kod (`ip:port + pairing kodu`), ilk bağlantıda 6 haneli PIN onayı, ardından her cihaza ayrı token verilir. Editörde bir cihaz listesi olur: isim, bağlı olup olmadığı, atanmış profil, token iptali.
- **Dinamik text:** `"Live: {obs.stream.duration}"`, format filtreleri de var: `{system.cpu|0}%`, `{system.time|HH:mm}`. `VariableStore` hangi widget'ın hangi değişkene bağlı olduğunu indeksler, bir değer değişince yalnızca etkilenen widget'ları yeniden render edip ilgili cihazlara gönderir.
- **Custom CSS güvenliği:** Her widget kendi Shadow DOM'unda çizilir. CSS `postcss` ile parse edilir ve boyut/konum özellikleri silinir (`width, height, min/max-*, position, inset, top/left/…, margin, grid-*, transform, zoom, display`). Dışarıdan `url()` çekmek engellenir, yalnızca asset ve data: URI'lerine izin var. Dış kutu `overflow:hidden` ile ölçüyü korur. Gradient border, gölge ve animasyon serbest.
- **Web widget (Kick/Twitch chat):** Önce iframe ile denenir (Twitch embed chat `parent=` parametresi ister). Siteler iframe'i engellerse (X-Frame-Options), Android tarafında yazılacak küçük bir Capacitor plugin'i **native WebView'ı widget'ın grid hücresinin tam üstüne** konumlandırır ve sayfa kaydırılınca/değişince senkron tutar. Not: chat için telefonun internete erişimi olmalı, server tarafında ise internet gerekmez.
- **Client dayanıklılığı:** Ekran açık tutulur (KeepAwake), tam ekran/kiosk modu (immersive, istenirse screen pinning), yön kilidi (yatay/dikey/otomatik, cihaz bazında). Bağlantı koparsa exponential backoff ile otomatik yeniden bağlanır, son layout ve asset'ler önbellekte tutulur ve bağlantı yokken ekranda "offline" rozeti gösterilir.
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
5. **Client:** Eşleştirme (mDNS/QR/PIN), cihaz bazında profil, profil drawer'ı, sayfa geçişi, kiosk/keep-awake/yön kilidi, yeniden bağlanma ve önbellek. Tarayıcı client da bu aşamada gelir.
6. **Plugin'ler:** Audio (ses slider'ları), Soundboard, OBS, IconPacks, WebView/chat (iframe ve native overlay).
7. **JS plugin runtime:** Jint sandbox'ı, izin sistemi, ayar arayüzü. `plugin-html` widget köprüsü.
8. **Paketleme:** `.msprofile` içe/dışa aktarma. Server için tek dosya publish + Inno Setup installer (Windows ile başlama seçeneğiyle), client için imzalı APK.

## Doğrulama
- `dotnet test`: şablon ayrıştırma ve filtreler, layout çakışma kontrolü, plugin yükleme, Jint sandbox testleri (CLR erişimi, sonsuz döngü, bellek aşımı ve izinsiz API çağrısı reddedilmeli), `.msprofile` round-trip.
- Vitest (`client/packages/renderer`): CSS sanitizer (`width`/`position` silinir, `linear-gradient` korunur), long press ve double tap zamanlaması.
- Uçtan uca test: 4x3 grid'de 2x2 bir buton, bir ses slider'ı ve bir Twitch chat web widget'ı oluşturulur, ayrıca ikinci bir sayfa ve ona geçiş butonu eklenir. İki cihaz (emülatör + tarayıcı) eşleştirilir ve her birine farklı profil atanır. Kontrol edilecekler:
  - Butonla Notepad'de `ctrl+v` çalışıyor
  - Slider Windows ses seviyesini değiştiriyor, Windows'tan değiştirilen ses de slider'a geri yansıyor
  - CPU değeri canlı güncelleniyor
  - `Live:{obs.stream.duration}` metni canlı akıyor
  - Wi-Fi kapatılıp açılınca otomatik yeniden bağlanıyor
  - Drawer ile profil değişiyor
