# Teknik başvuru

## Aksiyon türleri

Yerleşik türler `core.*`, eklenti aksiyonları `<eklenti kimliği>.*` biçimindedir.

| Tür | Ayarlar |
|---|---|
| `core.hotkey` | `keys`, örneğin `ctrl+shift+s` |
| `core.typeText` | metin |
| `core.open` | `target`, argümanlar |
| `core.openUrl` | `url` (`http://` veya `https://`) |
| `core.delay` | `ms`, en fazla 60000 |
| `core.page` | `mode`: bir sayfaya git, `next`, `prev`, `back` |
| `core.profile` | profil |
| `core.setVolume` | slider veya knob değerini kullanır |
| `core.setMute` | `mute` true veya false |
| `core.toggleMute` | yok |

OBS eklentisi: `obs.setScene`, `obs.setPreviewScene`, `obs.studioTransition`, `obs.toggleStudioMode`, `obs.setTransition`, `obs.setProfile`, `obs.startStream`, `obs.stopStream`, `obs.toggleStream`, `obs.startRecord`, `obs.stopRecord`, `obs.toggleRecord`, `obs.pauseRecord`, `obs.virtualCam`, `obs.replayBuffer`, `obs.saveReplay`, `obs.setMute`, `obs.toggleMute`, `obs.setVolume`, `obs.adjustVolume`, `obs.setItemVisibility`, `obs.setText`.

## Widget türleri ve olaylar

Profil dosyalarındaki türler: `button`, `toggle`, `slider`, `knob`, `label`, `image`, `web`, `plugin-html` (son ikisi yer tutucudur).

| Widget | Olaylar |
|---|---|
| Buton | `press`, `release`, `longPress`, `doubleTap` |
| Toggle | `toggleOn`, `toggleOff` |
| Slider, Knob | `valueChange` |

## Ağ

| Ne | Nerede |
|---|---|
| Sunucu | TCP port 9820, tüm arayüzler |
| Cihazlar (WebSocket) | `ws://<bilgisayar adresi>:9820/ws` |
| Tarayıcı deck'i | `http://<bilgisayar adresi>:9820/deck/` |
| Düzenleyici | `/editor/`; `/api` uç noktaları yalnızca bilgisayarın kendisinden gelen isteklere yanıt verir |
| QR kod içeriği | `macrogrid://pair?host=<ip>&port=9820&pin=<pin>` |

Protokol, veri modeli ve güvenlik modeli [mimari belgesinde](https://github.com/Deccoyi/macro-grid/blob/main/docs/architecture.md) anlatılır.
