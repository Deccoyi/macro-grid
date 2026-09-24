# Eklentiler

Eklentiler **aksiyonlar**, **değişkenler**, **ayar sayfaları**, **durum çubuğu öğeleri** ve **simge paketleri** ekler. Sunucu çalışırken, yeniden başlatmadan kurulur, yeniden yüklenir ve kaldırılır.

Kendi eklentinizi yazmak, manifest, SDK'lar ve eğitimlerin tümü **[eklenti dokümantasyon sitesinde](https://deccoyi.github.io/macro-grid-plugin/)** yer alır. Kaynak kod ve sürümler: [macro-grid-plugin](https://github.com/Deccoyi/macro-grid-plugin).

## Eklenti kurma

![OBS ve PLC Icons kurulu Eklentiler penceresi](/img/plugins.png)

1. Bir eklenti klasörü edinin: bir sürümün zip'ini açın ya da kendiniz derleyin. Eklenti klasörü `plugin.json` içerir.
2. Düzenleyici'de **Eklentiler → Eklentileri Yönet…** yolunu izleyin ve **Klasörden Yükle…** seçeneğini seçin.
3. Eklenti hemen yüklenir. JavaScript eklentisi önce istediği izinleri gösterir (değişkenleri okuma, aksiyon ekleme, tuşlara basma, bir sunucuya web isteği gönderme) ve yalnızca **İzin ver ve etkinleştir**'e tıkladıktan sonra çalışır.

Bir eklentiyi değiştirdikten sonra **Yeniden yükle**'yi, **Ayarlar** için dişli düğmesini, silmek için de **Kaldır**'ı kullanın. Eklentiler `%AppData%\MacroGrid\plugins\<id>\` altında durur.

::: danger Güven
Bir **C# eklentisi** sunucunun içinde, sunucunun kendisi gibi tam erişimle çalışır. Yalnızca kaynağına güvendiğiniz C# eklentilerini kurun. **JavaScript eklentileri** korumalı alanda çalışır ve yalnızca sizin onayladığınız şeyleri yapabilir.
:::

## Resmi eklentiler

### OBS {#obs}

OBS Studio'yu yerleşik WebSocket'i üzerinden kontrol eder (OBS 28 ya da daha yeni). **22 aksiyon** (sahneler, stüdyo modu, yayın, kayıt, sanal kamera, tekrar arabelleği, ses kapatma ve düzeyi, sahne öğesi görünürlüğü, metin kaynakları) ve yaklaşık **45 canlı `obs.*` değişkeni** (yayın süresi, düşen kareler, kayıt durumu, FPS, CPU ve daha fazlası) ekler.

1. OBS'de: **Tools → WebSocket Server Settings** yolundan sunucuyu etkinleştirin (isterseniz parola belirleyin).
2. Macro Grid'de: **Eklentiler**'i açın, OBS'nin yanındaki dişliye tıklayın, **Enabled** seçeneğini açın ve sunucuyu, bağlantı noktasını (varsayılan 4455) ve parolayı girin. Birkaç saniye içinde bağlanır.
3. Düzenleyici'nin durum çubuğu bağlantı durumunu gösterir.

![OBS eklentisi ayar formu](/img/obs-settings.png)

Deneyin: [OBS ile bir yayın deck'i](/tr/tutorials/obs-deck). Ayrıntılar: [OBS kılavuzu](https://deccoyi.github.io/macro-grid-plugin/guides/obs-plugin).

### PLC Icons {#plc-icons}

Bobinler, zamanlayıcılar ve karşılaştırma blokları gibi 29 merdiven mantığı (PLC) simgesi içeren bir simge paketi. Simge seçicide kendi kategorisi olarak görünür. Ayarı yoktur.

### Hello JS

Küçük bir JavaScript örneği: bir sayaç değişkeni, bir ayar sayfası ve bir aksiyon. [Eklenti eğitimlerinin](https://deccoyi.github.io/macro-grid-plugin/tutorials/js-hello-world) başlangıç noktasıdır.

## İçe aktarılan profilde eksik eklenti

Sahip olmadığınız bir eklentiyi kullanan bir profili içe aktardığınızda hangisi olduğu size bildirilir. Eklentiyi kurun; düğmeler çalışmaya başlar.
