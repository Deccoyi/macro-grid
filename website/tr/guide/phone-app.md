# Telefon uygulaması

Android uygulaması deck'i tam ekran çizer ve dokunuşlarınızı sunucuya gönderir. Kaynak kod ve sürümler: [macro-grid-client](https://github.com/Deccoyi/macro-grid-client).

- Android 7.0 (API 24) veya üstü. Sunucuyla aynı ağda olmalı.
- Uygulama, çalışan bir Macro Grid sunucusu olmadan hiçbir işe yaramaz.
- Uygulamanın ekranları şu an yalnızca Türkçedir.

## Kurulum

1. Önce [sunucuyu](/tr/guide/install) kurup başlatın.
2. APK'yı [telefon uygulaması indirme sayfasından](https://deccoyi.github.io/macro-grid-client/download) indirip telefonda açın. Android sorarsa tarayıcınızdan veya dosya yöneticinizden kuruluma izin verin.

## Bağlanma

- Düzenleyicinin Eşleştirme penceresindeki QR kodu okutun, **ya da**
- sunucu adresini ve PIN'i girin. Uygulama kullandığınız sunucuları hatırlar. Sunucuyu çekmeceden değiştirebilir, silebilir veya yenisini ekleyebilirsiniz.

## Deck'i kullanma

![Telefonda bir deck](/img/deck-phone.png)

| Yaptığınız | Olan |
|---|---|
| Butona dokunmak | Basma ve bırakma sunucuya gönderilir. |
| Basılı tutmak | Uzun basma. |
| İki kez dokunmak | Çift dokunma. |
| Slider veya knob'u sürüklemek | Değer gönderilir. Bir değişkene bağlıysa başka yerde yapılan değişiklikleri de takip eder. |
| Sola veya sağa kaydırmak | Sayfa değişir. |
| Kenardan çekmek (veya tutamacı kullanmak) | **Profil çekmecesi** açılır. |

Çekmece profilleri, kayıtlı sunucuları ve **kilit** anahtarını listeler. Kilit açıkken [otomatik profil geçişi](/tr/guide/auto-switch) durur. Profili elle seçmek ise her zaman çalışır.

## Ayarlar

- **Kiosk modu** durum ve gezinme çubuklarını gizler (varsayılan olarak açık).
- **Ekran yönü** kilidi.
- **Ekranı açık tut**: bir profil gösterilirken ekran kapanmaz.

## Bağlantı koptuğunda

Uygulama giderek uzayan aralıklarla yeniden bağlanmayı dener, son düzeni "çevrimdışı, önbellek" rozetiyle gösterir ve simgeleri önbelleğe alır, böylece yeniden bağlanma hızlı olur. Bir aksiyon bilgisayarda başarısız olursa kısa, kırmızı bir hata bildirimi görürsünüz.
