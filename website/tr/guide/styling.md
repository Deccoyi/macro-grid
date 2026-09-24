# Stil, simgeler ve CSS

## Görünüm

**Görünüm** bölümünde **Arka plan**, **Yazı** rengi, **Border** rengi ve kalınlığı, köşe **Radius** değeri ve bir **Animasyon** ayarlayabilirsiniz: yok, **Yanıp sönme**, **Nabız** ya da **Nabız (büyüyüp küçülme)**. Her renk ve animasyon [dinamik](/tr/guide/dynamic) olabilir.

## Simgeler

**İkon seç…** düğmesi, Lucide simge setiyle (ISC lisansı) aranabilir bir seçici açar. Eklentiler kendi paketlerini ekleyebilir; [PLC Icons](/tr/guide/plugins#plc-icons) eklentisi kendi kategorisinde 29 merdiven mantığı simgesi ekler. Seçilen simge widget'ın simge rengini alır; boyutunu ve metne göre konumunu siz belirlersiniz.

Simgeler bir cihaza bir kez gönderilir ve orada önbelleğe alınır.

## Özel CSS

Her widget'ta, panelin kapsamadığı her şey için bir **Özel CSS** kutusu vardır: gradyanlar, gölgeler, animasyonlar, yazı tipleri.

CSS her widget için ayrı bir korumalı alanda çalışır ve temizlenir. Şunlar kaldırılır ve Düzenleyici sizi uyarır:

- Boyutu ya da konumu değiştiren her şey: `width`, `height`, `position`, `inset`, `top`, `right`, `bottom`, `left`, `margin`, `transform`, `zoom`, `display`.
- `@import`.
- `data:` URI dışında bir yere işaret eden `url()`.

Gradyanlar, gölgeler, kenarlıklar ve animasyonlar çalışır.

```css
background: linear-gradient(135deg, #d97706, #b45f04);
box-shadow: 0 4px 14px rgba(0, 0, 0, 0.4);
```

## Düzenleyicinin teması

Düzenleyicinin kendi açık ya da koyu teması widget renklerinizi hiçbir zaman etkilemez. O renkler her zaman sizindir.
