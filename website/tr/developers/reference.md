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

## Ağ

| Ne | Nerede |
|---|---|
| Sunucu | TCP port 9820, tüm arayüzler; TLS portu 9821 (`ServerApp.TlsPort`, 9820'nin bir sonrakisi) |
| Cihazlar (WebSocket) | `ws://<bilgisayar adresi>:9820/ws`; şifreli: `wss://<bilgisayar adresi>:9821/ws` |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |
| Düzenleyici | `/editor/`; `/api` uç noktaları yalnızca bilgisayarın kendisinden gelen isteklere yanıt verir |
| QR kod içeriği | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

Protokol, veri modeli ve güvenlik modeli [mimari belgesinde](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md) anlatılır.
