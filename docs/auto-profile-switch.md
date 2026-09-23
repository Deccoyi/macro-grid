# Aktif pencereye göre otomatik profil geçişi

## Bağlam
Bugün profil yalnızca elle değişiyor (client drawer, `profile.change` ya da `core.profile` aksiyonu). `docs/agent-notes.md` ve `docs/plan.md` bu yüzden "otomatik değişmeyecek" diyordu (bkz. bu dosyanın kaydedilmesiyle güncellenen karar). Kullanıcının asıl isteği farklı: yayın profili açıkken Spotify ya da media player öne gelince Spotify düzenine geçsin; Spotify kapanınca bir önceki profile dönsün; hiçbir şey eşleşmezse varsayılan profile düşsün. Client'taki drawer'da bir **kilit** olacak. Kilit otomatik geçişi durdurur ama drawer ve buton ile elle geçiş her zaman çalışır. Otomatik geçişi her cihaz kendi açar (opt-in).

Çalışma `dev` branch'inde yapılacak. Değişiklik üç repoya dokunuyor: `macro-station` (server + editör), `macro-station-client` (telefon), ayrıca protokol.

## Davranış modeli (her oturum için bir yığın)
Her `ClientSession` bir `AutoSwitchState` tutar: `Stack<Entry>` ve `Locked`. Her `Entry` şu alanlardan oluşur: `{ ProfileId, Source: Manual | Rule, ProcessName? }`.

- **Tanımlı bir pencere öne gelirse** (process adı eşleşir): o kural yığında zaten varsa en üste taşınır, yoksa eklenir. Ardından o profile geçilir.
- **Tanımsız bir pencere öne gelirse:** hiçbir şey olmaz. Son eşleşen profilde kalınır. Oyun, MacroStation'ın kendi penceresi ve masaüstü de bu gruba girer.
- **Tanımlı pencere kapanırsa:** "Kapandı" demek, process'in görünür üst düzey penceresi kalmadı demek (tray'e gizlenen Spotify da kapanmış sayılır). Bu durumda yığındaki ölü `Rule` kayıtları atılır ve üstteki kayda dönülür.
- **Yığın boşalırsa:** varsayılan profile dönülür. Önce cihaza atanmış profile bakılır (`PairedDevice.AssignedProfileId`), yoksa yeni `AppPreferences.DefaultProfileId` kullanılır, o da yoksa `profiles.First()`.
- **Elle seçim** (drawer ya da `core.profile`): yığına bir `Manual` kaydı eklenir. Bu kayıt taban gibi davranır ve process kontrolüyle atılmaz. Sonra tanımlı başka bir pencere öne gelirse onun üstüne geçilir; o pencere kapanınca tekrar elle seçilen profile dönülür. Yani "yayın profili (elle) → Spotify açılır → Spotify kapanır → yayın profili" akışı doğal olarak çalışır.
- **Kilitliyken:** pencere olayları yok sayılır. Elle geçişler çalışmaya devam eder. Kilit açıldığında durum bir kez yeniden değerlendirilir.
- **Opt-in:** `PairedDevice.FollowActiveWindow = false` olan cihazlar bu mantığa hiç girmez.

## Değişiklikler: `macro-station` (server)

**1. Model ve kalıcılık**
- `Core/Model/Profile.cs`: `List<AppMatch> AppMatches` eklenecek. `AppMatch` şu alanları taşır: `{ ProcessName (örn. "Spotify.exe"), TitleContains? }`. Kurallar profilin üzerinde durduğu için "bu profil Spotify'da açılsın" diye düşünmek kolay oluyor. Birden fazla profil aynı pencereyle eşleşirse `ProfileStore.All` sırasındaki ilk profil kazanır. `ProfileValidator`'a boş ya da tekrarlı kural kontrolü eklenecek.
- `Core/Model/PairedDevice.cs`: `bool FollowActiveWindow` ve `bool AutoSwitchLocked` eklenecek. Kilit kalıcı olacak, böylece yeniden bağlanınca korunur. Bunları yazmak için `DeviceStore`'a mevcut `AssignProfile` desenini izleyen setter'lar eklenecek.
- `Core/Preferences/AppPreferences.cs`: `string? DefaultProfileId` eklenecek.
- Varsayılan profil çözümlemesi tek bir yardımcıda toplanacak: `ProfileResolver.ResolveDefault(device)`. `ClientHub.OnHelloAsync` (`ClientHub.cs:201`) da bu yardımcıyı kullanacak.

**2. Windows tarafı: `MacroStation.Windows/Windows/ForegroundWindowMonitor.cs` (yeni)**
- CPU bütçesi için polling yerine olay tabanlı çalışacak: kendi message loop'u olan ayrı bir thread üzerinde `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)`. Olay geldiğinde `GetWindowThreadProcessId` ile process adı, `GetWindowText` ile başlık okunur ve `ForegroundChanged` event'i yayınlanır.
- `HasVisibleWindow(processName)`: `EnumWindows` + `IsWindowVisible` + `GetWindowThreadProcessId` ile bakar.
- Kapanma tespiti iki yoldan yapılacak. Birincisi her foreground olayında budama; ikincisi yığında `Rule` kaydı olan bir oturum varsa 2 sn'lik bir `PeriodicTimer`. Yığında kural yokken timer çalışmaz.
- P/Invoke, `WindowsInputService.cs:75`'teki `DllImport` stiliyle yazılacak.
- Test edilebilirlik için Core'da bir arayüz olacak: `IActiveWindowSource` (event'ler + `HasVisibleWindow`).

**3. Core: `Sessions/AutoProfileSwitcher.cs` (yeni, `IHostedService`)**
- `IActiveWindowSource` olaylarını dinler. Her olayda `SessionRegistry.All` içinden `FollowActiveWindow` açık ve kilitsiz oturumları gezer, yukarıdaki yığın mantığını uygular.
- Geçişi `SessionDeviceController.SwitchProfileAsync` ile yapar (`SessionDeviceController.cs:22`). Aktif profil zaten hedef profilse hiçbir şey göndermez.
- Yığın mantığı saf bir sınıfta tutulacak (`AutoSwitchState.OnForeground / Prune / OnManual`) ki unit test yazmak kolay olsun.
- Kayıt `ServerApp.Build` içinde, `AddHostedService` ile (`ServerApp.cs:85` civarı) yapılacak.

**4. Elle geçişin kaydı**
- `SwitchProfileAsync`'e bir `origin` parametresi eklenecek (`Manual` varsayılan, `Auto`). `Manual` olduğunda `AutoSwitchState.OnManual` çağrılır. Mevcut çağıranlar (drawer, `ProfileAction`) değişmeden `Manual` olarak kalır. `IDeviceController` imzası plugin SDK'sında olduğu için orada overload/varsayılan parametre kullanılacak ki plugin'ler kırılmasın.

**5. Protokol (`MacroStation.Protocol`)**
- Client'tan server'a yeni `profile.lock { locked: bool }` mesajı. `ClientHub.HandleMessageAsync` bunu işler: `DeviceStore`'a yazar, kilit açılıyorsa durumu yeniden değerlendirir.
- `profiles.list` mesajına `autoSwitch: { enabled, locked }` alanı eklenecek. Kilit ya da ayar değişince bu mesaj yeniden gönderilir. Ek alan olduğu için eski client'lar bozulmaz. Sürüm `docs/versioning.md`'ye göre artırılacak.

**6. REST ve editör**
- `GET /api/system/windows`: görünür penceresi olan process'lerin listesi (ad + başlık). Editörde "çalışan uygulamadan seç" özelliği için kullanılacak.
- `PUT /api/devices/{id}/follow-window` (bağlıysa canlı oturuma da uygulanır). `PUT /api/preferences` `DefaultProfileId`'yi zaten taşıyacak.
- Editör:
  - Profil ayarlarına "Otomatik etkinleştir" bölümü gelecek: `AppMatches` listesi, çalışan uygulama seçici ve elle exe adı girişi. Ekle/sil deseni `PreferencesWindow.tsx:55-77`'den alınacak.
  - `PreferencesWindow`'a "Profiller" kategorisi ve varsayılan profil seçici eklenecek.
  - `PairingWindow.tsx`'te cihaz başına "Aktif pencereyi takip et" anahtarı olacak.
  - Metinler `i18n/tr.ts` ve `en.ts` dosyalarına eklenecek.

**7. Dokümanlar**
- `agent-notes.md` ve `plan.md`'deki eski karar ("otomatik değişmeyecek") yeni kararla güncellendi: otomatik geçiş opt-in, yığın ile geri dönülüyor, kilit var. `CHANGELOG.md`'ye uygulama bittiğinde kayıt düşülecek.

## Değişiklikler: `macro-station-client`
- `src/ws/connection.ts`: `setProfileLock(locked)` eklenecek, `profiles.list` içindeki `autoSwitch` okunacak.
- `src/App.tsx` → `ProfileDrawer`: `autoSwitch.enabled` ise başlıkta kilit ikonu ya da toggle gösterilecek. Kilitliyken bir durum göstergesi olacak. Profil listesi ve elle seçim her zaman çalışır.
- `webclient/` (tarayıcı deck'i) bu adımın dışında kalıyor.

## Kapsam dışı
- Tam ekran veya oyun algılama, başlık regex'i, sayfa bazlı (profil değil) otomatik geçiş.
- `system.activeApp` değişkeni. Kolay bir ek olur ama bu aşamanın işi değil.

## Doğrulama
- **Unit testler** (`tests/MacroStation.Tests`), sahte `IActiveWindowSource` ile:
  - tanımsız pencere → değişiklik yok
  - Spotify öne → Spotify profili; Spotify kapan → önceki profil
  - elle seçim → kural → kapan → elle seçilen profil
  - yığın boş → varsayılan profil zinciri
  - kilitliyken olaylar yok sayılır, elle geçiş çalışır; kilit açılınca yeniden değerlendirme
  - opt-in kapalı cihaz etkilenmez
- `ProfileStoreTests` ve validator testleri `AppMatches` ile genişletilecek.
- **Manuel uçtan uca test:**
  1. Host'u çalıştır, telefonu bağla. Pairing'de takip anahtarını aç.
  2. Spotify profiline `Spotify.exe` kuralı ekle, yayın profilini elle seç.
  3. Spotify'ı öne al → geçmeli. Oyun ya da Explorer'a geç → Spotify profilinde kalmalı. Spotify'ı kapat → yayın profiline dönmeli.
  4. Drawer'dan kilitle → Spotify öne gelse de geçmemeli, drawer'dan elle geçiş çalışmalı.
- Görev Yöneticisi'nde host'un boşta CPU'sunun değişmediğini kontrol et (hafif kaynak bütçesi).
