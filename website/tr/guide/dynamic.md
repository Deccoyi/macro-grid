# Dinamik kurallar

Kural, bir widget özelliğini bir değişkene bağlar: "CPU 80'in üzerindeyse arka planı kırmızı yap ve yanıp söndür". Kural oluşturmak için bir özelliğin yanındaki **şimşek düğmesine** tıklayın.

## Dinamik olabilen özellikler

Arka plan, yazı rengi, kenarlık rengi, animasyon, simge ve metin. Dinamik bir metin ya da simge sonucunun kendisi de `{değişkenler}` içerebilir.

## Kural oluşturma

![İki durumlu arka plan rengi için Mantık kur penceresi](/img/editor-dynamic.png)

**Mantık kur** penceresi şöyle çalışır:

1. **Eğer**: bir değişken, bir operatör ve bir değer seçin.
2. Koşulları **VE**, **VEYA** ya da **XOR** ile birleştirin; bir koşulu **değilse** ile tersine çevirin.
3. **İse**: uygulanacak rengi, animasyonu, simgeyi ya da metni seçin.
4. **Yoksa eğer**: yeni durumlar ekleyin. Eşleşen ilk durum geçerli olur.
5. **Yoksa**: varsayılan bir değer verin ya da değişmesin diye boş bırakın; böylece widget'ın kendi sabit değeri görünür.
6. **Uygula**.

Operatörler: büyük, büyük veya eşit, küçük, küçük veya eşit, eşit, eşit değil, **arasında**.

Sabit değere dönmek için **Dinamizasyonu kaldır** düğmesini kullanın.

## Yazılacak değerler

Sayıları olduğu gibi (`80`), metinleri tırnak işareti olmadan (`live`) yazın. `system.audio.muted` gibi mantıksal değerler için `true` ya da `false` yazın (büyük veya küçük harf fark etmez). `1` ve `0` mantıksal değerle henüz **eşleşmez**.

## Örnek: yoğun CPU uyarısı

| Özellik | Kural |
|---|---|
| Arka plan | `system.cpu` **büyük** `80` ise kırmızı; yoksa eğer `50`'den büyükse kehribar; yoksa koyu gri |
| Animasyon | `system.cpu` `80`'den büyükse **Yanıp sönme** |

Adım adım anlatım: [Canlı bir CPU karosu](/tr/tutorials/cpu-tile).

::: info Tasarım gereği güvenli
Kurallar düz veridir: bir değişkeni okuyabilir ve karşılaştırabilirler. Betik ve kod çalıştırma yoktur.
:::
