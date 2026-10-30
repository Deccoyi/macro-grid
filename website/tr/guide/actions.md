# Eylemler ve makrolar

Eylem, bir widget olayı tetiklendiğinde bilgisayarın yaptığı iştir. Özellikler panelinin **Eylemler** bölümünde **+ Eylem ekle** ile eklersiniz. Bu, aranabilir ve kategorilere ayrılmış bir seçici açar.

## Yerleşik eylemler

![Kategorilere ayrılmış, aranabilir eylem seçicisi](/img/editor-action-picker.png)

| Eylem | Ne yapar |
|---|---|
| **Kısayol** | `ctrl+shift+s` gibi bir tuş kombinasyonuna basar. Alana tıklayıp tuşlara basın. |
| **Metin yaz** | Bir metin yazar. |
| **Uygulama aç** | Bir program başlatır veya bir dosya açar. İsteğe bağlı argümanlar alır. |
| **URL aç** | Bir `http://` veya `https://` adresini varsayılan tarayıcınızda açar. |
| **Sayfa değiştir** | Bir sayfaya, sonrakine, öncekine (başa sarar) veya geri gider. |
| **Profil değiştir** | Bu cihazı başka bir profile geçirir. |
| **Gecikme** | En fazla 60000 ms bekler. Eylemlerin arasında kullanın. |
| **Ana ses**, **Sesi kapat/aç**, **Sessize al (geçiş)** | Windows ses düzeyi ve sessize alma. |

Eklentiler daha fazlasını ekler. [OBS eklentisi](/tr/guide/plugins#obs) 22 eylem ekler.

Seçicideki eylem adları düzenleyicinin diline uyar. Tam liste: [Eylem başvurusu](/tr/reference/actions).

## Makrolar

Aynı olaya birkaç eylem bağlayın. Bunlar **sırayla**, biri bittikten sonra diğeri çalışır. Başarısız olan bir eylem kaydedilir ve bildirilir, ama sonrakilerin çalışmasını durdurmaz.

Örnek, "editörümü aç ve kayda başla":

1. **Uygulama aç** → editörünüz
2. **Gecikme** → 1500 ms
3. bir OBS **Kaydı başlat** eylemi

Sırayı değiştirmek için okları, silmek için **Kaldır**'ı kullanın.

## Eylemlerin içinde değişkenler

Değişkene izin veren eklenti eylemi alanları (örneğin OBS metin kaynağı eylemi), eylem çalışmadan önce `{variable}` ifadesini çözer. Örnek: `Now streaming for {obs.stream.duration}`.

## Bir eylem başarısız olduğunda

Bir şey ters giderse (artık var olmayan bir sahne, başlatılamayan bir program), telefon kırmızı bir bildirim gösterir ve düzenleyicinin durum çubuğu hatayı yazar. Eylemler asla sessizce başarısız olmaz.

## Sıra her cihaz için önemlidir

Bir cihazdan gelen eylemler sırayla ve alma döngüsünün dışında çalışır. Bu yüzden uzun bir gecikme gibi yavaş bir eylem, sonraki dokunuşun okunmasını asla engellemez.
