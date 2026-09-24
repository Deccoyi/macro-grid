# Hızlı başlangıç

Sıfırdan telefonunuzda çalışan bir butona, yaklaşık on dakikada.

::: warning Alfa yazılım
Macro Grid herkese açık alfa sürümündedir ve tamamen yapay zekâ tarafından yazıldı. Özellikler ve dosya biçimleri sürümler arasında değişebilir. Bir cihazı eşleştirmeden önce [Güvenlik](/tr/guide/security) sayfasını okuyun.
:::

## Neye ihtiyacınız var

- Windows 10 veya 11 çalıştıran bir bilgisayar (sunucu yalnızca Windows'ta çalışır).
- **Aynı ağdaki** bir telefon ya da tablet ve [Android uygulaması](/tr/guide/phone-app). Uygulama yerine bir tarayıcı da yeterlidir.

## 1. Sunucuyu kurun

`MacroGrid-Setup-<version>.exe` dosyasını [Sürümler sayfasından](https://github.com/Deccoyi/macro-grid/releases) indirip çalıştırın. Ayrıntılar: [Sunucuyu kurun](/tr/guide/install).

Macro Grid, saatin yanında bir **sistem tepsisi simgesi** olarak başlar. Düzenleyiciyi açmak için simgeye çift tıklayın (veya menüsünü kullanın).

## 2. Telefonunuzu eşleştirin

1. Düzenleyicide **Eşleştirme**'ye tıklayın. Beş dakika geçerli olan altı haneli bir PIN ve bir QR kod görürsünüz.
2. Telefonda uygulamayı açıp QR kodu okutun. Bilgisayarın adresini ve PIN'i elle de girebilirsiniz.
3. Telefonda profiliniz görünür. Bir dahaki sefere kendiliğinden yeniden bağlanır.

Devamı: [Cihaz eşleştirme](/tr/guide/pairing).

## 3. İlk butonunuzu yapın

1. **Widget ekle**'ye tıklayıp **Buton**'u seçin. Buton, ızgaranın boş bir hücresine yerleşir.
2. Butonu seçin ve **Metin** alanını doldurun, örneğin `CPU {system.cpu|0}%`. `{...}` kısmı canlı bir değerdir.
3. **Aksiyonlar** bölümünde **Basınca** olayı için **+ Aksiyon ekle**'ye tıklayıp **Kısayol**'u seçin. Alana tıklayın ve tuş kombinasyonuna basın, örneğin `Ctrl+Shift+S`.
4. **Kaydet**'e tıklayın. Telefon hemen güncellenir. Butona basın, bilgisayar kısayolu alır.

::: tip İpucu: Birden fazla aksiyon, tek makro
Aynı olaya daha fazla aksiyon ekleyin, sırayla çalışırlar. Bir program zamana ihtiyaç duyuyorsa aralarına **Gecikme** koyun.
:::

## Sırada ne var

- Araçları öğrenin: [Düzenleyici](/tr/guide/editor), [Widget'lar](/tr/guide/widgets), [Aksiyonlar](/tr/guide/actions).
- Bir öğretici izleyin: [ses slider'ı](/tr/tutorials/volume-slider) veya [OBS ile yayın deck'i](/tr/tutorials/obs-deck).
- Bir şey çalışmıyor mu? [Sorun giderme](/tr/guide/troubleshooting).
