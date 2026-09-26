# Güvenlik

Macro Grid, **güvendiğiniz bir ev ya da ofis ağı** için tasarlanmıştır. İnternete açık kullanım için sağlamlaştırılmamıştır.

::: warning Alfa, tamamen yapay zekâ tarafından yazıldı, garanti yok
Tüm kod, tasarım ve dokümantasyon bir yapay zekâ asistanı tarafından oluşturulmuştur; bir insan tarafından satır satır incelenmemiş ve güvenlik denetiminden geçirilmemiştir. Yazılım "olduğu gibi" sunulur; hiçbir garanti ya da sorumluluk yoktur. Yazılımı tamamen kendi sorumluluğunuzda kullanırsınız: hangi cihazları eşleştireceğiniz, hangi eklentileri kuracağınız ve hangi düğmelere basacağınız size aittir. Yükleyici ve uygulama, [kullanıcı sözleşmesini](https://github.com/Deccoyi/macro-grid/blob/main/installer/license-agreement.txt) kabul etmenizi ister.
:::

- **Hiçbir şey şifrelenmez.** Trafik, ağınızda düz `ws://` ve `http://` olarak akar.
- **9820 numaralı bağlantı noktasını asla** internete yönlendirmeyin. Güvenlik duvarında yalnızca özel ağlara izin verin (yükleyici bunu yapar).
- **Eşleştirme bir PIN kullanır**, ardından cihaz başına bir belirtece (token) geçilir. PIN yalnızca Eşleştirme penceresi açıkken, en fazla beş dakika ve tek bir cihaz için geçerlidir. Çok sayıda yanlış PIN giren cihaz bir süre bekletilir ve PIN değiştirilir. Belirteçler `%AppData%\MacroGrid\devices.json` dosyasında düz metin olarak saklanır.
- **Eşleşmiş bir cihaz, bilgisayarınızda tuşlara basabilir, metin yazabilir ve programlar başlatabilir.** Yalnızca güvendiğiniz cihazları eşleştirin ve eskilerini kaldırın.
- **Düzenleyici API'si yalnızca bilgisayarın kendisi içindir.** Sunucu, başka makinelerden gelen Düzenleyici API isteklerine 403 döndürür; böylece ağınızdaki kimse profillerinizi değiştiremez ya da PIN'inizi okuyamaz.
- **C# eklentileri tam güvenle çalışır**; JavaScript eklentileri korumalı alanda çalışır ve onaylanmış izinlere ihtiyaç duyar.
- **Başka yazarların eklentileri incelenmez.** Bir C# eklentisi dosyalarınızı okuyabilir, program başlatabilir, internete bağlanıp veri gönderebilir; Macro Grid bunu sınırlayamaz ve kontrol edemez. Bir JavaScript eklentisi yalnızca listelediği adreslere web isteği gönderebilir ve izni, her adresin bu bilgisayarda mı, yerel ağınızda mı, internette mi olduğunu söyler.
- **Güvenlik kaydı.** Eşleştirmeler, yanlış PIN'ler, kaldırılan cihazlar ve eklenti kurulumları `%AppData%\MacroGrid\logs` klasöründeki kayıt dosyalarına, geldikleri adresle birlikte yazılır; PIN ya da belirteç asla yazılmaz. Kayıtlar bilgisayarınızda kalır ve 14 gün sonra silinir (toplamda en fazla 20 MB).
- **Verileriniz yerelde kalır.** Bulut hizmeti ve hesap yoktur.
- **İsteğe bağlı tek bağlantı: güncelleme denetimi.** Sunucu yaklaşık altı saatte bir github.com'a yeni bir sürüm olup olmadığını sorar. Yalnızca program adı ve sürümü (`MacroGrid/<sürüm>`) gönderilir; sizinle ya da bilgisayarınızla ilgili hiçbir şey gönderilmez ve siz "Şimdi kur"a basmadan hiçbir şey kurulmaz. İndirilen yükleyici, GitHub'ın bildirdiği SHA-256 ile karşılaştırılır; kod imzalı değildir, bu yüzden Windows yönetici izni ister. Denetimi Tercihler > Genel bölümünden kapatabilirsiniz.
- **Telefon uygulamasının da aynı türden isteğe bağlı bir bağlantısı vardır.** Açılışta ve yaklaşık altı saatte bir github.com'a yeni bir uygulama sürümü olup olmadığını sorar ve yalnızca `MacroGridClient/<sürüm>` gönderir. Siz dokunmadan hiçbir şey kurmaz, dosya GitHub'ın SHA-256'sıyla karşılaştırılır ve Android dosyayı yalnızca kurulu uygulamayla aynı anahtarla imzalıysa kurar (anahtar bakımcının bilgisayarında kalır, GitHub'a hiç konmaz). Android uygulama kurma izni için bir kez sorar ve her seferinde kendi onay penceresini gösterir. İndirmeler, mobil veriye izin vermediyseniz yalnızca Wi-Fi ile yapılır; denetimi uygulamanın Ayarlar bölümünden kapatabilirsiniz.

Bir güvenlik açığını bildirmek için [SECURITY.md](https://github.com/Deccoyi/macro-grid/blob/main/SECURITY.md) dosyasına bakın.
