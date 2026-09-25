# Sunucuyu kurun

Sunucu, Windows'ta çalışan bir sistem tepsisi uygulamasıdır. Profillerinizi saklar, deck ile **9820 numaralı port** üzerinden WebSocket ile konuşur, aksiyonları çalıştırır ve düzenleyiciyi barındırır.

## Gereksinimler

- Windows 10 veya 11.
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/). Güncel Windows sürümlerinde hazır gelir. Eksikse kurulum programı yükler.

## Kurulum programıyla

1. `MacroGrid-Setup-<version>.exe` dosyasını (veya zip'i) [İndirme sayfasından](/tr/download) indirin.
2. Çalıştırın ve kullanıcı sözleşmesini kabul edin. Varsayılan olarak masaüstüne bir kısayol eklenir.
3. Kurulumu bitirip Macro Grid'i başlatın.

Kurulum programı uygulamayı `Program Files\Macro Grid` klasörüne yerleştirir ve **yalnızca özel ve etki alanı ağlarında TCP 9820**'ye izin veren bir güvenlik duvarı kuralı ekler. Genel ağlara asla izin vermez. Kaldırma işlemi kuralı da siler.

::: info Bilgi: İmzasız kurulum programı
Kurulum programı henüz kod imzalı değil, bu yüzden Windows SmartScreen ilk çalıştırmada uyarı verebilir. İndirdiğiniz dosyaya güveniyorsanız **Ek bilgi**'yi, ardından **Yine de çalıştır**'ı seçin.
:::

## Güncellemeler

Macro Grid yeni sürümü kendiliğinden arar: açıldıktan yaklaşık bir dakika sonra ve sonrasında altı saatte bir. Yalnızca GitHub'daki herkese açık sürüm listesini okur, sizinle veya bilgisayarınızla ilgili hiçbir şey göndermez. Yeni bir sürüm olduğunda bir bildirim ve arada kalan tüm sürümlerin değişikliklerini listeleyen bir güncelleme penceresi görürsünüz. Seçenekler:

- **Şimdi kur:** Kurulum programını indirir, GitHub'ın bildirdiği SHA-256 ile doğrular, Windows'tan yönetici izni ister ve yerinde günceller. Macro Grid kendiliğinden yeniden açılır, profilleriniz ve eşleştirdiğiniz cihazlar korunur.
- **Sonra:** Bir gün sonra yeniden sorar.
- **Bu sürümü atla:** Bu sürüm için sessiz kalır, daha yeni bir sürüm her zamanki gibi duyurulur.

Yeni sürümün **yeni bir kullanıcı sözleşmesi** varsa kurulum programı onu gösterir ve devam etmek için kabul etmeniz gerekir. İptal ederseniz hiçbir şey değişmez ve elinizdeki sürüm çalışmaya devam eder. Aynı bilgisayarı kullanan diğer kişilere sözleşme, Macro Grid'i ilk açtıklarında bir kez sorulur.

Elle de denetleyebilirsiniz: sistem tepsisi menüsünden ya da **Yardım > Güncellemeleri Denetle**. Otomatik denetimi durdurmak için [Tercihler](/tr/guide/preferences) bölümünden kapatın. Zip dosyasından çalıştırılan kopya yerinde güncellenmez, güncelleme penceresi sürüm sayfasını açar.

## Sistem tepsisi simgesi

Sunucu adresini görmek için simgenin üzerine gelin. Menüde sürüm, telefonların kullanabileceği adresler ve bağlı cihaz sayısı yer alır. Buradan düzenleyiciyi ya da veri klasörünü açabilir, uygulamadan çıkabilirsiniz. Simgeye çift tıklamak düzenleyiciyi açar.

[Tercihler](/tr/guide/preferences) bölümünde, Windows'ta oturum açtığınızda Macro Grid'in başlamasını sağlayabilirsiniz. Yalnızca sistem tepsisinde ya da düzenleyici penceresi açık olarak başlatabilirsiniz. Sistem tepsisinde başlatırsanız, hiçbir şey açmadan telefonunuz bağlanabilir.

## Verileriniz

Her şey `%AppData%\MacroGrid\` klasöründe saklanır. Bkz. [Dosyalar ve portlar](/tr/reference/files).
