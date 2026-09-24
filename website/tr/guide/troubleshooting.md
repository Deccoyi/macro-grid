# Sorun giderme

## Telefon bağlanamıyor

- Bilgisayar ve telefon **aynı ağda** olmalıdır. Misafir Wi-Fi ve "istemci yalıtımı" çoğu zaman cihazlar arası trafiği engeller.
- Sistem tepsisi menüsündeki ya da Eşleştirme penceresindeki adresi kontrol edin ve `9820` bağlantı noktasıyla birlikte tam olarak girin.
- Güvenlik duvarı, ağ profilinizde TCP **9820**'ye izin vermelidir. Yükleyici özel ve etki alanı ağları için bir kural ekler. Windows ağınızı **Genel** olarak sınıflandırdıysa Özel olarak değiştirin.
- Sunucu çalışıyor mu? Sistem tepsisi simgesine bakın.
- Uygulama sorununu ağ sorunundan ayırmak için telefonun tarayıcısında `http://<PC adresi>:9820/deck/` adresini deneyin.

## PIN reddediliyor

PIN beş dakika sonra geçerliliğini yitirir. **Eşleştirme**'yi yeniden açın ya da **Yeni kod üret**'e tıklayın.

## Eşleştirme penceresinde QR kod görünmüyor

Yerel ağ adresi bulunamadı. Bilgisayarı Wi-Fi'ye ya da Ethernet'e bağlayın.

## Bir düğme hiçbir şey yapmıyor

Düzenleyici'nin durum çubuğuna ve telefondaki hata mesajına bakın. Sık nedenler: eski kalmış bir bağlama (silinmiş bir OBS sahnesi), kapalı ya da bağlı olmayan bir eklenti veya artık var olmayan bir program yolu. **Uzun basınca** ve **Çift dokununca** olaylarının, Basınca'ya ek olarak tetiklendiğini unutmayın.

## Değişikliklerim telefonda görünmüyor

**Kaydet**'e tıklayın. O zamana kadar hiçbir şey gönderilmez.

## Canlı bir değer boş

Değişken yok (yazım hatası ya da eklentisi kurulu veya bağlı değil). Listeden seçmek için **+ Değişken ekle**'yi, bir eklenti kurduktan sonra da **Değişkenleri yenile**'yi kullanın.

## Dinamik bir kural hiç eşleşmiyor

Mantıksal bir değişken için `true` ya da `false` yazın; `1`, `0` ya da *Açık* yazmayın. Metin karşılaştırmaları büyük/küçük harfi yok sayar ama tam olarak eşleşmelidir.

## OBS "wrong password" ya da "OBS is not running" gösteriyor

OBS'de WebSocket sunucusunu etkinleştirin (**Tools → WebSocket Server Settings**) ve OBS eklentisi ayarlarına aynı parolayı girin. Yanlış parola, ayarları değiştirene kadar yeniden denemeleri durdurur.

## Windows SmartScreen yükleyici için uyarı veriyor

Yükleyici henüz kod imzalı değil. **Ek bilgi**'yi, ardından **Yine de çalıştır**'ı seçin.

## Günlükler

Günlükler `%AppData%\MacroGrid\logs\` klasöründedir. [Bir sorun bildirirken](https://github.com/Deccoyi/macro-grid/issues) bunları ekleyin.
