# OBS ile yayın deck'i

Sahne değiştirin, yayına geçin, kayıt alın ve yayın süresini görün; hepsi telefonunuzdan. Bunun için resmî [OBS eklentisi](/tr/guide/plugins#obs) kullanılır.

**Gerekenler:** OBS Studio 28 veya üzeri, OBS eklentisi ve eşleşmiş bir cihaz.

## 1. OBS WebSocket'i etkinleştirin

OBS'te **Araçlar → WebSocket Sunucu Ayarları**'nı açın, **WebSocket sunucusunu etkinleştir**'i işaretleyin, isterseniz bir parola belirleyin. Portu not alın (varsayılan `4455`).

## 2. Eklentiyi kurun ve bağlayın

1. OBS eklentisini derleyin veya indirin ([yönergeler](https://deccoyi.github.io/macro-grid-plugin/guides/obs-plugin)).
2. Düzenleyicide: **Eklentiler → Eklentileri Yönet… → Klasörden Yükle…** yolunu izleyin ve eklenti klasörünü (içinde `plugin.json` olanı) seçin.
3. OBS'in yanındaki dişli simgesine tıklayın: **Etkin**'i açın, sunucuyu (OBS bu bilgisayardaysa `127.0.0.1`), portu ve parolayı girin, **Kaydet**.
4. Durum çubuğu bağlantıyı gösterir. **Değişkenleri yenile**'ye tıklayın.

## 3. Sahne butonları

Her sahne için bir **Buton** ekleyin. Metin: sahnenin adı. **Basınca** olayına sahne değiştiren OBS aksiyonunu ekleyin ve sahneyi listeden seçin (liste OBS'in kendisinden doldurulur).

Etkin sahneyi öne çıkarın: dinamik bir **Arka plan** kuralı ekleyin, **Eğer** `obs.scene.current` **eşitse** `Game` **İse** sarı.

## 4. Yayına geçin

OBS yayın aç/kapat aksiyonuna sahip bir buton ekleyin ve şu metni verin:

```text
{obs.streaming|LIVE/OFFLINE} {obs.stream.duration}
```

Dinamik kurallar ekleyin: **Eğer** `obs.streaming` **eşitse** `true` **İse** arka plan kırmızı ve animasyon **Yanıp sönme**. Aynısını kayıt için `obs.recording` ve `{obs.record.duration}` ile yapın.

## 5. Ses

- Mikrofonunuz için OBS sessize alma aksiyonlarına sahip bir **Toggle**.
- **Min** `0`, **Maks** `100` olan ve **Değer değişti** olayında OBS ses düzeyi aksiyonuna sahip bir **Slider**. Sürüklenen canlı değeri kullanır.

## 6. Kullanışlı ekstralar

- Etiket olarak `Dropped {obs.stream.frames.droppedPercent|0.0}%` ve `FPS {obs.stats.fps|0}`.
- Bir sahne öğesini gösteren veya gizleyen ya da bir metin kaynağını ayarlayan bir buton (metin `{değişkenler}` içerebilir).
- Tekrar tamponu kullanıyorsanız bir **Tekrarı kaydet** butonu.

**Kaydet**'e basıp deneyin. OBS kapalıysa eklenti bekler ve OBS açılır açılmaz bağlanır.
