# Sorun giderme

## Telefon bağlanamıyor

- Bilgisayar ve telefon **aynı ağda** olmalıdır. Misafir Wi-Fi ve "istemci yalıtımı" çoğu zaman cihazlar arası trafiği engeller.
- Sistem tepsisi menüsündeki ya da Eşleştirme penceresindeki adresi kontrol edin ve `9820` bağlantı noktasıyla birlikte tam olarak girin.
- Güvenlik duvarı, ağ profilinizde TCP **9820** ve **9821**'e izin vermelidir. Yükleyici özel ve etki alanı ağları için bir kural ekler. Windows ağınızı **Genel** olarak sınıflandırdıysa Özel olarak değiştirin.
- Sunucu çalışıyor mu? Sistem tepsisi simgesine bakın.
- Uygulama sorununu ağ sorunundan ayırmak için telefonun tarayıcısında `http://<PC adresi>:9820/deck/` adresini deneyin.

## PIN reddediliyor

PIN yalnızca **Eşleştirme** penceresi açıkken, en fazla beş dakika ve tek bir cihaz için geçerlidir. PIN'i girerken pencereyi açık tutun ya da **Yeni kod üret**'e tıklayın. Çok sayıda yanlış PIN'den sonra cihaz yeniden denemeden önce biraz beklemelidir (ilk seferde 30 saniye).

## Eşleştirme penceresinde QR kod görünmüyor

Yerel ağ adresi bulunamadı. Bilgisayarı Wi-Fi'ye ya da Ethernet'e bağlayın.

## Bir düğme hiçbir şey yapmıyor

Telefondaki hata mesajına ve Düzenleyici'deki **Tanılama Mesajları** penceresine bakın. Sık nedenler: eski kalmış bir bağlama (silinmiş bir OBS sahnesi), kapalı ya da bağlı olmayan bir eklenti veya artık var olmayan bir program yolu. **Uzun basınca** ve **Çift dokununca** olaylarının, Basınca'ya ek olarak tetiklendiğini unutmayın.

## Değişikliklerim telefonda görünmüyor

**Kaydet**'e tıklayın. O zamana kadar hiçbir şey gönderilmez.

## Canlı bir değer boş

Değişken yok (yazım hatası ya da eklentisi kurulu veya bağlı değil). Listeden seçmek için **+ Değişken ekle**'yi, bir eklenti kurduktan sonra da **Değişkenleri yenile**'yi kullanın.

## Dinamik bir kural hiç eşleşmiyor

Mantıksal bir değişken için `true` ya da `false` yazın; `1`, `0` ya da *Açık* yazmayın. Metin karşılaştırmaları büyük/küçük harfi yok sayar ama tam olarak eşleşmelidir.

## OBS "wrong password" ya da "OBS is not running" gösteriyor

OBS'de WebSocket sunucusunu etkinleştirin (**Tools → WebSocket Server Settings**) ve OBS eklentisi ayarlarına aynı parolayı girin. Yanlış parola, ayarları değiştirene kadar yeniden denemeleri durdurur.

## Windows SmartScreen yükleyici için uyarı veriyor

Yükleyici henüz kod imzalı değil. **Ek bilgi**'yi, ardından **Yine de çalıştır**'ı seçin.

## Tanılama Mesajları

**Tanılama Mesajları** (alttaki panel, eski adı Hata Listesi) profiliniz ya da eklentileriniz hakkında neyin yanlış olduğunu, her satırda bir kodla listeler. Bir satıra çift tıklayın ya da satırın düğmesine basın; tam mesajı, kodu, kaç kez olduğunu ve yerini görürsünüz. **Widget'a git** sorunlu widget'ı açar. Hiçbir şey yüklenmez: listeyi panelin düğmeleriyle kopyalayın ya da kaydedin ve kendiniz paylaşın.

- Bir sütun kenarına çift tıklayarak sütunu içeriğine göre genişletin. Tablo başlığına sağ tıklayarak hangi sütunların gösterileceğini seçin.
- Yalnızca uyarısı olan bir düğmenin yanında Düzenleyici'de küçük bir sarı üçgen çıkar; hatası olan düğme kırmızı işaretini korur. Tüm satırlarını görmek için üzerine gelin.

| Kod | Anlamı | Ne yapmalı |
|---|---|---|
| `E210` | Bir düğme, eklentisi kullanılamayan bir eylemi çalıştırır. | Eklentiyi kurun ya da açın, ya da başka bir eylem seçin. |
| `E211` | Bir eklenti widget'ı kullanılamıyor. | Eklentiyi kurun ya da açın, ya da widget'ı silin. |
| `E212` | Bir kural sayı değişkenini metinle karşılaştırıyor; bu yüzden hiçbir zaman doğru olmaz. | Kuralda bir sayı yazın. Profil yine de kaydedilebilir. |
| `E220` | Bir eylem artık var olmayan bir sayfaya, profile ya da web widget'ına işaret ediyor. | Var olan bir hedef seçin. |
| `W221` | Bir eylemin ayarı izin verilen bir değer değil. | Geçerli bir değer seçin. |
| `W230` | Bir metin ya da kural, o anda hiçbir eklentinin sağlamadığı bir değişkeni kullanıyor. | Eklentinin açık olduğunu ve adın doğru yazıldığını kontrol edin. |
| `W231` | Bir metin, kural ya da eylem, Genel Değişken Listesi'nde olmayan bir ad kullanıyor. | Tercihler'de yeniden ekleyin ya da adı değiştirin. |
| `W233` | Bir eylem listesinin If, Else ve End adımları eşleşmiyor. | Blokları düzeltin. |
| `W234` | Bir If adımının koşulu yok. | Ona bir değişken seçin. |
| `W235` | Bir widget, o anda değeri olmayan bir değişken gösteriyor ya da sınıyor. | Eklentiyi kontrol edin. Yer tutucu ya da "kullanılamıyorsa" kuralı uyarıyı önler. |
| `P114` | Resmî bir eklentinin dosyaları kurulduktan sonra değişti; bu yüzden çalışmıyor. | Eklentiler penceresinden yeniden kurun. |
| `P122` | Bir eklenti hata verdi ve kapatıldı. | Güncelleyin, sonra Eklentiler penceresinden açın. |
| `P131` | Bir düğmeye basıldı, ama eylemlerinden biri kullanılamıyor. | `E210` ile aynı. |
| `P132` | Bir düğmenin eylemi çalışırken başarısız oldu. | Eylemin ayarlarını kontrol edin. Hatasız bir çalışmadan sonra satır kaybolur. |
| `P160` | Bir otomasyon kuralının bir adımı çalışmadı. | Otomasyon bölümünde o adımı kontrol edin. |
| `P161` | Bir otomasyon kuralı çok sık başladı, bu yüzden bazı başlatmalar atlandı. | Onu neyin tetiklediğini kontrol edin. |
| `P162` | Bir otomasyon kuralı sürekli başladığı için kapatıldı. | Tetikleyicisini düzeltin, sonra yeniden açın. |
| `P163` | Bir otomasyon kuralı var olmayan bir genel değişken kullanıyor. | Kuralda başka bir değişken seçin ya da değişkeni yeniden ekleyin. |

## Bir düğme gri

Şu anda çalışamayan bir düğme, telefonda ve Düzenleyici'de küçük bir uyarı işaretiyle gri görünür. Telefonda basarak neyin eksik olduğunu görün. Bkz. [Telefon uygulaması](/tr/guide/phone-app).

## Çökme raporu

Bir çökmeden sonra Macro Grid'in bir sonraki açılışında rapor metnini içeren bir **çökme raporu** penceresi çıkar. **Dosyaya kaydet** metni bir metin dosyası olarak yazar, **Kopyala** metni kopyalar, **Kapat** pencereyi kapatır. Hiçbir şey gönderilmez; dosyayı kendiniz paylaşın, örneğin bir sorun bildiriminde. Rapor her zaman İngilizcedir. Düzenleyici başlangıçta açılmazsa pencere bekler: sistem tepsisinde bir bildirim raporun hazır olduğunu söyler ve tepsi menüsünü açtığınızda ya da bildirime tıkladığınızda pencere açılır.

## Günlükler

Günlükler `%AppData%\MacroGrid\logs\` klasöründedir. [Bir sorun bildirirken](https://github.com/Deccoyi/macro-grid/issues) bunları ekleyin.

Bütün günlükleri tek bir zip dosyası olarak kaydetmek için **Yardım → Günlükleri Dışa Aktar…** yolunu izleyin. Kişisel bilgiler tanınabildiği yerlerden silinir; yine de paylaşmadan önce dosyalara göz atın.
