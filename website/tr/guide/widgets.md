# Widget'lar

Widget, bir sayfanın ızgarasındaki tek bir öğedir. Eklerken türünü seçersiniz.

| Tür | Ne için kullanılır |
|---|---|
| **Buton** | Basılınca, bırakılınca, uzun basılınca veya çift dokunulunca eylem çalıştırır. |
| **Toggle** | Açık/kapalı anahtarı. Bir basış, Basınca olayını tetiklemek yerine durumu çevirir. |
| **Etiket** | Metin veya canlı değer gösterir, etkileşim gerekmez. |
| **Görsel** | Bir `https://` URL'sinden veya `data:image/...` URL'sinden resim gösterir, isteğe bağlı altyazıyla. |
| **Slider** | Min, maks ve adım değeri olan bir sürükleme kontrolü. |
| **Knob** | Slider ile aynı ayarlara sahip döner kontrol. |
| **Web** | Bir web sayfasını (örneğin bir sohbet) deck'inizde gösterir. Araç Kutusu'nda küçük bir deney şişesi simgesi vardır. |
| **Eklenti** | Bir eklentinin çizdiği widget (örneğin bir gösterge). Eklentinin adı altında listelenir. Bkz. [Eklentiler](/tr/guide/plugins). |

## Olaylar

Her widget türü farklı olaylar tetikleyebilir. [Eylemleri](/tr/guide/actions) **Eylemler** bölümünde bunlara bağlarsınız.

| Widget | Olaylar |
|---|---|
| Buton | Basınca, Bırakınca, Uzun basınca, Çift dokununca |
| Toggle | Açılınca, Kapanınca |
| Slider, Knob | Değer değişti |

::: warning Uyarı: Uzun basma ve çift dokunma, Basma'ya eklenir
Her dokunuş aynı zamanda bir basıştır. Bu yüzden **Uzun basınca** ve **Çift dokununca**, Basınca ve Bırakınca'nın yerine değil, onlara *ek olarak* çalışır. Bunları kullanıyorsanız Basınca eylemini zararsız tutun ya da boş bırakın.
:::

## Metin, simgeler ve yerleşim

- **Metin** canlı değerler içerebilir: `CPU {system.cpu|0}%`. Bkz. [Değişkenler ve metin](/tr/guide/variables).
- Yatay ve dikey hizalamayı, yazı boyutunu ve bir **ikon** ile boyutunu ve konumunu (metnin üstünde, altında, solunda veya sağında) ayarlayın. İkon, metnin rengini alır.
- **İkon seç…** ile ikon ekleyin (bkz. [Stil](/tr/guide/styling)).

## Slider ve knob'lar

**Min**, **Maks** ve **Adım** değerlerini ayarlayın. Bağlı "Değer değişti" eylemi, sürükleme bittiğinde (bırakınca) çalışır, örneğin **Ana ses**.

Kontrolün bilgisayardan bir değeri *göstermesi* için `system.audio.master` gibi **konumunu belirleyen bir değişken** seçin. Konum artık bu değişkeni izler, başka bir cihazdan ya da Windows'un kendisinden değişse bile. Değişken yoksa konum, yalnızca o cihazdaki son sürüklemeyi yansıtır.

## Toggle'lar

Bir toggle'ın sunucunun takip ettiği bir durumu (açık veya kapalı) vardır. Eylemleri **Açılınca** ve **Kapanınca** olaylarına bağlayın, örneğin sesi kapatma ve açma. Görünümünü gerçekle uyumlu tutmak için (örneğin başka yerde kapatılmış bir mikrofon), bir değişkene dayalı [dinamik kural](/tr/guide/dynamic) kullanın.

## Web widget'ları

Bir web widget'ının özelliklerinde **Yüklü tut** anahtarı vardır. Siz ızgaranın başka bir sayfasına geçtiğinizde sayfayı çalışır durumda tutar; böylece bir sohbet her seferinde yeniden yüklenmez. Telefon aynı anda yalnızca birkaç web widget'ı çalıştırabildiğinden, bir sayfada üçten fazla web widget'ı varsa düzenleyici uyarır.

## İsimler

Her widget'ın bir **adı** vardır; bunu **Özellikler**'den değiştirirsiniz. Mesajlar ve seçiciler bu adı kullanır, böylece sorunlu bir widget'ı bulmak kolaylaşır. Ad aynı sayfada benzersiz olmalıdır (harf büyüklüğü dikkate alınmaz).
