# Devir notu — 2026-09-23 (gece), aktif pencereye göre otomatik profil geçişi

Önceki oturum bu özelliği baştan sona uyguladı ve commit'ledi (push'landı). Kullanıcı bu makineden
(uzak masaüstü/remote) başka bir bilgisayara (evdeki) geçti — oradan `git pull` ile devam edecek.
Bir sonraki ajan buradan devam etsin.

## Durum

- Üç repo da (`macro-station`, `macro-station-client`, `macro-station-plugins`) `dev` branch'inde,
  **tamamı commit'li ve push'lı**:
  - `macro-station`: commit `e38e972` — "feat: auto-switch profile based on the active foreground window"
  - `macro-station-client`: commit `7be2742` — "feat: profile drawer lock for server-side auto profile switch"
  - `macro-station-plugins`: bu özellikle ilgili değişiklik yok, zaten temiz.
- Tasarım dokümanı: `macro-station/docs/auto-profile-switch.md` (tüm plan burada, adım adım).
- `macro-station/docs/plan.md`:53 ve `docs/agent-notes.md`:119 civarı güncellendi (eski "otomatik
  değişmeyecek" kararı tersine döndü).
- `macro-station/docs/CHANGELOG.md` ve `macro-station-client/docs/CHANGELOG.md`'de `[Unreleased]`
  altında birer madde var.

## Ne çalışıyor, doğrulandı

- **Derleme:** `dotnet build` (Host dahil, tüm proje) hatasız. `dotnet test`: 122/122 yeşil
  (yeni `AutoSwitchStateTests` + `ProfileValidatorTests`'e eklenen 3 test dahil).
- **`AutoSwitchState`** (saf yığın/kilit mantığı, `src/MacroStation.Core/Sessions/AutoSwitchState.cs`):
  tanımsız pencere no-op, kural eşleşmesi → geçiş, kapanınca önceki profile dönüş, elle seçim → kural →
  kapanınca elle seçilene dönüş, kilitliyken olaylar yok sayılıyor ama elle geçiş çalışıyor, aynı kural
  tekrar eşleşince no-op, derinde kalan bir kural tekrar öne gelince yığının tepesine taşınıyor —
  hepsi unit test'lerle kapsanıyor.
- **`GET /api/system/windows`** gerçek sunucuya karşı canlı denendi, gerçek açık pencerelerin
  process adı + başlığını doğru döndürdü (`ForegroundWindowMonitor.ListVisibleWindows`,
  `EnumWindows`/`GetWindowThreadProcessId`/`GetWindowText` — bu P/Invoke altyapısı çalışıyor).
- **`PUT /api/devices/{id}/follow-window`** ve **`PUT /api/profiles/{id}` içinde `appMatches`**
  canlı sunucuya karşı denendi, doğru kaydediliyor/dönüyor.
- Editör (`npm run typecheck` + `npm run build` temiz) ve `macro-station-client`
  (`npm run typecheck` + `npm run build` temiz) — ikisi de derleniyor.

## Ne doğrulanmadı — buradan devam

**Tek eksik doğrulama: gerçek bir Windows ön-plan-penceresi (foreground) olayının
`ForegroundWindowMonitor`'daki `SetWinEventHook` callback'ini gerçekten tetikleyip tetiklemediği.**
Bu makine (uzak masaüstü/RDP) üzerinden `SetForegroundWindow`/`AppActivate` gibi yollarla pencere
öne getirilemedi (Windows'un "foreground lock" kısıtı — arka planda çalışan bir otomasyon script'i
gerçek kullanıcı etkileşimi olmadan başka bir pencereyi öne zorlayamıyor), o yüzden uçtan uca canlı
test yapılamadı.

**Yeni makinede yapılacak manuel test:**
1. `git pull` (üç repo da), `dotnet build` (macro-station), `npm run build` (editor/ ve
   macro-station-client varsa), host'u çalıştır.
2. Editörde bir profile (ör. mevcut "kamil" test profiline) bir `AppMatch` kuralı ekle — sol panelde
   profil butonlarının yanındaki pencere ikonuna tıkla, "Çalışan uygulamadan seç" ile örn. Not Defteri
   (`notepad.exe`) veya Hesap Makinesi (`CalculatorApp.exe`) seç.
3. Eşleştirme penceresinde test cihazının (telefon veya tarayıcı `/deck/`) "Aktif pencereyi takip et"
   anahtarını aç.
4. O uygulamayı (ör. Not Defteri) gerçekten öne getir (fiziksel tıklama/Alt+Tab ile, otomasyon
   script'i ile değil) — cihazın otomatik olarak o profile geçtiğini doğrula.
5. Uygulamayı kapat — cihazın önceki profile döndüğünü doğrula.
6. Drawer'daki kilide bas — uygulamayı tekrar öne getir, profilin DEĞİŞMEDİĞİNİ, ama drawer'dan elle
   profil seçmenin hâlâ çalıştığını doğrula.
7. Görev Yöneticisi'nde host'un CPU kullanımının boşta artmadığını gözle kontrol et (event-driven
   olması bekleniyor, polling yok).
8. Hepsi çalışırsa: `docs/plan.md` ve `docs/CHANGELOG.md`'deki "gerçek OS olayıyla doğrulanmadı"
   notlarını kaldır, `docs/handoff-2026-09-23-auto-profile-switch.md`'yi (bu dosya) sil ya da
   `docs/done/`'a taşı.
9. Çalışmazsa en olası şüpheliler: `ForegroundWindowMonitor.RunMessageLoop`'un thread'i gerçekten
   başlıyor mu (bir log/breakpoint ekle), `SetWinEventHook`'un dönüş değeri `IntPtr.Zero` mu (izin/
   session sorunu olabilir — RDP/uzak masaüstü oturumlarında `WINEVENT_OUTOFCONTEXT` bazen normal
   çalışır ama garanti değil, `WINSTA0\Default` dışında bir session'da hook kurulamayabilir).

## Kullanıcı profili verisi (git'te DEĞİL)

Kullanıcının gerçek profilleri (`%AppData%/MacroStation/profiles/*.json`), eşleşmiş cihazları
(`devices.json`, token'lar dahil) ve tercihleri (`preferences.json`) git repolarının içinde değil —
bu makineye özel. Önceki oturum bunları şuraya kopyaladı (git'e dahil değil, `.gitignore` dışı bir
klasör, taşınabilir):

```
macro-station-main/macro-station-userdata-export/
├── devices.json
├── preferences.json
└── profiles/
    ├── 0de64efde57f.json   (TelefonYatay — kullanıcının gerçek PLC ladder-logic profili)
    └── 692eac1f92c7.json   (kamil — test/scratch profili)
```

Yeni makinede devam etmeden önce bu klasörün içeriğini oradaki `%AppData%/MacroStation/`'a
(`profiles/`, `devices.json`, `preferences.json`) kopyala — yoksa kullanıcı sıfırdan boş bir
kurulumla karşılaşır ve gerçek profili kaybolmuş görünür (kaybolmadı, sadece bu export klasöründe
duruyor).

## Küçük açık kalemler (bu özellikten önceki oturumdan, hâlâ açık)

- "Silinmiş sahneye bağlı aksiyon hata versin" (`ObsTargetCheck`) — gerçek cihazdan zaten doğrulandı,
  kapalı sayılabilir.
- Aksiyon-hatası durum çubuğu uyarısı (`core.actionError`) bir sonraki olaya kadar temizlenmiyor —
  küçük bir arka plan görevi olarak not edilmişti (`task_7bdc17a1`), hâlâ açık.

## Faydalı notlar (tekrar)

- **Editör build'i:** `editor/` içinde `npm run build`, sonra `dist`'i hem
  `src/MacroStation.Host/wwwroot/editor`'a hem
  `src/MacroStation.Host/bin/Debug/net10.0-windows/wwwroot/editor`'a kopyala.
- **Kilitli dosyalar:** Host build'i almadan önce çalışan `MacroStation.exe`'yi kapat.
- **Repo yapısı:** Üç repo da bağımsız — `macro-station` (server+editör), `macro-station-client`
  (Android/Capacitor), `macro-station-plugins` (OBS + PLC İkonları). Hiçbirinde `.sln` yok, her
  proje kendi `.csproj`'u ile derlenip test ediliyor.
