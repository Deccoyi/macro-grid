# Cihaz eşleştirme

Bir cihazın PIN ile yalnızca bir kez eşleştirilmesi gerekir. Sonrasında cihaz bir belirteç saklar ve sormadan yeniden bağlanır.

## Telefon veya tablet eşleştirme

![Kod, QR kod ve tarayıcı adresini (bulanık) gösteren Eşleştirme penceresi](/img/pairing.png)

1. Düzenleyicide **Eşleştirme**'yi açın. Altı haneli bir PIN ve bir QR kod görürsünüz. **Beş dakika** geçerlidirler. Pencere her açıldığında yenisi üretilir, ya da **Yeni kod üret**'e tıklayabilirsiniz.
2. [Telefon uygulamasında](/tr/guide/phone-app) QR kodu okutun ya da sunucu adresini ve PIN'i elle girin.
3. Cihaz **Eşleşmiş cihazlar** altında görünür.

Pencere yerel ağ adresi bulunamadığını söylüyorsa, bilgisayarın Wi-Fi veya Ethernet'e bağlı olduğunu kontrol edin.

## Tarayıcıyı deck olarak kullanın

Ağdaki her cihaz tarayıcısında `http://<bilgisayar adresi>:9820/deck/` adresini açabilir. Yine PIN gerekir. Bilgisayarın adresi sistem tepsisi simgesinin menüsünde ve Eşleştirme penceresinde yazar.

::: tip İpucu
Tarayıcı deck'i test için ya da boştaki bir ekran için kullanışlıdır. Android uygulaması ise kiosk modu, çevrimdışı önbellek ve kaydırma hareketleri sunar.
:::

## Eşleşmiş cihazları yönetin

Aynı pencerede her cihaz için şunları yapabilirsiniz:

- **Cihazda açılacak profili seçin** (varsayılan: ilk profil).
- **Aktif pencereyi takip et** seçeneğini açın veya kapatın. Bkz. [Otomatik geçiş](/tr/guide/auto-switch).
- Eşleşmeyi iptal etmek için **Eşleşmeyi kaldır**'a tıklayın. Cihazın yeni bir PIN ile yeniden eşleşmesi gerekir.

Artık kullanmadığınız ya da güvenmediğiniz cihazları kaldırın: eşleşmiş bir cihaz bilgisayarınızda tuşlara basabilir, metin yazabilir ve program başlatabilir.
