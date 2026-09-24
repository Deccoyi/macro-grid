# Değişkenler

Widget metninde `{ad}` veya `{ad|biçim}` olarak, ayrıca [dinamik kurallarda](/tr/guide/dynamic) kullanın. Biçimler için bkz. [Değişkenler ve metin](/tr/guide/variables).

## Sistem

| Değişken | Anlamı |
|---|---|
| `system.time` | Geçerli saat |
| `system.cpu` | CPU yükü (%) |
| `system.ram` | RAM kullanımı (%) |
| `system.ram.used` | Kullanılan RAM (GB) |
| `system.ram.total` | Kurulu RAM (GB) |
| `system.uptime` | Windows başlatıldığından beri geçen süre |
| `system.audio.master` | Ana ses düzeyi (0 ile 100) |
| `system.audio.muted` | Ses kapalıysa `true` |

## OBS eklentisi

Hepsi `obs.` ile başlar.

| Grup | Değişkenler |
|---|---|
| Bağlantı | `obs.connected`, `obs.status`, `obs.ws.in`, `obs.ws.out` |
| Sahneler ve durum | `obs.scene.current`, `obs.scene.preview`, `obs.studioMode`, `obs.transition.current`, `obs.profile.current`, `obs.sceneCollection.current` |
| Yayın | `obs.streaming`, `obs.stream.reconnecting`, `obs.stream.duration`, `obs.stream.timecode`, `obs.stream.congestion`, `obs.stream.bytes`, `obs.stream.kbps`, `obs.stream.frames.dropped`, `obs.stream.frames.total`, `obs.stream.frames.droppedPercent` |
| Kayıt | `obs.recording`, `obs.record.paused`, `obs.record.duration`, `obs.record.timecode`, `obs.record.bytes`, `obs.record.kbps` |
| Çıkışlar | `obs.virtualcam`, `obs.replayBuffer` |
| İstatistikler | `obs.stats.fps`, `obs.stats.cpu`, `obs.stats.memory`, `obs.stats.disk`, `obs.stats.renderTime`, `obs.stats.render.skipped`, `obs.stats.render.total`, `obs.stats.render.skippedPercent`, `obs.stats.output.skipped`, `obs.stats.output.total`, `obs.stats.output.skippedPercent` |
| Giriş ve öğe başına | `obs.input.<slug>.muted`, `obs.input.<slug>.volumeDb`, `obs.item.<scene>.<source>.visible` |

**Slug**, adın küçük harfe çevrilmiş ve `a-z` ile `0-9` dışındaki her karakter dizisinin `_` ile değiştirilmiş hâlidir. Örneğin `Mic/Aux` girişi `mic_aux` olur.

Diğer eklentiler kendi değişkenlerini yayınlar; seçici o an kullanılabilen her şeyi listeler.
