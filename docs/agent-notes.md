# AI Agent Notları

Bu projede çalışan AI agent'lar (ve geliştiriciler) için bağlam, kurallar ve bilinen tuzaklar. Genel plan için: [plan.md](plan.md). Plan değiştikçe ve yeni notlar çıktıkça bu klasör güncellenmeli.

> **Repo bölündü (2026-09-22):** Bu repo artık yalnızca **server + editör**'ü içeriyor. Telefon/tablet client'ı ayrı bir repoda ve bağımsız sürümleniyor: https://github.com/Deccoyi/macro-station-client. Paylaşılan grid/widget render motoru (`packages/renderer`) her iki repoda da **bağımsız birer kopya** olarak duruyor — kasıtlı bir karar (kullanıcı: "neden ortak ki, biri buton gösteriyor öbürü uygulama"), senkronize tutulmuyor. Aşağıdaki notlarda geçen `client/` yol referansları bölünmeden ÖNCEki (tek repo) duruma ait tarihsel kayıtlardır; bu repoda artık `client/` klasörü yok.

## Durum (2026-09-22)
- **Aşama 1 server tarafı tamam:** WebSocket (`/ws`, port 9820), `hello` → `welcome` + `layout.full`, `widget.down/up` → aksiyon, `core.hotkey` + `core.typeText` aksiyonları, JSON profil deposu, tray uygulaması, dosya logları, geçici test sayfası (`src/MacroStation.Host/wwwroot/index.html`).
- 18 birim testi geçiyor (`dotnet test MacroStation.slnx`).
- **Henüz yok:** kimlik doğrulama/eşleştirme (Aşama 5), `client/` projesi, editör, değişkenler, plugin'ler.
- **Kullanıcı telefondan test etti, çalışıyor** (kısayol + metin yazma).
- **Aşama 2 tamam:** VariableStore/Template, `system.*` değişkenleri, sayfa/profil geçişi (`SessionDeviceController`), toggle widget'lar, `core.page`/`core.profile`/`core.open`/`core.delay` aksiyonları. 44 birim testi geçiyor. WebSocket akışı elle uçtan uca doğrulandı (bkz. aşağıdaki tasarım notunun sonundaki gerçekleşen davranış).
- **Aşama 3 tamam:** `client/packages/renderer` — `Grid`, `WidgetView` (+ `ButtonContent`/`ImageContent`/`SliderContent`/`KnobContent`/`PlaceholderContent`), `ShadowHost`, `sanitizeWidgetCss`, `widgetBaseCss`, `usePressGesture`. 26 birim testi geçiyor, `client/packages/renderer/demo` (Vite) ile gerçek tarayıcıda görsel/etkileşim doğrulaması yapıldı.
- **Renk sistemi için [color-bible.md](color-bible.md)'ye bak** (editör chrome token'ları + widget swatch seti). Aşama 4'te gerçek CSS'e geçirilecek.
- **Aşama 4 tamam:** `editor` (React + Vite) — profil/sayfa yönetimi, sürükle/boyutlandır/çakışma engelleme, stil paneli, CSS editörü (sanitize uyarılı), aksiyon editörü (press/release/longPress/doubleTap veya toggleOn/toggleOff), tek seferlik değişken önizlemesi. Server tarafında `/api/profiles` (GET/POST/PUT/DELETE), `/api/actions`, `/api/variables/snapshot` eklendi. Editör build'i `wwwroot/editor`'a gömülüyor, tray'deki **"Düzenleyiciyi aç"** gerçek bir WebView2 penceresinde açıyor (sistem tarayıcısında değil — bu kullanıcının kararıydı). Uçtan uca gerçek server'a karşı tarayıcıda test edildi (kaydet/yükle/sil, sürükle-bırak, sanitize uyarısı, aksiyon atama) — **WebView2 penceresinin kendisi native bir Windows kontrolü olduğu için görsel olarak doğrulanamadı**, sadece derlendiği ve aynı web içeriğini açacağı doğrulandı. Kullanıcı tray'den denemeli.
- **Aşama 4 sonrası kullanıcı geri bildirimiyle genişletildi (bu turda):**
  - Editör UX düzeltmeleri: sürükle/boyutlandırırken düşük opaklıklı önizleme + hedef hücre vurgusu (`EditorCanvas.tsx`); profil adı artık ayrı bir kalem/modal ile değişiyor (dropdown ile karıştırılmıyor); kısayol artık **gerçek tuş yakalama** ile giriliyor (yazmak yok, `HotkeyCapture.tsx`, fiziksel `KeyboardEvent.code` kullanıyor, layout'tan bağımsız); aksiyon olay sekmeleri artık taşmıyor (`flexWrap`); `core.open` ikiye ayrıldı: `core.openUrl` (URL, varsayılan tarayıcı) ve `core.open` (uygulama, **native Gözat dialogu** — `IUiDialogService`/`UiDialogService`, WinForms UI thread'ine `SynchronizationContext` ile marshal ediliyor).
  - **Kategorili/aranabilir seçici kalıbı** (`PickerShell.tsx`) hem değişken seçicide hem ikon seçicide kullanılıyor: sol tarafta kategori/paket listesi + arama. Değişkenler `VariableInfo.Category` ile etiketleniyor (plugin'ler kendi kategorilerini ekleyebilir); ikon paketleri de aynı şekilde genişletilebilir (`ICON_PACKS` listesi, şimdilik sadece "Lucide").
  - **İkon sistemi tamamlandı:** lucide-react (1539 ikon, ISC lisans, `lucide-react/dynamicIconImports` ile lazy-loaded) gömüldü, arama+seç modali var. İkon boyutu ve konumu (üst/alt/sol/sağ) `WidgetStyle.iconSize`/`iconPosition` ile ayarlanabiliyor — ikon vektör olduğu için boyut değişikliği yeniden render gerektirmiyor. İkon **rengi** değiştirmek ikonun yeniden "pişirilmesini" gerektiriyor (renk SVG'nin içine gömülü bir data-URI); bunun için `WidgetStyle.iconName` (editör-only, hangi lucide ikonundan pişirildiği) saklanıyor, renk değişince aynı isimle yeniden pişiriliyor.
  - **Koşullu (dinamik) stil sistemi eklendi** — kullanıcının "cpu>50 ise kırmızı" örneği ve AND/OR/XOR/NOT ile birleşik koşul isteği üzerine:
    - Model tamamen **veri**, kod/eval yok: `Widget.Dynamic: Dictionary<string, DynamicBinding>` (property path → kural), `DynamicBinding{Cases, Default}`, `DynamicCase(ConditionNode, Result)`, `ConditionNode{Kind: compare|and|or|xor|not, Variable, Operator, Value, Value2, Children}`. Operatörler: `>,>=,<,<=,==,!=,between`. Güvenlik mimari olarak garanti (yorumlayıcı/reflection/programın başka yerine erişim yok) — bkz. `Core/Variables/DynamicRuleEvaluator.cs`.
    - Dinamize edilebilir alanlar: `style.background`, `style.foreground`, `style.borderColor`, `style.animation` (Yok/Yanıp sönme/Nabız — `WidgetStateService.DynamizableProperties`). Animasyon salt CSS `@keyframes` (`ms-blink`/`ms-pulse`, `widgetBaseCss.ts`), kod/eval yok. İkon rengi bilerek kapsam dışı bırakıldı (yukarıdaki "pişirme" sorunu yüzünden ayrı bir iş).
    - `DynamizeModal` UI'ı ("Mantık kur") tamamen pill-tabanlı yeniden tasarlandı (kullanıcının açık isteğiyle): değişken/operatör/VE-VEYA-XOR/sonuç renkli pill'ler, `resultKind` prop'u ile hem renk (color picker pill) hem sabit seçenek (animasyon gibi select pill) sonuçlarını destekliyor. Bkz. `DynamizeModal.tsx`, `ResultKind` tipi.
    - Server, değişken değiştiğinde ilgili widget'ların dinamik kurallarını yeniden değerlendirip `widget.state`'in yeni `Style: Dictionary<string,string>?` alanıyla client'a push ediyor (`ClientSession.SentStyles` ile değişmeyince tekrar göndermiyor).
    - Editör: `AppearanceFields`'taki her dinamize edilebilir alanın yanında ⚡ ikonu (`DynamicFieldLabel.tsx`), tıklayınca `DynamizeModal.tsx` açılıyor (koşul satırları + VE/VEYA/XOR + "değilse" + between için iki değer + else-if zinciri + varsayılan). Editörün kendi canlı önizlemesi için TS tarafında aynı mantığın bir kopyası var (`grid/evaluateDynamic.ts`) — **gerçek çalışma zamanı davranışı her zaman server'daki `DynamicRuleEvaluator`'dır**, ikisi ayrı tutulmalı, biri değişirse diğeri de güncellenmeli.
    - UI şu an sadece "düz" koşul grupları düzenleyebiliyor (tek seviye and/or/xor, iç içe değil); daha derin iç içe ağaçlar (örn. hazır JSON'dan) `DynamizeModal` açıldığında "desteklenenden karmaşık" uyarısıyla düzleştiriliyor. Model tamamen recursive olduğu için ileride UI'ı iyileştirmek mümkün.
    - 84 server testi bunu kapsıyor (`DynamicRuleEvaluatorTests.cs`, `VariableCatalogTests.cs`).
  - Widget tipine özel Inspector alanları: Web/Plugin artık kendi URL alanını gösteriyor (Etiket'le aynı görünmüyor), Görsel kendi `props.src` alanını gösteriyor, Slider/Knob kendi min/max/adım alanlarını + dürüst bir not gösteriyor ("şu an hiçbir plugin bu değeri kullanmıyor").
  - `widgetBaseCss`'e varsayılan buton stili eklendi (`#2d3136` arka plan, `#e6e7ea` yazı, `8px` radius) — stil verilmemiş widget artık tuvalle karışmıyor.
  - `wwwroot/index.html` (geçici test client) artık gerçek uzun-basma/çift-dokunma algılıyor (`usePressGesture` mantığının vanilla JS kopyası) — bu, "uzun basma/çift dokunma çalışmıyor" şikayetinin asıl sebebiydi (test client hiç bu mesajları göndermiyordu).
  - **Bilinen sınırlamalar (kullanıcıya bildirildi):** native "Gözat" dialogu headless/otomasyonda test edilemedi (gerçek Windows dialogu açıyor), kod incelemesiyle doğrulandı. Ana bundle ~500KB (lucide + editör), code-splitting ileride yapılabilir.
  - **Widget özellik paneli (Inspector) `ui-guidelines.md`'e göre yeniden tasarlandı:** kart-içinde-kart yerine düz yüzey + ince ayırıcı + küçük başlık (`SectionLabel`), her widget tipinde aynı sıra: Görünüm → İçerik (tipe özel) → Aksiyonlar. Renk alanları tek satır swatch+hex (`ColorField`), hizalama artık ikonlu segmented control (`Seg`, lucide `AlignLeft/Center/Right` + `AlignVerticalJustify*`) — dropdown değil. Paylaşılan kontroller `editor/src/panels/fields/controls.tsx`'te; yeni bir alan eklerken oradaki `SectionLabel`/`ColorField`/`Seg`'i kullan, ham `<select>`/kart kutusu ekleme.
- **Aşama 5 başladı (Capacitor client):** `client/` artık kendi başına bir Vite+React+TS uygulaması (önceden sadece `packages/renderer` workspace root'uydu). `package.json` (Capacitor 8.5.2 — 6.x'te `@capacitor-community/keep-awake` peer'ı 7+ istiyordu, tüm paketler 8'e sabitlendi), `capacitor.config.ts` (`allowMixedContent: true` — ws:// LAN bağlantısı için), `src/ws/connection.ts` (hello/layout.full/widget.state, exponential backoff reconnect), `src/deviceId.ts` (localStorage'da kalıcı UUID), `src/App.tsx` (IP gir → bağlan ekranı + `@macro/renderer` ile aynı grid'i çizen ana ekran, press/release/longPress/doubleTap sunucuya gönderiliyor). `npx cap add android` çalıştırıldı, `android/` projesi oluştu; `AndroidManifest.xml`'e elle `android:usesCleartextTraffic="true"` eklendi (cap sync bu dosyayı ezmez, elle düzenlemek kalıcı).
  - **Profil çekmecesi + client'tan profil değiştirme tamam:** `profiles.list` (hello'da push) + `profile.change` mesajları, `SessionDeviceController.SwitchProfileAsync` üzerinden. Client'ta kenardan swipe + her zaman görünen ince tutamaç ile açılan drawer. Gerçek sunucuya karşı uçtan uca doğrulandı (profil değiştirip geri dönmek dahil).
  - **PIN eşleştirme tamam (mDNS/QR hâlâ yok):** `DeviceStore`/`PairingService`, `hello.token`/`hello.pin`, `welcome.token`. **Kırıcı değişiklik:** artık her cihaz (test client dahil) ilk bağlantıda editördeki "Eşleştirme" panelinde gösterilen PIN'i girmeli — önceden eşleşmiş hiçbir cihaz yok, herkes bir kere PIN girmek zorunda. `wwwroot/index.html` test client'ı da `prompt()` ile PIN akışını destekliyor. mDNS otomatik keşif ve QR kod ile PIN gösterme henüz yok (mDNS için native Android NSD köprüsü gerektiren bir Capacitor plugin'i yazmak gerekiyor — kapsamı büyük, ayrı bir iş).
  - **Keep-awake eklendi** (`@capacitor-community/keep-awake`), bir profil yüklüyken ekran uyumuyor.
  - Bulunan gerçek buglar: `ButtonContent`'te ikon üstte/altta iken hizalama eksenleri karışıyordu (düzeltildi, hem server hem client repo'sunda); client'ta React 18 StrictMode'un efekt-iki-kez-çalıştırma davranışı yüzünden eski bağlantının gecikmeli `onclose`'u durumu yanlışlıkla "disconnected" yapabiliyordu (düzeltildi — `ServerConnection.destroyed` bayrağı); PIN ile eşleştikten sonra durum "pairing_required"da takılı kalıyordu, çünkü `retryWithPin` sonrası hiçbir yer "connected"a çekmiyordu (düzeltildi — `welcome` mesajı her zaman "connected" set ediyor artık).
  - **Offline/son-layout önbelleği tamam** (client repo'da): her `layout.full` host'a özel `localStorage`'a yazılıyor, soğuk başlangıçta sunucuya ulaşılamasa bile son grid anında gösteriliyor ("Çevrimdışı · önbellek" rozeti). Pairing her zaman önbelleğin önüne geçiyor. Sunucuyu gerçekten kapatıp açarak doğrulandı. Ayrıntı için client repo'nun kendi `docs/agent-notes.md`'sine bak.
  - **Henüz yok:** mDNS/QR otomatik eşleştirme, kiosk modu, gerçek cihaz/emülatör testi (şimdiye kadar sadece tarayıcıda `npm run dev` ile ve bir kez `assembleDebug` ile doğrulandı).
- **Sürümleme kuralları için [versioning.md](versioning.md)'ye bak.** Özellikle: `development` → `main` merge'inden önce versiyon bump'ı (ve MAJOR/MINOR/PATCH) HER SEFERİNDE kullanıcıya sorulur, sessizce yapılmaz.

## Aşama 3'ten kritik öğrenimler (Aşama 4/5'te tekrar karşına çıkar)

**React + Shadow DOM + portal'da event iki kez ateşlenebilir.** `ShadowHost`, widget içeriğini `createPortal` ile bir shadow root'a render ediyor. Pointer olaylarını host div'e JSX prop'u olarak (`onPointerDown={...}`) vermek, **tek bir native event için handler'ı 2 kez çağırdı** — React'ın sentetik event sistemi, portal + shadow-root sınırında fiber ağacını iki farklı yoldan aynı node'a yürüyor (bilinen bir React davranışı). Çözüm: `ShadowHost` içinde pointer olayları JSX prop değil, `useLayoutEffect` içinde gerçek `addEventListener`/`removeEventListener` ile bağlanıyor (bkz. [ShadowHost.tsx](../packages/renderer/src/style/ShadowHost.tsx)). **Shadow root'a portal edilen herhangi bir yere yeni bir DOM event handler'ı eklerken bunu unutma** — JSX prop'u değil, native listener kullan.

**`file:` bağımlılığıyla bağlanan paketlerde React çoğalması.** `@macro/renderer`'ı `npm install`'la kendi `node_modules`'i olacak şekilde kurup başka bir projeye `file:../..` ile bağlarsan, iki proje **iki farklı React kopyasına** sahip olabilir (symlink gerçek yola çözülüyor, oradan `node_modules` yukarı doğru aranıyor). Bunun belirtisi: bir dokunuşun/tıklamanın state güncellemesini 2 kez tetiklemesi. Bu yüzden `client/` artık bir **npm workspace kökü** (`client/package.json` → `workspaces: ["packages/renderer", "packages/renderer/demo"]`), tek bir hoisted `react`/`react-dom` paylaşılıyor — paket içi `node_modules` olmamalı. **Aşama 4'te editör (`editor`) renderer'ı `file:../../packages/renderer` ile bağlayacak; bu, client workspace'inin dışında ayrı bir npm kökü olduğu için aynı riski taşıyor.** Önlem: editör'ün `vite.config.ts`'inde `resolve.dedupe: ["react", "react-dom"]` mutlaka olmalı; kurulumdan sonra bir tıklamanın tam olarak bir kez tetiklendiği elle doğrulanmalı (bkz. `client/packages/renderer/demo/vite.config.ts`'deki yorum).

**`setPointerCapture` geçersiz/pasif bir `pointerId` ile atılırsa exception fırlatabilir** ve try/catch'e alınmazsa `onPress` hiç çalışmadan handler'ı sessizce keser. `usePressGesture` bunu try/catch içine aldı — benzer native DOM API çağrılarında (özellikle pointer capture/release) aynı korumayı unutma.

**Renderer demo:** `client/packages/renderer/demo` (Vite + React) — `@macro/renderer`'ın tüm widget tiplerini gösteren, elle test edilebilen küçük bir sayfa. `.claude/launch.json`'a `renderer-demo` olarak kayıtlı, `preview_start({name:"renderer-demo"})` ile açılır. Kalıcı bir ürün parçası değil, geliştirme sırasında görsel doğrulama için var — silinmedi çünkü Aşama 4/5'te de işe yarayabilir.

## Aşama 2 tasarımı (uygulanacak)
**Plugin.Abstractions'a eklenecek:**
- `IVariableStore { void Set(string name, object? value); object? Get(string name); }`
- `IVariableProvider { Task RunAsync(IVariableStore store, CancellationToken ct); }`: Host'ta bir `BackgroundService` tüm provider'ları çalıştırır.
- `IDeviceController { ShowPageAsync(pageId), NextPageAsync(), PreviousPageAsync(), BackAsync(), SwitchProfileAsync(profileId) }`
- `ActionContext`'e `IDeviceController Device` alanı eklenir.

**Core:**
- `Variables/VariableStore`: thread-safe, değer değişmezse event tetiklenmez, `Changed(name)` event'i.
- `Variables/TemplateRenderer`: `{name|format}` biçimini çözer. `{{` ve `}}` literal süslü parantez üretir. Geçersiz token olduğu gibi yazılır, olmayan değişken boş string olur. Parse sonucu template string'ine göre cache'lenir. Formatlama kuralları:
  - Sayılar: varsayılan format `0.##`
  - DateTime: varsayılan `HH:mm`
  - TimeSpan: kullanıcı `hh:mm:ss` yazar, harf olmayan karakterler `\` ile escape edilir. Varsayılan `hh\:mm\:ss`, gün varsa `d\.hh\:mm\:ss`
  - bool: `Açık/Kapalı`, özel format `EVET/HAYIR` biçiminde (`/` ile ayrılır)
- `Sessions/SessionRegistry`: `_sessions` ClientHub'dan buraya taşınır. Bu, ClientHub ile WidgetStateService arasındaki döngüsel bağımlılığı kırmak için.
- `Sessions/WidgetStateService`:
  - Değişen değişken adlarını dirty set'te biriktirir, 100ms'de bir flush eder (en fazla 10Hz).
  - Flush sırasında her session için, profilindeki widget'lardan Text'i değişen değişkenlere referans verenleri render eder. Çıkan metin session'daki `SentTexts[widgetId]` cache'inden farklıysa `widget.state` gönderir.
  - `SendInitialAsync(session)` layout gönderildikten sonra çağrılır, tüm template'li widget'ların ve toggle'ların durumunu yollar.
  - Toggle durumları profil+widget bazında tutulur, aynı profili gösteren tüm cihazlara yayınlanır.
- `Sessions/SessionDeviceController`: sayfa geçişi (`session.PageHistory` stack'i, next/prev sona gelince başa sarar) ve profil değişimi (`layout.full` + initial states gönderir). `hello` işlemi de layout'u bunun üzerinden gönderir.
- **Yeni aksiyonlar:**
  - `core.page {mode: goto|next|prev|back, pageId?}`
  - `core.profile {profileId}`
  - `core.open {target, arguments?}` (`Process.Start`, `UseShellExecute`)
  - `core.delay {ms}` (en fazla 60000)
  - Çoklu aksiyon (makro) = aynı event'e bağlı sıralı aksiyon listesi, zaten destekleniyor.
- **Toggle:** `widget.Type == toggle` olan bir widget'a basıldığında durum çevrilir ve `WidgetEvents.ToggleOn/ToggleOff` (`"toggleOn"/"toggleOff"`) event'i dispatch edilir.

**Protocol:** `page.show {pageId}` (server → client) eklenir. `WidgetStateMessage`'a `Active bool?` alanı eklenir.

**Windows:** `SystemMetricsProvider : IVariableProvider` saniye sınırına hizalı olarak her saniye çalışır. CPU için `GetSystemTimes`, RAM için `GlobalMemoryStatusEx` P/Invoke kullanılır; PerformanceCounter paketi gerekmez. Değişkenler: `system.time` (DateTime), `system.cpu` (%), `system.ram` (%), `system.ram.used`/`system.ram.total` (GB), `system.uptime` (TimeSpan).

**Test sayfası (`wwwroot/index.html`):** `widget.state` (text, active) ve `page.show` mesajlarını işlemeli. Metinlerde `white-space: pre-line` kullanılmalı (`\n` ile satır atlama). Kullanıcı sayfayı yerelde değiştirirse `page.change` göndermeli.

**Varsayılan profil:** Kullanıcının diskindeki mevcut `Varsayılan` profil otomatik üretildi, elle düzenlenmedi. Yeni varsayılan profille değiştirilebilir. Yeni profilde olacaklar:
- Sayfa 1: saat (2x2 label, `{system.time|HH:mm:ss}` + tarih), CPU ve RAM label'ları, "Sessiz" toggle'ı (volumemute), "Sayfa 2 →" butonu
- Sayfa 2: "← Geri", "Notepad aç" (`core.open`), "Tümünü kopyala" makrosu (ctrl+a → delay 50 → ctrl+c)

**Testler:** TemplateRenderer (formatlar, escape, eksik değişken), VariableStore (aynı değer event tetiklemez), sahte `IDeviceController` ile `core.page`.

**Uyarı:** Build almadan önce çalışan `MacroStation.exe` kapatılmalı (tray → Çıkış), yoksa `bin/` kilitli kalır.

## Aşama 4'ten öğrenimler
- **Editör API'sinde boş gövdeli 200 tuzağı:** `PUT`/`DELETE` başarı durumunda `Results.Ok()` (200, boş gövde) dönülüyordu; client `res.json()` çağırınca "Unexpected end of JSON input" fırlatıyordu — **istek aslında başarıyla tamamlanmış oluyordu**, sadece client'ta gösterilen hata yanlıştı. Düzeltme: server `Results.NoContent()` (204) dönüyor, client'taki `json<T>()` yardımcı fonksiyonu önce `res.text()` okuyup boşsa parse etmiyor. Yeni bir API endpoint'i eklerken boş-gövdeli başarı durumunu unutma.
- **Profil değiştirmede "dirty" koruması olmadan veri kaybı riski:** Kaydedilmemiş değişiklik varken profil değiştirmek/yeni profil oluşturmak in-memory state'i sessizce eziyordu. `useEditorState.ts`'e `confirmDiscardIfDirty()` (native `confirm()`) ve `beforeunload` koruması eklendi. **Yeni bir "profil/sayfa değiştir" akışı eklerken bu korumayı çağırmayı unutma.**
- **npm workspace + `file:` bağımlılığı iki farklı npm kökünde (client/ ve editor/) React'ı yine çoğaltabilir.** `editor/vite.config.ts`'de `resolve.dedupe: ["react","react-dom"]` bu yüzden var; kaldırma.
- **Geliştirme sırasında CORS yerine Vite proxy kullanıldı:** `editor/vite.config.ts`'deki `server.proxy` `/api` ve `/ws`'yi gerçek çalışan server'a (`:9820`) yönlendiriyor, böylece server'da CORS açmaya gerek kalmadı. Production'da zaten aynı origin'den (`wwwroot/editor`) servis ediliyor.
- **Editör build'i iki yere kopyalanmalı:** `editor/dist` → hem `src/MacroStation.Host/wwwroot/editor` (kaynak, git'e girecek) hem gerekirse doğrudan `bin/Debug/net10.0-windows/wwwroot/editor` (çalışan sürece anında yansısın diye, normal `dotnet build` zaten Content olarak kopyalar ama server çalışırken elle test ediyorsan iki yere de kopyala). İleride bunu bir MSBuild target'ı ile otomatikleştirmek iyi olur (`npm run build` + kopyalama tek komutla).
- **Test verisiyle gerçek kullanıcı profilini karıştırma riski:** Editörü elle test ederken kullanıcının gerçek "Varsayılan" profiline yanlışlıkla bir test widget'ı kaydedilmişti (profil değiştirme sırasında oluşan bir yarış nedeniyle). API üzerinden fark edilip düzeltildi. **Editörü/API'yi elle test ederken kullanıcının gerçek profiliyle değil, `+ Profil` ile oluşturulan ayrı bir test profiliyle çalış, işin bitince o profili sil.**
- WebView2 paketi (`Microsoft.Web.WebView2`) hem WinForms hem WPF kontrolünü içeriyor, sadece WinForms kullanılıyor olsa da build'de zararsız bir `MSB3277 WindowsBase` uyarısı çıkıyor — göz ardı edilebilir.
- Aksiyon ayarları formu şu an sadece bilinen `core.*` tipleri için özel form gösteriyor (`editor/src/panels/actionForms/forms.tsx`); bilinmeyen/plugin action tipleri için ham JSON formuna (`GenericJsonForm`) düşüyor. Aşama 6'da plugin action'ları JSON Schema ile tanımlarsa bu dosyaya schema-tabanlı bir form üretici eklenmeli.
- CSS editörü şu an düz `<textarea>` (`CssEditor.tsx`); plan Monaco öngörüyor, henüz eklenmedi (syntax highlight yok, ama sanitize uyarıları çalışıyor).
- Editördeki değişken önizlemesi (`renderPreviewText` in `EditorCanvas.tsx`) ham değişken değerini basıyor, sunucudaki `Template.Render`'ın yaptığı `{name|format}` biçimlendirmesini uygulamıyor — yalnızca "değişken orada mı" diye kaba bir önizleme, tam biçimli değil.

## Kullanıcı tercihleri
- Kullanıcı Türkçe konuşuyor; kullanıcıya dönük metinler (UI, hata mesajları) Türkçe, kod/yorumlar İngilizce.
- Profil **aktif pencereye göre otomatik değişmeyecek** (yayında aktif pencere OBS olmuyor). Profil seçimi client'ta kenardan swipe drawer + `profile.switch` aksiyonu ile.
- Widget'lar yalnızca buton değil: slider, web/chat penceresi, plugin HTML widget'ları da grid'e oturur.
- JS plugin'lerinde güvenlik şart (Jint sandbox, izin manifesti, kaynak limitleri).
- Planlar ve agent notları bu `docs/` klasöründe tutulur.

## Yapı ve kurallar
- İki bağımsız proje: `server/` (.NET 10, `MacroStation.slnx`) ve `client/` (Capacitor + React + TS). Ortak renderer `client/packages/renderer`; server editörü onu `file:` bağımlılığıyla kullanacak.
- Protokol: her frame `{ "type": "...", "data": {...} }`. Tipler `src/MacroStation.Protocol/MessageTypes.cs`. JSON camelCase (`ProtocolJson.Options`). Client TS tipleri bununla birebir eşleşmeli.
- Aksiyonlar `IActionHandler` (Plugin.Abstractions) uygular; built-in'ler de plugin'lerle aynı arayüzü kullanır. Tip id'leri `core.*`, plugin'ler `<plugin>.*`.
- Kısayol yazımı `HotkeyParser` üzerinden (`ctrl+shift+s`, `+` tuşu = `plus`). Yeni tuş adı eklenirse `KnownKeys` **ve** `VirtualKeys` güncellenmeli (bunu kontrol eden test var).
- Her client'ın aksiyonları sırayla, receive loop dışında bir kuyrukta çalışır (`ClientHub`).
- Profiller `%AppData%\MacroStation\profiles\*.json` (atomik yazım: tmp + rename). Loglar `%AppData%\MacroStation\logs`.

## Ortam ve tuzaklar
- Makinede global NuGet kaynağı **tanımlı değil**; `nuget.config` bu yüzden var, silme.
- `.NET 8` ve `.NET 10.0.401` SDK kurulu; hedef `net10.0` / `net10.0-windows`.
- Host projesi `Microsoft.NET.Sdk` + WinForms + `FrameworkReference Microsoft.AspNetCore.App`; Web SDK'nın implicit using'leri `GlobalUsings.cs`'de.
- `JAVA_HOME` = Android Studio JBR, `ANDROID_HOME` = `%LOCALAPPDATA%\Android\Sdk` (kullanıcı ortam değişkeni olarak ayarlandı). SDK'da android-35 var; Capacitor android-36 isterse SDK Manager'dan kurulmalı.
- Geliştirme makinesi şirket domain ağında; güvenlik duvarı 9820'yi engelleyebilir.
- Bash'te heredoc içinde tek tırnaklı metin bazen bozuluyor; çok satırlı dosyaları Write aracıyla yaz.
- Testlerde `widget.down` göndermek geliştiricinin aktif penceresine gerçek tuş basar; otomatik testlerde `IInputService` sahte (fake) olmalı.
- Server çalışırken `bin/` kilitli olur; yeniden build öncesi tray'den Çıkış yap veya process'i kapat.
- **KRİTİK: editör her rebuild sonrası mutlaka `bin/Debug/.../MacroStation.exe` yeniden başlatılmalı.** `wwwroot/editor` kaynak ağaçtaki kopya güncellense bile, çalışan .exe kendi `bin/Debug/net10.0-windows/wwwroot/editor`'ından servis ediyor — o kopya yalnızca `dotnet build` ile tazeleniyor. Ayrıca WebView2/tarayıcı `*.html`'i (öncesinde cache-control hiç ayarlanmamıştı) agresif önbelleğe alıyordu; bu yüzden dosyalar sunucu tarafında güncel olsa bile eski JS bundle görünmeye devam edebiliyordu. Artık `ServerApp.cs`'te `.html` yanıtlarına `Cache-Control: no-cache, no-store, must-revalidate` ekleniyor (bkz. CHANGELOG) ama yine de her frontend değişikliğinden sonra akış şu olmalı: `npm run build` (editor) → `dist`'i `wwwroot/editor`'a kopyala → çalışan .exe'yi durdur → `dotnet build` → yeniden başlat.
- **Arayüzde emoji/metin-sembol ikon yasak** — kullanıcının açık talebi: her ikon ihtiyacında `lucide-react`'ten gerçek bir bileşen kullan (zaten proje bağımlılığı, `IconPicker.tsx`'te de kullanılıyor), emoji veya Unicode sembol (✎, ⚡, ↑↓, ×, → vb.) asla yazma.
