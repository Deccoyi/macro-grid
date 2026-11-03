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

## Eklentiden düğme resimleri

Bir eklenti düğmeler için raster resim verebilir. Profil resmin kendisini tutmaz, yalnızca `plugin-image:<eklentiId>/<resimId>` başvurusunu tutar (resim kimliği 1 ile 128 karakter; harf, rakam, `.`, `_`, `:` ve `-`). Sunucu başvuruyu yerleşimi gönderirken resme çevirir; cihaz başvuruyu hiç görmez.

| | JavaScript | C# |
|---|---|---|
| Resim istenir | `host.images.onRequest(ids => { ... })` | eklenti sınıfı `IPluginImageSource.RequestImages(IReadOnlyList<string> imageIds)` uygular |
| Resim verilir | `host.images.put(imageId, dataUrl)` (kabul edilirse `true`) | `IPluginHost.Images.Put(imageId, dataUrl)` (`bool` döner) |

Sunucu çağrı başına en fazla 20 kimlik ister, 500 ms'de en fazla bir çağrı yapar ve en fazla 200 açık istek tutar; yanıtlanmayan kimlik 10 dakika sonra yeniden istenir. Resim yalnızca son 10 dakikada istenen bir kimlik için, base64 `data:image/png`, `jpeg`, `webp` veya `gif` olarak, çözüldüğünde en fazla 64 KB ve ilk baytları türe uyuyorsa kabul edilir. SVG kabul edilmez. Reddedilen resim Hata Listesi'ne tek satır ekler (kod `P151`). Kabul edilen resimler eklentinin klasöründe (`.image-cache`, en fazla 20 MB ve 2000 dosya, en az kullanılan önce silinir; yedeklere, tanılamaya ve imza denetimine girmez) saklanır ve eklenti çalışmıyorken de gösterilir. Henüz önbellekte olmayan resim gelene kadar düğmenin simgesi gösterilmez. Düzenleyici önbellekteki resmi `GET /api/plugins/{pluginId}/images/{imageId}` adresinden okur.

## Ağ

| Ne | Nerede |
|---|---|
| Sunucu | TCP port 9820, tüm arayüzler; TLS portu 9821 (`ServerApp.TlsPort`, 9820'nin bir sonrakisi) |
| Cihazlar (WebSocket) | `ws://<bilgisayar adresi>:9820/ws`; şifreli: `wss://<bilgisayar adresi>:9821/ws` |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |
| Düzenleyici | `/editor/`; `/api` uç noktaları yalnızca bilgisayarın kendisinden gelen isteklere yanıt verir |
| QR kod içeriği | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

Protokol, veri modeli ve güvenlik modeli [mimari belgesinde](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md) anlatılır.
