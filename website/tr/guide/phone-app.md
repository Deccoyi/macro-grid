# Telefon uygulaması

Android uygulaması deck'i tam ekran çizer ve dokunuşlarınızı sunucuya gönderir. Kaynak kod ve sürümler: [macro-grid-client](https://github.com/Deccoyi/macro-grid-client).

- Android 7.0 (API 24) veya üstü. Sunucuyla aynı ağda olmalı.
- Uygulama, çalışan bir Macro Grid sunucusu olmadan hiçbir işe yaramaz.
- Uygulamanın ekranları Türkçe veya İngilizcedir. Ayarlar'dan seçmediğiniz sürece telefonun dilini izler.

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
| İki parmakla sola veya sağa kaydırmak | Sayfa değişir. Deck üzerinde her yerde çalışır, slider ve knob üstünde de. Tek parmak asla sayfa değiştirmez. |
| Kenardan çekmek (veya tutamacı kullanmak) | **Profil çekmecesi** açılır. |

Çekmece profilleri, kayıtlı sunucuları ve **kilit** anahtarını listeler. Kilit açıkken [otomatik profil geçişi](/tr/guide/auto-switch) durur. Profili elle seçmek ise her zaman çalışır.

## Ayarlar

- **Kiosk modu** durum ve gezinme çubuklarını gizler (varsayılan olarak açık).
- **Ekran yönü** kilidi.
- **Ekranı açık tut**: bir profil gösterilirken ekran kapanmaz.
- **Dil**: otomatik (telefonun dili), Türkçe veya İngilizce.
- **Güncellemeler**: sürüm, uygulamanın güncellemeleri kendiliğinden denetleyip denetlemeyeceği, ön sürümlerin sayılıp sayılmayacağı ve güncellemenin mobil veriyle inip inemeyeceği (varsayılan yalnızca Wi-Fi).

## Güncellemeler

Açılışta ve yaklaşık altı saatte bir uygulama github.com'a yeni bir sürüm olup olmadığını sorar. Varsa çekmece tutamağında bir nokta belirir ve çekmecede "Yeni sürüm var" satırı görünür;
uygulama açıldıktan sonra ilk seferde güncelleme ekranı kendiliğinden de açılır. Aradaki her sürümde neyin değiştiğini listeler; **Şimdi güncelle**, **Sonra** ve **Bu sürümü atla** düğmeleri vardır.

**Şimdi güncelle** dosyayı indirir (mobil veriye izin vermediyseniz Wi-Fi ile; mobil veriyi kullanmadan önce sorar), doğrular ve Android'e verir. İlk seferde Android'in uygulama kurma iznine
ihtiyaç vardır: uygulama önce bunu açıklar, sonra **Bu kaynaktan izin ver**'i açacağınız Android sayfasını açar. Ardından Android kendi onay penceresini gösterir ve bir güvenlik uyarısı da
gösterebilir; yine de kurmayı seçin. Kurulurken Macro Grid kapanır, sonra yeniden açın. Eşleşmeniz korunur. Denetimi Ayarlar'dan kapatabilirsiniz; oradaki **Güncellemeleri denetle** hemen denetler.

Bir güncelleme yalnızca kurulu uygulamayla aynı anahtarla imzalıysa kurulur. 0.1.1 sürümünüz varsa (farklı anahtarlı bir test derlemesi olarak yayımlandı) bir kez kaldırıp yeni sürümü elle kurun.

## Bağlantı koptuğunda

Uygulama giderek uzayan aralıklarla yeniden bağlanmayı dener, son düzeni "çevrimdışı, önbellek" rozetiyle gösterir ve simgeleri önbelleğe alır, böylece yeniden bağlanma hızlı olur. Bir aksiyon bilgisayarda başarısız olursa kısa, kırmızı bir hata bildirimi görürsünüz.
