# Güvenlik

Macro Grid, **güvendiğiniz bir ev ya da ofis ağı** için tasarlanmıştır. İnternete açık kullanım için sağlamlaştırılmamıştır.

::: warning Alfa, tamamen yapay zekâ tarafından yazıldı, garanti yok
Tüm kod, tasarım ve dokümantasyon bir yapay zekâ asistanı tarafından oluşturulmuştur; bir insan tarafından satır satır incelenmemiş ve güvenlik denetiminden geçirilmemiştir. Yazılım "olduğu gibi" sunulur; hiçbir garanti ya da sorumluluk yoktur. Yazılımı tamamen kendi sorumluluğunuzda kullanırsınız: hangi cihazları eşleştireceğiniz, hangi eklentileri kuracağınız ve hangi düğmelere basacağınız size aittir. Yükleyici ve uygulama, [kullanıcı sözleşmesini](https://github.com/Deccoyi/macro-grid/blob/main/installer/license-agreement.txt) kabul etmenizi ister.
:::

- **Hiçbir şey şifrelenmez.** Trafik, ağınızda düz `ws://` ve `http://` olarak akar.
- **9820 numaralı bağlantı noktasını asla** internete yönlendirmeyin. Güvenlik duvarında yalnızca özel ağlara izin verin (yükleyici bunu yapar).
- **Eşleştirme, beş dakika geçerli bir PIN kullanır**, ardından cihaz başına bir belirtece (token) geçilir. Belirteçler `%AppData%\MacroGrid\devices.json` dosyasında düz metin olarak saklanır.
- **Eşleşmiş bir cihaz, bilgisayarınızda tuşlara basabilir, metin yazabilir ve programlar başlatabilir.** Yalnızca güvendiğiniz cihazları eşleştirin ve eskilerini kaldırın.
- **Düzenleyici API'si yalnızca bilgisayarın kendisi içindir.** Sunucu, başka makinelerden gelen Düzenleyici API isteklerine 403 döndürür; böylece ağınızdaki kimse profillerinizi değiştiremez ya da PIN'inizi okuyamaz.
- **C# eklentileri tam güvenle çalışır**; JavaScript eklentileri korumalı alanda çalışır ve onaylanmış izinlere ihtiyaç duyar.
- **Her şey yerelde kalır.** Bulut hizmeti ve hesap yoktur.

Bir güvenlik açığını bildirmek için [SECURITY.md](https://github.com/Deccoyi/macro-grid/blob/main/SECURITY.md) dosyasına bakın.
