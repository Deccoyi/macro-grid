# Tercihler

Düzenleyici'de **Ayarlar → Tercihler…** yolunu açın. Bir ayar penceresi açıkken arkadaki Düzenleyici kilitlenir.

| Kategori | Seçenekler |
|---|---|
| **Genel** | Windows'a giriş yaptığımda Macro Grid'i başlat ve bu başlatmanın ne yaptığı (yalnızca sistem tepsisi ya da Düzenleyici'yi aç). Macro Grid'i elle açtığınızda ne olacağı (Düzenleyici penceresi ya da yalnızca sistem tepsisi). Macro Grid'in güncellemeleri kendiliğinden denetleyip denetlemeyeceği ve ön sürümlerin (beta) sayılıp sayılmayacağı. |
| **Görünüm** | **Tema**: koyu ya da açık. |
| **Dil** | Düzenleyici dili: Türkçe (varsayılan) ya da İngilizce, ve eklediğiniz dil paketleri. |
| **Profiller** | Bir cihazın ataması yoksa ve hiçbir otomatik geçiş kuralı geçerli değilse kullanılan **varsayılan profil**. |
| **Önizleme Profilleri** | Düzenleyici önizlemesi için cihaz boyutları ekleyin ya da kaldırın (ad, genişlik, yükseklik). |
| **Genel Değişken Listesi** | Kendi metin, sayı ve Doğru / Yanlış değişkenleriniz. Bkz. [Değişkenler ve metin](/tr/guide/variables). |
| **Otomasyon** | Bir eylem listesini kendiliğinden çalıştıran kurallar ve hepsini duraklatan anahtar. Bkz. [Otomasyon kuralları](/tr/guide/automation). |

## Dil paketleri

Düzenleyici Türkçe ve İngilizce ile gelir. Başka bir dil eklemek için Dil sayfasında bir CSV tablosu dışa aktarın, **translation** (çeviri) sütunundaki metinleri çevirin ve CSV'yi **CSV içe aktar…** ile geri yükleyin. Dil sayfası ne kadarının çevrildiğini ve kaç satırın gözden geçirilmesi gerektiğini gösterir; **Kullan**, **CSV dışa aktar**, **CSV içe aktar…** ve **Kaldır** düğmelerini sunar. Dil paketi yalnızca düzenleyicideki metinleri değiştirir, davranışı değil. İzin ve kaldırma pencereleri İngilizce metni köşeli parantez içinde de gösterir.

## Bildirimler

- **Şifrelenmemiş bağlantılara izin ver** varsayılan olarak açıktır. Kapatırsanız diğer cihazlara yalnızca şifreli bağlantı noktası hizmet verir. Bkz. [Güvenlik](/tr/guide/security).
- Bir web widget'ının adresinin altındaki ipuçları şimdilik ya da kalıcı olarak kapatılabilir. **Kapatılan uyarıları yeniden göster**, "Bir daha gösterme" ile kapatılanları geri getirir.

Tercihler `%AppData%\MacroGrid\preferences.json` dosyasına kaydedilir.
