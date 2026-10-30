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

Bir değişkenin değeri yokken metin bunun yerine bir yer tutucu gösterebilir: `{name|biçim|yer tutucu}`, örneğin `CPU {system.cpu|0|--} %`. `{name||n/a}` varsayılan biçimi korur. Yer tutucu yoksa değeri olmayan değişken hiçbir şey göstermez.

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

## Kendi değişkenleriniz

**Tercihler → Genel Değişken Listesi** bölümünde kendi değişkenlerinizi tanımlayın: metin, sayı ya da Doğru / Yanlış değeri. Bunları her yerde `{user.name}` olarak kullanın: metinde, kuralda ya da eylemde. **Değişkene değer ata** eylemi bunları değiştirir (bkz. [Eylemler ve makrolar](/tr/guide/actions)).

- Adı ve türü sonradan değiştirilemez. Birini değiştirmek için değişkeni silin ve yenisini ekleyin.
- Yeniden başlattıktan sonra, değişken için **Koru** seçeneğini açmadıysanız başlangıç değerinden yeniden başlar.
- Hâlâ kullanılan bir değişkeni silmeden önce nerelerde kullanıldığı listelenir. Siz düzeltene kadar bu yerler hiçbir şey göstermez.

## Kullanılamayan değişkenler

Değeri olmayan bir değişken **kullanılamaz** durumdadır. Örneğin eklentisi kapalıdır ya da bir sayı değişkeninin başlangıç değeri yoktur. Metin bunun için hiçbir şey göstermez (ya da yer tutucusunu). Bir kural `unavailable` ya da `available` ile bunu sınayabilir. Kullanılamayan bir değişkeni okuyan eylemi olan bir düğme telefonda gri görünür ve küçük bir uyarı işareti taşır. Bkz. [Telefon uygulaması](/tr/guide/phone-app).

## Bu düğme

**Bu düğme** altındaki beş değişken düğmenin kendisini tanımlar. Yalnızca kendi metninde ve kurallarında çalışırlar:

- `self.toggled`: Toggle açıkken açık.
- `self.busy`: Bu düğmenin eylem listesi 150 ms'den uzun süredir çalışıyorsa açık.
- `self.pressed`: Parmağınız basılıyken, en az 150 ms boyunca açık.
- `self.lastResult`: `Success` ya da `Failed`; son çalışmanın nasıl bittiği.
- `self.lastError`: son çalışmadaki ilk hatanın kodu.

Bir eylemde ya da slider'ın değişkeninde değerleri yoktur.

## Slider ve knob'lar

Bir slider'ın **konumu gösteren değişkenini** `system.audio.master` yapın; slider ses düzeyini gösterir ve ses başka yerden değişince hareket eder. Bkz. [ses slider'ı eğitimi](/tr/tutorials/volume-slider).
