# Değişkenler ve metin

**Değişken**, sunucunun güncel tuttuğu canlı bir değerdir; örneğin işlemci yükü. Widget metni, değişkenleri gösterebilen bir şablondur.

## Değişken kullanma

Bir widget'ın **Metin** alanına `{ad}` yazın ya da aranabilir listeden seçmek için **+ Değişken ekle** düğmesine tıklayın:

```text
CPU {system.cpu|0}%
Time {system.time|HH:mm}
Live: {obs.stream.duration}
```

Yalnızca değişen bir değişkeni kullanan widget'lar yeniden çizilir ve cihaza yalnızca gerçekten değişen metin gönderilir (saniyede en fazla yaklaşık on güncelleme).

## Biçimler

`|` işaretinden sonra bir biçim ekleyin:

| Tür | Varsayılan | Örnek |
|---|---|---|
| Sayı | `0.##` | `{system.cpu\|0}` şunu gösterir: `23` |
| Tarih/saat | `HH:mm` | `{system.time\|HH:mm:ss}` |
| Süre | `hh:mm:ss` | `{system.uptime}` |
| Mantıksal (boolean) | `Açık` / `Kapalı` | `{obs.streaming\|ON/OFF}` |

Düz süslü parantez yazmak için arka arkaya iki açma ya da iki kapama parantezi yazın. Bulunamayan bir değişken boş metin olarak görünür.

::: info Mantıksal değerler
Biçim verilmezse mantıksal bir değer *Açık* ve *Kapalı* sözcükleriyle gösterilir. İfadeyi kendiniz belirlemek için `{system.audio.muted|MUTED/LIVE}` gibi kendi biçiminizi verin.
:::

## Yerleşik değişkenler

| Değişken | Anlamı |
|---|---|
| `system.time` | Geçerli saat |
| `system.cpu` | İşlemci yükü, yüzde |
| `system.ram` | RAM kullanımı, yüzde |
| `system.ram.used`, `system.ram.total` | RAM, GB cinsinden |
| `system.uptime` | Windows'un başlamasından bu yana geçen süre |
| `system.audio.master` | Ana ses düzeyi, 0 ile 100 arası |
| `system.audio.muted` | Ses kapalıysa `true` |

Eklentiler çok daha fazlasını ekler (OBS eklentisi yaklaşık 45 tane ekler). Bkz. [Değişkenler başvurusu](/tr/reference/variables). Bir eklenti kurduktan sonra Düzenleyici'nin üst çubuğundaki **Değişkenleri yenile** düğmesine tıklayın.

## Slider ve knob'lar

Bir slider'ın **konumu gösteren değişkenini** `system.audio.master` yapın; slider ses düzeyini gösterir ve ses başka yerden değişince hareket eder. Bkz. [ses slider'ı eğitimi](/tr/tutorials/volume-slider).
