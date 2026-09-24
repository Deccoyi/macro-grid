# Ses slider'ı

Windows ana ses düzeyini ayarlayan ve ses başka bir yerden değiştiğinde onu takip eden bir slider oluşturun.

**Gerekenler:** eşleşmiş bir cihaz (bkz. [Cihaz eşleştir](/tr/guide/pairing)).

1. Düzenleyicide **Widget ekle**'ye tıklayın ve **Slider**'ı seçin. Geniş olacak şekilde yeniden boyutlandırın (tam bir satır iyi çalışır).
2. Denetçi panelinde **Min**'i `0`, **Maks**'ı `100` ve **Adım**'ı `1` yapın. `Ses` gibi bir üst yazı verin.
3. **Konumu gösteren değişken** altında `system.audio.master` seçin. Slider artık geçerli ses düzeyini gösterir ve değiştiğinde hareket eder.
4. **Aksiyonlar** altında, **Değer değişti** olayında **+ Aksiyon ekle**'ye tıklayın ve **Ana ses seviyesi**'ni seçin. Slider'ın değerini uygular.
5. **Kaydet**'e tıklayın.

Telefonunuzda slider'ı sürükleyin: bıraktığınızda Windows ses düzeyi onu izler. Klavyenizin ses tuşlarıyla sesi değiştirin, slider hareket eder.

## Sessize alma butonu ekleyin

1. `Sustur` metniyle bir **Toggle** ekleyin.
2. **Açılınca** olayına **Sustur** seçili **Sesi kapat/aç** aksiyonunu ekleyin. **Kapanınca** olayına aynı aksiyonu **Aç** ile ekleyin.
3. Sesi başka bir yerden kapattığınızda toggle'ın doğru görünmesi için arka planına bir [dinamik kural](/tr/guide/dynamic) ekleyin: `system.audio.muted` **eşitse** `true`, kırmızı.

::: tip İpucu
Toggle yerine, **Sesi sessize al/aç** aksiyonuna sahip düz bir **Buton** ve `{system.audio.muted|KAPALI/AÇIK}` gibi dinamik bir metin de kullanabilirsiniz.
:::
