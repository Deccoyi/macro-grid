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
| **Bekle** (Mantık) | En fazla 60 saniye bekler. Eylemlerin arasında kullanın. |
| **Ana ses**, **Sesi kapat/aç**, **Sessize al (geçiş)** | Windows ses düzeyi ve sessize alma. |
| **Değişkene değer ata**, **Sayıyı artır / azalt**, **Değişkeni ters çevir**, **Başlangıç değerine döndür** (Değişken) | Genel değişkenlerinizden birini değiştirir. Eski tek eylem **Değişkene değer ata (eski)** olarak hâlâ çalışır. |
| **If**, **Else**, **End**, **Stop** (Mantık) | Hangi adımların çalışacağını belirler. Bkz. aşağıdaki Mantık bölümü. |

Eklentiler daha fazlasını ekler. [OBS eklentisi](/tr/guide/plugins#obs) 22 eylem ekler.

Seçicideki eylem adları düzenleyicinin diline uyar. Tam liste: [Eylem başvurusu](/tr/reference/actions).

## Mantık: If, Else, End ve Stop

Mantık adımları seçicinin **Mantık** grubundadır. Bir eylem listesinin neyi çalıştıracağına karar vermesini sağlarlar:

- **If** bir blok başlatır. Blokun adımları yalnızca testi sağlandığında çalışır. Test, değişkenler üzerinde bir **koşul** ([dinamik kurallarla](/tr/guide/dynamic) aynı düzenleyici) ya da **önceki adım başarısız oldu** / **önceki adım başarılı oldu** olabilir.
- **Else** (isteğe bağlı) If sağlanmadığında çalışacak adımları tutar. Her If'in en fazla bir Else'i olabilir. Bir tane eklemek için **Else ekle**'yi kullanın.
- **End** bloğu kapatır. If eklediğinizde End da onunla birlikte eklenir.
- **Stop** tüm listeyi o noktada bitirir. Bu bir hata değildir.

Bloklar üç seviyeye kadar iç içe geçebilir. Bir If'i daraltarak tek satırda görebilirsiniz. Bir If'i taşıdığınızda tüm bloğu taşır; If'i kaldırdığınızda içindeki adımlar kalır. Koşul If'e gelindiğinde okunur, bu yüzden önceki bir adım cevabı değiştirebilir. Atlanan adımlar "önceki adım" olarak sayılmaz. Koşul eksik ya da okunamıyorsa sağlanmamış sayılır.

**Bekle** aynı gruptadır ve saniye cinsinden ayarlanır. Başladıktan sonra iptal edilemez.

## Makrolar

Aynı olaya birkaç eylem bağlayın. Bunlar **sırayla**, biri bittikten sonra diğeri çalışır. Başarısız olan bir eylem kaydedilir ve bildirilir, ama sonrakilerin çalışmasını durdurmaz.

Örnek, "editörümü aç ve kayda başla":

1. **Uygulama aç** → editörünüz
2. **Bekle** → 2 saniye
3. bir OBS **Kaydı başlat** eylemi

Sırayı değiştirmek için okları, silmek için **Kaldır**'ı kullanın.

## Eylemlerin içinde değişkenler

Değişkene izin veren eklenti eylemi alanları (örneğin OBS metin kaynağı eylemi), eylem çalışmadan önce `{variable}` ifadesini çözer. Örnek: `Now streaming for {obs.stream.duration}`.

## Bir eylem başarısız olduğunda

Bir şey ters giderse (artık var olmayan bir sahne, başlatılamayan bir program), telefon kırmızı bir bildirim gösterir. Sorun ayrıca düzenleyicideki **Tanılama Mesajları** penceresinde listelenir: düğme başına bir satır, yanında sayısıyla. Düğme bir sonraki çalışmasında hatasız olursa satır kaybolur. Eylemler asla sessizce başarısız olmaz.

## Sıra her cihaz için önemlidir

Bir cihazdan gelen eylemler sırayla ve alma döngüsünün dışında çalışır. Bu yüzden uzun bir gecikme gibi yavaş bir eylem, sonraki dokunuşun okunmasını asla engellemez.
