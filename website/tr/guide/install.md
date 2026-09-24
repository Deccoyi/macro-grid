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

## Sistem tepsisi simgesi

Sunucu adresini görmek için simgenin üzerine gelin. Menüde sürüm, telefonların kullanabileceği adresler ve bağlı cihaz sayısı yer alır. Buradan düzenleyiciyi ya da veri klasörünü açabilir, uygulamadan çıkabilirsiniz. Simgeye çift tıklamak düzenleyiciyi açar.

[Tercihler](/tr/guide/preferences) bölümünde, Windows'ta oturum açtığınızda Macro Grid'in başlamasını sağlayabilirsiniz. Yalnızca sistem tepsisinde ya da düzenleyici penceresi açık olarak başlatabilirsiniz. Sistem tepsisinde başlatırsanız, hiçbir şey açmadan telefonunuz bağlanabilir.

## Verileriniz

Her şey `%AppData%\MacroGrid\` klasöründe saklanır. Bkz. [Dosyalar ve portlar](/tr/reference/files).
