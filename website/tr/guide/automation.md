# Otomasyon kuralları

Bir kural, bir eylem listesini düğmeye basmadan kendiliğinden çalıştırır: bir değer bir sınırı geçtiğinde, günün belirli bir saatinde ya da bir cihaz bağlandığında. Kural eklemek, düzenlemek ya da silmek için **Tercihler → Otomasyon** yolunu açın.

Kurallar tuşlara basmaz. Bir kural kısayol gönderemez ya da metin yazamaz, çünkü bunları o an önde olan pencereye basarak yapardı. Böyle bir adım reddedilir ve [Tanılama Mesajları](/tr/guide/troubleshooting) penceresinde gösterilir.

## Tetikleyiciler

| Tetikleyici | Ne zaman başlar |
|---|---|
| **Bir değer doğru olunca** | Bir değişken üzerindeki bir koşul doğru olur; örneğin `system.cpu` değeri `90`dan büyük. Kural, koşul yanlıştan doğruya geçtiğinde başlar. Sunucu açıldığı ya da kural kaydedildiği için, koşul zaten doğruyken başlamaz. |
| **Bir saatte** | Yerel saat, sizin belirlediğiniz saate (`HH:mm`) gelir ve seçtiğiniz günlerden birindedir; varsayılan olarak her gün. Bilgisayar uykudayken ya da Macro Grid kapalıyken geçen bir saat sonradan telafi edilmez. |
| **Bir cihaz bağlandığında** | Daha önce bağlı olmayan eşleşmiş bir cihaz bağlanır. Tek bir cihaz ya da eşleşmiş herhangi bir cihaz seçebilirsiniz. |

Değeri olmayan bir değişken, bir koşulda yanlış sayılır.

## Adımlar

Adımlar bir düğmenin eylemleriyle aynıdır; If blokları da dahil. Bkz. [Eylemler ve makrolar](/tr/guide/actions).

Bir kuralı denemek için kural listesindeki **Şimdi çalıştır** düğmesini kullanın. Bu düğme açma/kapama anahtarını ve bekleme süresini yok sayar, ama aşağıdaki sınırları yok saymaz.

## Sınırlar

- **Çalıştırmalar arasında en az … saniye bekle**, bir kuralın bekleme süresidir. Kural bu süre dolmadan yeniden başlamaz.
- Bir kuralın aynı anda en fazla bir çalışması bekleyebilir ya da çalışabilir. Bu arada gelen başlatmalar atlanır.
- Aynı anda en fazla dört kural çalışır; diğerleri bekler.
- Çok sık başlayan bir kuralın bazı başlatmaları atlanır (`P161`). Sürerse Macro Grid onu kapatır (`P162`). İkisi de [Tanılama Mesajları](/tr/guide/troubleshooting) penceresinde görünür.

## Kapatma ve yedekleme

- Her kuralın kendi açma/kapama anahtarı vardır. **Tüm kuralları duraklat** onları devam ettirene kadar hepsini durdurur.
- Listede her kuralın son sonucu görünür.
- Kurallar yedeğin bir parçasıdır. Geri yükleme onları **kapalı** olarak ekler; açmadan önce kontrol edin. Bkz. [Profiller ve sayfalar](/tr/guide/profiles).

## Henüz mümkün olmayanlar

Tuşlara basan kurallar, başka olaylara tepki veren kurallar ve önde olan pencereye bağlı kurallar.
