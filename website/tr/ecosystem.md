# Macro Grid ekosistemi

Macro Grid, birlikte çalışan üç parçadır. İlki şart, diğerleri isteğe bağlıdır.

## Sunucu ve düzenleyici (Windows)

Sistemin kalbi. Windows bilgisayarınızda bildirim alanında çalışır, deck'lerinizi saklar, bir butona bastığınızda aksiyonları çalıştırır ve her şeyi tasarladığınız **düzenleyiciyi** barındırır. Diğer tüm parçalar onunla konuşur.

[Kurun](/tr/guide/install) · [Düzenleyici](/tr/guide/editor)

## Telefon ve tablet uygulaması (Android)

Deck'inizi tam ekran gösterir ve dokunuşlarınızı bilgisayara iletir. QR kodla eşleşir, kendiliğinden yeniden bağlanır, bağlantı koptuğunda son düzeni hatırlar ve kiosk modu vardır. Android cihazınız yoksa herhangi bir cihazdaki tarayıcı deck'i gösterebilir.

[Telefon uygulaması](/tr/guide/phone-app) · [Cihaz eşleştir](/tr/guide/pairing)

## Eklentiler

Eklentiler, Macro Grid'e yeniden başlatmadan yeni beceriler kazandırır:

- **OBS**: sahne değiştirin, yayına geçin, kayıt alın, kaynakları susturun; yayın süresini ve istatistikleri deck'inizde görün.
- **PLC Icons**: simge seçici için merdiven mantığı (ladder) sembollerinden oluşan bir paket.
- **Kendi eklentileriniz**: küçük, korumalı (sandbox) JavaScript eklentileri ya da tam C# eklentileri. Bkz. [Geliştiriciler için](/tr/developers/).

[Eklentileri kullanma](/tr/guide/plugins)

## Nasıl bağlanırlar

```
Telefon veya tarayıcı  <-- yerel ağınız -->  Bilgisayarınızdaki Macro Grid  <-->  Eklentiler (OBS, ...)
```

Her şey kendi ağınızda kalır: bulut yok, hesap yok. Bkz. [Güvenlik](/tr/guide/security).
