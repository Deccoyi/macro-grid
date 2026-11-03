# Teknik başvuru

## Eylem türleri

Yerleşik türler `core.*`, eklenti eylemleri `<eklenti kimliği>.*` biçimindedir.

| Tür | Ayarlar |
|---|---|
| `core.hotkey` | `keys`, örneğin `ctrl+shift+s` |
| `core.typeText` | metin |
| `core.open` | `target`, argümanlar |
| `core.openUrl` | `url` (`http://` veya `https://`) |
| `core.delay` | `ms`, en fazla 60000 (düzenleyici Bekle'yi saniye olarak gösterir) |
| `core.if`, `core.else`, `core.endIf`, `core.stop` | Mantık adımları: `when` ayarlı bir If (`condition`, `previousFailed` ya da `previousOk`), bir Else, onun End'i ve Stop |
| `core.variable.set`, `core.variable.add`, `core.variable.toggle`, `core.variable.reset` | `user.<name>` değişkeni üzerinde Değişkene değer ata eylemleri; `value` ya da `amount` ile |
| `core.page` | `mode`: bir sayfaya git, `next`, `prev`, `back` |
| `core.profile` | profil |
| `core.setVolume` | slider veya knob değerini kullanır |
| `core.setMute` | `mute` true veya false |
| `core.toggleMute` | yok |

OBS eklentisi: `obs.setScene`, `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode`, `obs.setTransition`, `obs.setProfile`, `obs.startStream`, `obs.stopStream`, `obs.toggleStream`, `obs.startRecord`, `obs.stopRecord`, `obs.toggleRecord`, `obs.pauseRecord`, `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay`, `obs.setMute`, `obs.toggleMute`, `obs.setVolume`, `obs.adjustVolume`, `obs.setItemVisibility`, `obs.setText`.

## Widget türleri ve olaylar

Profil dosyalarındaki türler: `button`, `toggle`, `slider`, `knob`, `label`, `image`, `web`, `plugin-widget`, `plugin-html` (`plugin-html` eski bir yer tutucudur, yalnızca boş bir kutu çizer).

| Widget | Olaylar |
|---|---|
| Buton | `press`, `release`, `longPress`, `doubleTap` |
| Toggle | `toggleOn`, `toggleOff` |
| Slider, Knob | `valueChange` |

## Eklenti ayar alanları

Bir eklentinin ayar sayfası alanlardan oluşur. Türler şunlardır: `Text`, `Password`, `Number`, `Slider`, `Bool`, `Select`, `Segmented`, `File`, `List`, `Button`, `Notice`, `Variable`, `Color`, `Hotkey`, `Duration` ve `MultiSelect`. En yeni üçü:

| Tür | Ne işe yarar |
|---|---|
| `Hotkey` | Tuşlara basarak alınan bir tuş kombinasyonu. |
| `Duration` | Milisaniye, saniye ya da dakika olarak gösterilebilen bir süre. |
| `MultiSelect` | Seçeneklerden birkaçı, her biri ayrı ayrı işaretlenir. |

`Password` alanı, JavaScript eklentide de C# eklentide de geçerli Windows kullanıcısı için korunarak saklanır (bunu ekleyen Macro Grid sürümünden itibaren; değişiklik günlüğüne bakın). `host.settings.get()` betiğinize düz değeri verir. Düzenleyici değeri asla geri göstermez, yedekler de bu değeri içermez. Ayarlar başka bir Windows kullanıcısıyla veya başka bir bilgisayarda açılırsa değer okunamaz: boş gelir ve Hata Listesi kişiden yeniden girmesini ister. Eski bir sürümün kaydettiği değer okunabilir kalır ve ilk yüklemede diskte korunmuş hale getirilir. `List` alanının satırlarındaki parolalar da korunur ve düzenleyiciden gizlenir; bir satır parolasını yalnızca aynı sırada kalıp diğer değerleri değişmediği sürece korur. `Password` değeri metin olmalıdır.

**Kural:** bir sır tutan eklenti `Password` alanı (JavaScript eklentinin ayar sayfasını sunucu korur) veya `IPluginSecrets` kullanmalıdır (C# eklenti kendi ayar dosyasını yazar, bu yüzden sunucu zorunlu kılamaz).

## Eklentiden düğme resimleri

Bir eklenti düğmeler için raster resim verebilir. Profil resmin kendisini tutmaz, yalnızca `plugin-image:<eklentiId>/<resimId>` başvurusunu tutar (resim kimliği 1 ile 128 karakter; harf, rakam, `.`, `_`, `:` ve `-`). Sunucu başvuruyu yerleşimi gönderirken resme çevirir; cihaz başvuruyu hiç görmez.

| | JavaScript | C# |
|---|---|---|
| Resim istenir | `host.images.onRequest(ids => { ... })` | eklenti sınıfı `IPluginImageSource.RequestImages(IReadOnlyList<string> imageIds)` uygular |
| Resim verilir | `host.images.put(imageId, dataUrl, version?)` (kabul edilirse `true`) | `IPluginHost.Images.Put(imageId, dataUrl, version = null)` (`bool` döner) |
| Resmin değiştiğini bildir | `host.images.invalidate(imageId, version?)` | `IPluginHost.Images.Invalidate(imageId, version = null)` |

Sunucu çağrı başına en fazla 20 kimlik ister, 500 ms'de en fazla bir çağrı yapar ve en fazla 200 açık istek tutar; yanıtlanmayan kimlik 10 dakika sonra yeniden istenir. Resim yalnızca son 10 dakikada istenen bir kimlik için, base64 `data:image/png`, `jpeg`, `webp` veya `gif` olarak, çözüldüğünde en fazla 256 KB, ilk baytları türe uyuyorsa ve başlığı en fazla 1024 x 1024 piksel diyorsa (GIF en fazla 64 kare) kabul edilir. SVG kabul edilmez. Reddedilen resim Hata Listesi'ne tek satır ekler (kod `P151`). Kabul edilen resimler eklentinin klasöründe (`.image-cache`, en fazla 64 MB ve 2000 dosya, en az kullanılan önce silinir; yedeklere, tanılamaya ve imza denetimine girmez) saklanır ve eklenti çalışmıyorken de gösterilir. Henüz önbellekte olmayan resim gelene kadar düğmenin simgesi gösterilmez. Resim yalnızca kullanıldığında istenir: ona başvuran bir düğme ya da yerleşim, ya da `?request=1` ile düzenleyici. Bir listeye bakmak eklentiden hiçbir şey istemez. Kararlı kimlikler kullanın (öğe başına bir tane, resmin sağlama toplamı değil). Önbellekteki resim her zaman önbellekten verilir; 7 günden eskiyse yine hemen verilir ve yeniden istenir (kimlik başına 7 günde bir, eklenti başına saatte en fazla 10, aynı gruplarda); yeni resim eskisinin yerine geçer. `invalidate` önbellekteki resmi hemen yeniden ister; `version`, `put` ile verilenle aynıysa istemez. Sürüm en fazla 64 yazdırılabilir karakterdir. Düzenleyici önbellekteki resmi `GET /api/plugins/{pluginId}/images/{imageId}` adresinden okur; olmayan resim için 404 döner, yalnızca `?request=1` ile eklentiden de ister.

### Aranabilir seçenek listeleri

Seçenek kaynağı olan bir `Select` alanı `searchable` (JSON adı `searchable`, C# `SettingField.Searchable`) olarak işaretlenebilir. Düzenleyici bu durumda bir arama kutusu gösterir ve her seferinde tek sayfa ister. Alan `optionsSource` ve `dependsOn` özelliklerini eskisi gibi korur.

- **C#:** `IOptionsSearch` uygulayın (`IOptionsSource`'u genişletir): `SearchOptionsAsync(sourceId, currentValues, OptionsQuery query, ct)`. `OptionsQuery` şunları taşır: `Text` (kırpılmış, en fazla 100 karakter), `Offset` (0 veya daha büyük), `Limit` (1 ile 50) ve `Selected` (saklanan değer veya null). `Offset`, düzenleyicinin elindeki seçenek sayısıdır. Sayfayı `OptionsResult.Options` içinde döndürün, daha fazla eşleşme varsa `HasMore` ayarlayın; ilk sayfada (offset 0) `Selected` seçeneğini başa koyun (sayfa boyutuna ve offset'e sayılır) ve artık sunulmuyorsa `SelectedMissing` değerini true yapın. Yalnızca `IOptionsSource` uygulayan kaynak da çalışır: sunucu tüm listeyi süzer (büyük/küçük harf, aksanlar ve Türkçe noktalı/noktasız i yok sayılır) ve sayfalar.
- **JavaScript:** `host.options.source(id, (query, values) => ({ options: [{ value, label, group?, icon? }], more?: boolean, selectedMissing?: boolean, error?: string }))`. `query` `{ text, offset, limit, selected }` biçimindedir, `values` `dependsOn` içinde adı geçen form değerleridir. İşlev eklentinin belleğinden hemen yanıt verir (promise yok) ve normal 2 saniyelik çağrı bütçesi içinde kalır. Bir hata, zaman aşımı veya bilinmeyen kimlik düzenleyiciye `error` metni olarak ulaşır ve eklentiyi kapatma sayacına eklenmez. `registerAction` ve `settings.page` alanları `optionsSource`, `searchable` ve `dependsOn` kabul eder.
- **Uç nokta:** `POST /api/actions/{type}/options/{sourceId}` (ve ayar ile ağaç varyantları) değişmeyen gövdenin yanında `?q=&offset=&limit=&selected=` kabul eder. Bu parametreler yoksa çağrı eskisi gibidir ve kaynağın tüm listesini döndürür.
- **Yenile düğmesi:** düzenleyici her aranabilir listenin yanında bir yenile düğmesi gösterir. Soruyu `?refresh=1` ile yeniden sorar: C# `OptionsQuery.Refresh`, JavaScript `query.refresh === true`. Verisini eşzamansız yeniden okuması gereken kaynak, mevcut seçeneklerle ve `OptionsResult.Refreshing = true` ile (JavaScript `refreshing: true`, JSON `refreshing`) hemen yanıt verir. Düzenleyici bir döner gösterir, aynı soruyu 1,5 saniyede bir, en fazla 8 kez yeniden sorar ve son seçenekleri gösterir. Sınırlar (50 seçenek, 256 KB) aynı kalır.
- **Sınırlar:** sayfa başına en fazla 50 seçenek (seçili olan dahil) ve yanıt başına 256 KB; daha büyük yanıt seçenekler yerine hata olur. Seçenek etiketleri ve yanıtlar günlüğe yazılmaz.
- **Resimler:** seçenek `icon` değeri yerleşik bir simge adı veya `plugin-image:<eklentiId>/<resimId>` olmalıdır (kendi eklenti kimliğiniz; resim kimliği 1 ile 128 karakter; harf, rakam, `.`, `_`, `:` ve `-`). Başka her şey, `data:` adresi dahil, atılır.

### Daha büyük web soketi iletileri

Aynı bilgisayardaki bir uygulamayla konuşan JavaScript eklenti, `plugin.json` içinde daha büyük gelen iletiler isteyebilir: `"limits": { "wsMessageKB": 256 }` (64 ile 1024, varsayılan 64). Yalnızca `localhost` veya `127.0.0.1` için `ws:` iznine uygulanır; diğer adresler 64 KB'ta kalır. Kişi bunu onaylar ("Bu bilgisayardaki bir uygulamadan büyük mesajlar alır") ve kapatabilir. 64 KB'tan büyük iletiler ayrıca soket başına saniyede 2 MB'lık bir bütçeyi paylaşır; bütçe bitince fazla iletiler atılır ve Hata Listesi'ne tek satır yazılır. Giden iletiler 64 KB'ta kalır.

## Ağ

| Ne | Nerede |
|---|---|
| Sunucu | TCP port 9820, tüm arayüzler; TLS portu 9821 (`ServerApp.TlsPort`, 9820'nin bir sonrakisi) |
| Cihazlar (WebSocket) | `ws://<bilgisayar adresi>:9820/ws`; şifreli: `wss://<bilgisayar adresi>:9821/ws` |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |
| Düzenleyici | `/editor/`; `/api` uç noktaları yalnızca bilgisayarın kendisinden gelen isteklere yanıt verir |
| QR kod içeriği | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

Protokol, veri modeli ve güvenlik modeli [mimari belgesinde](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md) anlatılır.
